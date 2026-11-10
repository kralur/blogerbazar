using BloggerBazar.Domain.Enums;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using FluentValidation;
using BloggerBazar.Application.Validation;
using BloggerBazar.Application.Features.Campaigns;
using MediatR;

namespace BloggerBazar.Application.Features.Businesses;

public sealed record UpdateBusinessProfileCommand(
    long TelegramUserId,
    string Name,
    string? Username,
    string? City,
    string? LogoUrl,
    string? WebsiteUrl,
    string? Description,
    string? Phone,
    string? Email) : IRequest<BusinessProfileDto>;

public sealed class UpdateBusinessProfileValidator : AbstractValidator<UpdateBusinessProfileCommand>
{
    public UpdateBusinessProfileValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150).Must(ContactValidation.HasNoLink);
        RuleFor(command => command.Username).Must(ContactValidation.IsTelegramUsername).When(command => command.Username is not null);
        RuleFor(command => command.City).NotEmpty().MaximumLength(80).Must(Regions.IsKnown);
        RuleFor(command => command.LogoUrl).Must(ContactValidation.IsHttpsUrl).When(command => command.LogoUrl is not null);
        RuleFor(command => command.WebsiteUrl).Must(ContactValidation.IsHttpsUrl).When(command => command.WebsiteUrl is not null);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.Phone).NotEmpty().Must(ContactValidation.IsVerifiedPhone);
        RuleFor(command => command.Email).EmailAddress().MaximumLength(254).When(command => command.Email is not null);
    }
}

public sealed class UpdateBusinessProfileHandler(IBusinessProfileRepository businesses, IUnitOfWork unitOfWork, ICatalogCache? cache = null)
    : IRequestHandler<UpdateBusinessProfileCommand, BusinessProfileDto>
{
    public async Task<BusinessProfileDto> Handle(UpdateBusinessProfileCommand command, CancellationToken cancellationToken)
    {
        // D64: the owner or an admin of the company edits its profile.
        var profile = await BusinessTeamAccess.FindAsync(businesses, command.TelegramUserId, BusinessPermission.ManageProfile, cancellationToken)
            ?? throw new InvalidOperationException("Create a business profile before updating it.");
        // D64 stage C: the company's Telegram and phone are its owner's (D40, D41); an admin editing the profile does not
        // put their own on it.
        var editorIsOwner = profile.TelegramUserId == command.TelegramUserId;
        var username = editorIsOwner ? command.Username?.Trim() : profile.Username;
        var phone = editorIsOwner ? command.Phone?.Trim() : profile.Phone;

        var usernameOwner = username is null ? null : await businesses.GetByUsernameAsync(username, cancellationToken);
        if (usernameOwner is not null && usernameOwner.Id != profile.Id)
        {
            throw new InvalidOperationException("This Telegram username is already used by another business profile.");
        }

        profile.Update(
            command.Name.Trim(),
            username,
            command.City?.Trim(),
            profile.LogoUrl, // only our upload changes the logo
            command.WebsiteUrl?.Trim(),
            command.Description?.Trim(),
            phone,
            command.Email?.Trim());
        profile.PublishAfterOwnerEdit();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (cache is not null)
        {
            await cache.RotateNamespaceVersionAsync(cancellationToken);
            await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
        }
        return BusinessProfileDto.From(profile);
    }
}
