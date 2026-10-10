using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Application.Validation;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Deals;

// D51: the agreed price and links to published ads. Both are optional so a deal never waits on paperwork.
public static class DealResultCodes
{
    public const string PriceFixedByOffer = "price_fixed_by_offer";
    public const string PublicationLimit = "publication_limit";
    public const string PublicationDuplicate = "publication_duplicate";
    public const string PublicationConfirmed = "publication_confirmed";
}

public sealed record DealPublicationDto(Guid Id, string Url, int? Views, bool Confirmed, DateTime CreatedAtUtc, DateTime? ConfirmedAtUtc)
{
    public static DealPublicationDto From(DealPublication publication) =>
        new(publication.Id, publication.Url, publication.Views, publication.IsConfirmed, publication.CreatedAtUtc, publication.ConfirmedAtUtc);
}

public sealed record DealPriceDto(int? AgreedPrice);

public sealed record SetDealPriceCommand(Guid DealId, long TelegramUserId, int Price) : IRequest<DealPriceDto>;
public sealed record AddDealPublicationCommand(Guid DealId, long TelegramUserId, string Url, int? Views) : IRequest<DealPublicationDto>;
public sealed record UpdateDealPublicationViewsCommand(Guid DealId, Guid PublicationId, long TelegramUserId, int? Views) : IRequest<DealPublicationDto>;
public sealed record DeleteDealPublicationCommand(Guid DealId, Guid PublicationId, long TelegramUserId) : IRequest;
public sealed record ConfirmDealPublicationCommand(Guid DealId, Guid PublicationId, long TelegramUserId) : IRequest<DealPublicationDto>;

internal static class DealResultRules
{
    public const int MaxPublications = 5;
    public const int MaxUrlLength = 500;

    // Results stay editable after completion (views grow later) but not on a cancelled deal or with a deleted partner.
    public static void EnsureOpen(Deal deal, DealParticipantContext participant)
    {
        if (deal.Status == DealStatus.Cancelled)
        {
            throw new InvalidOperationException("A cancelled deal has no results.");
        }

        var partnerDeleted = participant.IsCreator ? deal.Business.IsDeleted : deal.Blogger?.IsDeleted ?? deal.BrandFace?.IsDeleted ?? false;
        if (partnerDeleted)
        {
            throw new InvalidOperationException("The partner deleted the account.");
        }
    }

    public static async Task<(DealParticipantContext Participant, Deal Deal)> RequireDealAsync(
        IPlatformUserRepository users,
        IBloggerProfileRepository bloggers,
        IBusinessProfileRepository businesses,
        IBrandFaceProfileRepository? brandFaces,
        IDealRepository deals,
        Guid dealId,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, telegramUserId, cancellationToken, brandFaces)
            ?? throw DealAccess.DealNotFound();
        var deal = await deals.GetForParticipantAsync(dealId, participant.Role, participant.ProfileId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        return (participant, deal);
    }

    public static void RequireCreator(DealParticipantContext participant)
    {
        if (!participant.IsCreator) throw new UnauthorizedAccessException("Only the blogger or brand face of the deal adds publications.");
    }

    public static void RequireBusiness(DealParticipantContext participant)
    {
        if (participant.Role != MarketplaceRole.Business) throw new UnauthorizedAccessException("Only the business of the deal does this.");
    }

    public static InvalidOperationException PublicationNotFound() => new("Publication was not found.");
}

public sealed class SetDealPriceValidator : AbstractValidator<SetDealPriceCommand>
{
    public SetDealPriceValidator()
    {
        RuleFor(command => command.DealId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Price).InclusiveBetween(0, InputLimits.MaxMoney);
    }
}

public sealed class AddDealPublicationValidator : AbstractValidator<AddDealPublicationCommand>
{
    public AddDealPublicationValidator()
    {
        RuleFor(command => command.DealId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Url).NotEmpty().MaximumLength(DealResultRules.MaxUrlLength)
            .Must(url => ContactValidation.IsHttpsUrl(url?.Trim()));
        RuleFor(command => command.Views).InclusiveBetween(0, InputLimits.MaxReach).When(command => command.Views.HasValue);
    }
}

public sealed class UpdateDealPublicationViewsValidator : AbstractValidator<UpdateDealPublicationViewsCommand>
{
    public UpdateDealPublicationViewsValidator()
    {
        RuleFor(command => command.DealId).NotEmpty();
        RuleFor(command => command.PublicationId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Views).InclusiveBetween(0, InputLimits.MaxReach).When(command => command.Views.HasValue);
    }
}

public sealed class DeleteDealPublicationValidator : AbstractValidator<DeleteDealPublicationCommand>
{
    public DeleteDealPublicationValidator()
    {
        RuleFor(command => command.DealId).NotEmpty();
        RuleFor(command => command.PublicationId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
    }
}

public sealed class ConfirmDealPublicationValidator : AbstractValidator<ConfirmDealPublicationCommand>
{
    public ConfirmDealPublicationValidator()
    {
        RuleFor(command => command.DealId).NotEmpty();
        RuleFor(command => command.PublicationId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
    }
}

public sealed class SetDealPriceHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IDealRepository deals,
    IUnitOfWork unitOfWork,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<SetDealPriceCommand, DealPriceDto>
{
    public async Task<DealPriceDto> Handle(SetDealPriceCommand command, CancellationToken cancellationToken)
    {
        var (participant, scoped) = await DealResultRules.RequireDealAsync(users, bloggers, businesses, brandFaces, deals, command.DealId, command.TelegramUserId, cancellationToken);
        DealResultRules.RequireBusiness(participant);
        DealResultRules.EnsureOpen(scoped, participant);
        if (scoped.CollaborationRequest?.OfferedBudget is not null)
        {
            throw new BusinessRuleConflictException(DealResultCodes.PriceFixedByOffer, "The offer already set the price.");
        }

        var deal = await deals.GetByIdAsync(scoped.Id, cancellationToken) ?? throw DealAccess.DealNotFound();
        deal.SetAgreedPrice(command.Price, DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DealPriceDto(deal.AgreedPrice);
    }
}

public sealed class AddDealPublicationHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IDealRepository deals,
    IDealPublicationRepository publications,
    IUnitOfWork unitOfWork,
    IBrandFaceProfileRepository? brandFaces = null,
    ITelegramBotClient? botClient = null,
    ILogger<AddDealPublicationHandler>? logger = null) : IRequestHandler<AddDealPublicationCommand, DealPublicationDto>
{
    public async Task<DealPublicationDto> Handle(AddDealPublicationCommand command, CancellationToken cancellationToken)
    {
        var (participant, deal) = await DealResultRules.RequireDealAsync(users, bloggers, businesses, brandFaces, deals, command.DealId, command.TelegramUserId, cancellationToken);
        DealResultRules.RequireCreator(participant);
        DealResultRules.EnsureOpen(deal, participant);

        var url = command.Url.Trim();
        var existing = await publications.ListForDealAsync(deal.Id, cancellationToken);
        // One link counts once, so the same post never doubles the views.
        if (existing.Any(publication => publication.Url == url))
        {
            throw new BusinessRuleConflictException(DealResultCodes.PublicationDuplicate, "This link is already in the deal.");
        }

        if (existing.Count >= DealResultRules.MaxPublications)
        {
            throw new BusinessRuleConflictException(DealResultCodes.PublicationLimit, "A deal has at most five publications.");
        }

        var publication = DealPublication.Create(deal.Id, url, command.Views, command.TelegramUserId, DateTime.UtcNow);
        await publications.AddAsync(publication, cancellationToken);
        // The unique (deal, url) index settles a double tap.
        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            throw new BusinessRuleConflictException(DealResultCodes.PublicationDuplicate, "This link is already in the deal.");
        }

        await BestEffortTelegramNotification.SendAsync(botClient, logger, deal.Business.TelegramUserId, BotMessages.DealPublicationAdded(deal.CreatorName, DealTopic.Of(deal)), $"/deal/{deal.Id}", cancellationToken);
        return DealPublicationDto.From(publication);
    }
}

public sealed class UpdateDealPublicationViewsHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IDealRepository deals,
    IDealPublicationRepository publications,
    IUnitOfWork unitOfWork,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<UpdateDealPublicationViewsCommand, DealPublicationDto>
{
    public async Task<DealPublicationDto> Handle(UpdateDealPublicationViewsCommand command, CancellationToken cancellationToken)
    {
        var (participant, deal) = await DealResultRules.RequireDealAsync(users, bloggers, businesses, brandFaces, deals, command.DealId, command.TelegramUserId, cancellationToken);
        DealResultRules.RequireCreator(participant);
        DealResultRules.EnsureOpen(deal, participant);
        var publication = await publications.GetForDealAsync(deal.Id, command.PublicationId, cancellationToken)
            ?? throw DealResultRules.PublicationNotFound();

        publication.UpdateViews(command.Views, DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DealPublicationDto.From(publication);
    }
}

public sealed class DeleteDealPublicationHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IDealRepository deals,
    IDealPublicationRepository publications,
    IUnitOfWork unitOfWork,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<DeleteDealPublicationCommand>
{
    public async Task Handle(DeleteDealPublicationCommand command, CancellationToken cancellationToken)
    {
        var (participant, deal) = await DealResultRules.RequireDealAsync(users, bloggers, businesses, brandFaces, deals, command.DealId, command.TelegramUserId, cancellationToken);
        DealResultRules.RequireCreator(participant);
        DealResultRules.EnsureOpen(deal, participant);
        var publication = await publications.GetForDealAsync(deal.Id, command.PublicationId, cancellationToken)
            ?? throw DealResultRules.PublicationNotFound();
        // A confirmed result is part of both sides' history.
        if (publication.IsConfirmed)
        {
            throw new BusinessRuleConflictException(DealResultCodes.PublicationConfirmed, "A confirmed publication stays.");
        }

        publications.Remove(publication);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ConfirmDealPublicationHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IDealRepository deals,
    IDealPublicationRepository publications,
    IUnitOfWork unitOfWork,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<ConfirmDealPublicationCommand, DealPublicationDto>
{
    public async Task<DealPublicationDto> Handle(ConfirmDealPublicationCommand command, CancellationToken cancellationToken)
    {
        var (participant, deal) = await DealResultRules.RequireDealAsync(users, bloggers, businesses, brandFaces, deals, command.DealId, command.TelegramUserId, cancellationToken);
        DealResultRules.RequireBusiness(participant);
        DealResultRules.EnsureOpen(deal, participant);
        var publication = await publications.GetForDealAsync(deal.Id, command.PublicationId, cancellationToken)
            ?? throw DealResultRules.PublicationNotFound();

        publication.Confirm(command.TelegramUserId, DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DealPublicationDto.From(publication);
    }
}
