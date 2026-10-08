using BloggerBazar.Application.Validation;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Offers;

public static class OfferFormats
{
    public const string Stories = "stories";
    public const string Reels = "reels";
    public const string Post = "post";
    public const string Integration = "integration";

    private static readonly Dictionary<string, CollaborationFormat> ByName = new(StringComparer.Ordinal)
    {
        [Stories] = CollaborationFormat.Stories,
        [Reels] = CollaborationFormat.Reels,
        [Post] = CollaborationFormat.Post,
        [Integration] = CollaborationFormat.Integration
    };

    public static bool IsKnown(string? value) => value is not null && ByName.ContainsKey(value);

    public static CollaborationFormat Parse(string value) => ByName[value];

    public static string? ToName(CollaborationFormat? format) => format is null ? null : ByName.First(pair => pair.Value == format).Key;
}

public static class OfferStates
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Declined = "declined";
    public const string Expired = "expired";
}

public sealed record OfferDto(
    Guid Id,
    Guid BloggerId,
    string CounterpartyName,
    string? CounterpartyImageUrl,
    string? Format,
    int? OfferedBudget,
    DateTime? Deadline,
    string Message,
    string State,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    Guid? DealId,
    bool CanRespond,
    Guid? BusinessId = null)
{
    internal static OfferDto From(CollaborationRequest offer, MarketplaceRole viewerRole, DateTime utcNow) =>
        viewerRole == MarketplaceRole.Blogger
            ? Build(offer, true, offer.Business.Name, offer.Business.LogoUrl, utcNow)
            : Build(offer, false, offer.Blogger.Name, offer.Blogger.AvatarUrl, utcNow);

    internal static OfferDto ForBusiness(CollaborationRequest offer, BloggerProfile blogger, DateTime utcNow) =>
        Build(offer, false, blogger.Name, blogger.AvatarUrl, utcNow);

    private static OfferDto Build(CollaborationRequest offer, bool viewerIsBlogger, string counterpartyName, string? counterpartyImageUrl, DateTime utcNow)
    {
        var state = StateOf(offer, utcNow);
        return new(
            offer.Id,
            offer.BloggerId,
            counterpartyName,
            counterpartyImageUrl,
            OfferFormats.ToName(offer.Format),
            offer.OfferedBudget,
            offer.Deadline,
            offer.Message,
            state,
            offer.CreatedAtUtc,
            offer.ExpiresAtUtc,
            offer.Deal?.Id,
            viewerIsBlogger && state == OfferStates.Pending,
            offer.BusinessId);
    }

    internal static string StateOf(CollaborationRequest offer, DateTime utcNow) => offer.Status switch
    {
        CollaborationRequestStatus.Accepted => OfferStates.Accepted,
        CollaborationRequestStatus.Declined => OfferStates.Declined,
        CollaborationRequestStatus.Expired => OfferStates.Expired,
        _ => offer.IsExpiredAt(utcNow) ? OfferStates.Expired : OfferStates.Pending
    };
}

public sealed record OfferDecisionDto(Guid Id, string State, Guid? DealId);

internal static class OfferAccess
{
    internal const int DailyLimit = 20;
    internal const string DailyLimitCode = "offer_daily_limit";
    internal const string AlreadyActiveCode = "offer_already_active";

    internal static InvalidOperationException NotFound() => new("Offer was not found.");

    internal static async Task<DealParticipantContext> RequireParticipantAsync(
        IPlatformUserRepository users,
        IBloggerProfileRepository bloggers,
        IBusinessProfileRepository businesses,
        long telegramUserId,
        CancellationToken cancellationToken) =>
        await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, telegramUserId, cancellationToken) ?? throw NotFound();
}

public sealed record CreateOfferCommand(long TelegramUserId, Guid BloggerId, string Format, int? OfferedBudget, DateTime? Deadline, string Message) : IRequest<OfferDto>;

public sealed class CreateOfferValidator : AbstractValidator<CreateOfferCommand>
{
    public CreateOfferValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.BloggerId).NotEmpty();
        RuleFor(command => command.Format).Must(OfferFormats.IsKnown);
        RuleFor(command => command.OfferedBudget).InclusiveBetween(0, InputLimits.MaxMoney).When(command => command.OfferedBudget.HasValue);
        RuleFor(command => command.Deadline).GreaterThan(_ => DateTime.UtcNow.AddDays(-1)).When(command => command.Deadline.HasValue);
        RuleFor(command => command.Message).NotEmpty().MaximumLength(1000);
    }
}

public sealed class CreateOfferHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ICollaborationRequestRepository offers,
    IUnitOfWork unitOfWork,
    ITelegramBotClient? botClient = null,
    ILogger<CreateOfferHandler>? logger = null) : IRequestHandler<CreateOfferCommand, OfferDto>
{
    public async Task<OfferDto> Handle(CreateOfferCommand command, CancellationToken cancellationToken)
    {
        var business = await DealAccess.RequireBusinessAsync(users, businesses, command.TelegramUserId, cancellationToken);
        var blogger = await bloggers.GetByIdAsync(command.BloggerId, cancellationToken);
        if (blogger is null || blogger.Status != BloggerStatus.Approved
            || await users.GetByTelegramUserIdAsync(blogger.TelegramUserId, cancellationToken) is { IsBlocked: true } or { IsDeleted: true })
        {
            throw new InvalidOperationException("Blogger profile was not found.");
        }

        if (blogger.TelegramUserId == business.TelegramUserId)
        {
            throw new InvalidOperationException("You cannot send an offer to your own profile.");
        }

        var now = DateTime.UtcNow;
        if (await offers.CountOffersSinceAsync(business.Id, now.AddDays(-1), cancellationToken) >= OfferAccess.DailyLimit)
        {
            throw new BusinessRuleConflictException(OfferAccess.DailyLimitCode, "Daily offer limit reached.");
        }

        var pending = await offers.GetPendingOfferAsync(business.Id, blogger.Id, cancellationToken);
        if (pending is not null)
        {
            if (!pending.IsExpiredAt(now))
            {
                throw new BusinessRuleConflictException(OfferAccess.AlreadyActiveCode, "An active offer to this blogger already exists.");
            }

            pending.Expire();
        }

        var offer = CollaborationRequest.CreateOffer(blogger.Id, business.Id, command.Message.Trim(), OfferFormats.Parse(command.Format), command.OfferedBudget, command.Deadline);
        await offers.AddAsync(offer, cancellationToken);
        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            throw new BusinessRuleConflictException(OfferAccess.AlreadyActiveCode, "An active offer to this blogger already exists.");
        }

        await BestEffortTelegramNotification.SendAsync(
            botClient,
            logger,
            blogger.TelegramUserId,
            BotMessages.OfferReceived(business.Name),
            $"/offer/{offer.Id}",
            cancellationToken);
        return OfferDto.ForBusiness(offer, blogger, now);
    }
}

public sealed record GetMyOffersQuery(long TelegramUserId) : IRequest<IReadOnlyList<OfferDto>>;

public sealed class GetMyOffersHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ICollaborationRequestRepository offers) : IRequestHandler<GetMyOffersQuery, IReadOnlyList<OfferDto>>
{
    public async Task<IReadOnlyList<OfferDto>> Handle(GetMyOffersQuery query, CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, query.TelegramUserId, cancellationToken);
        if (participant is null)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var items = await offers.ListOffersForParticipantAsync(participant.Role, participant.ProfileId, 100, cancellationToken);
        return items.Select(offer => OfferDto.From(offer, participant.Role, now)).ToArray();
    }
}

public sealed record GetMyOfferQuery(long TelegramUserId, Guid OfferId) : IRequest<OfferDto>;

public sealed class GetMyOfferHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ICollaborationRequestRepository offers) : IRequestHandler<GetMyOfferQuery, OfferDto>
{
    public async Task<OfferDto> Handle(GetMyOfferQuery query, CancellationToken cancellationToken)
    {
        var participant = await OfferAccess.RequireParticipantAsync(users, bloggers, businesses, query.TelegramUserId, cancellationToken);
        var offer = await offers.GetOfferForParticipantAsync(query.OfferId, participant.Role, participant.ProfileId, cancellationToken)
            ?? throw OfferAccess.NotFound();
        return OfferDto.From(offer, participant.Role, DateTime.UtcNow);
    }
}

public sealed record AcceptOfferCommand(long TelegramUserId, Guid OfferId) : IRequest<OfferDecisionDto>;

public sealed class AcceptOfferHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ICollaborationRequestRepository offers,
    IDealRepository deals,
    IUnitOfWork unitOfWork,
    ITelegramBotClient? botClient = null,
    ILogger<AcceptOfferHandler>? logger = null) : IRequestHandler<AcceptOfferCommand, OfferDecisionDto>
{
    public async Task<OfferDecisionDto> Handle(AcceptOfferCommand command, CancellationToken cancellationToken)
    {
        var participant = await OfferAccess.RequireParticipantAsync(users, bloggers, businesses, command.TelegramUserId, cancellationToken);
        if (participant.Role != MarketplaceRole.Blogger)
        {
            throw OfferAccess.NotFound();
        }

        var offer = await offers.GetOfferForParticipantAsync(command.OfferId, participant.Role, participant.ProfileId, cancellationToken)
            ?? throw OfferAccess.NotFound();
        if (offer.Status == CollaborationRequestStatus.Accepted)
        {
            var existing = offer.Deal ?? await deals.GetByCollaborationRequestIdAsync(offer.Id, cancellationToken);
            return new(offer.Id, OfferStates.Accepted, existing?.Id);
        }

        await OfferDecision.EnsurePendingAsync(offer, unitOfWork, cancellationToken);
        offer.Accept();
        var deal = Deal.CreateFromCollaborationRequest(offer.Id, offer.BloggerId, offer.BusinessId);
        await deals.AddAsync(deal, cancellationToken);
        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            var persisted = await deals.GetByCollaborationRequestIdAsync(offer.Id, cancellationToken)
                ?? throw new InvalidOperationException("Offer acceptance conflicts with an existing deal.");
            return new(offer.Id, OfferStates.Accepted, persisted.Id);
        }

        await BestEffortTelegramNotification.SendAsync(
            botClient,
            logger,
            offer.Business.TelegramUserId,
            BotMessages.OfferAccepted(offer.Blogger.Name),
            $"/deal/{deal.Id}",
            cancellationToken);
        return new(offer.Id, OfferStates.Accepted, deal.Id);
    }
}

public sealed record DeclineOfferCommand(long TelegramUserId, Guid OfferId) : IRequest<OfferDecisionDto>;

public sealed class DeclineOfferHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ICollaborationRequestRepository offers,
    IUnitOfWork unitOfWork,
    ITelegramBotClient? botClient = null,
    ILogger<DeclineOfferHandler>? logger = null) : IRequestHandler<DeclineOfferCommand, OfferDecisionDto>
{
    public async Task<OfferDecisionDto> Handle(DeclineOfferCommand command, CancellationToken cancellationToken)
    {
        var participant = await OfferAccess.RequireParticipantAsync(users, bloggers, businesses, command.TelegramUserId, cancellationToken);
        if (participant.Role != MarketplaceRole.Blogger)
        {
            throw OfferAccess.NotFound();
        }

        var offer = await offers.GetOfferForParticipantAsync(command.OfferId, participant.Role, participant.ProfileId, cancellationToken)
            ?? throw OfferAccess.NotFound();
        if (offer.Status == CollaborationRequestStatus.Declined)
        {
            return new(offer.Id, OfferStates.Declined, null);
        }

        await OfferDecision.EnsurePendingAsync(offer, unitOfWork, cancellationToken);
        offer.Decline();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await BestEffortTelegramNotification.SendAsync(
            botClient,
            logger,
            offer.Business.TelegramUserId,
            BotMessages.OfferDeclined(offer.Blogger.Name),
            $"/offer/{offer.Id}",
            cancellationToken);
        return new(offer.Id, OfferStates.Declined, null);
    }
}

internal static class OfferDecision
{
    internal static async Task EnsurePendingAsync(CollaborationRequest offer, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        if (!offer.IsPending)
        {
            throw new InvalidOperationException("The offer is no longer pending.");
        }

        if (offer.IsExpiredAt(DateTime.UtcNow))
        {
            offer.Expire();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("The offer has expired.");
        }
    }
}
