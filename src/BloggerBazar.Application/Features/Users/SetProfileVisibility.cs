using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Users;

// D50: the owner pauses one of their role profiles. A hidden profile leaves listings and the home page,
// cannot be sent new offers or applications and cannot send them; deals, reviews and its page by link stay.
public sealed record SetProfileVisibilityCommand(long TelegramUserId, MarketplaceRole Role, bool Hidden) : IRequest<ProfileVisibilityDto>;

public sealed record ProfileVisibilityDto(int Role, bool IsHidden);

public static class ProfileVisibilityCodes
{
    public const string ProfileHidden = "profile_hidden";
    public const string BusinessHidden = "business_hidden";
}

public sealed class SetProfileVisibilityValidator : AbstractValidator<SetProfileVisibilityCommand>
{
    public SetProfileVisibilityValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Role).IsInEnum();
    }
}

public sealed class SetProfileVisibilityHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IBrandFaceProfileRepository brandFaces,
    IUnitOfWork unitOfWork,
    ICatalogCache? cache = null) : IRequestHandler<SetProfileVisibilityCommand, ProfileVisibilityDto>
{
    public async Task<ProfileVisibilityDto> Handle(SetProfileVisibilityCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted)
        {
            throw new UnauthorizedAccessException("An active account is required.");
        }

        // Only the account's own profile of that role; a missing one reads as not found.
        var found = command.Role switch
        {
            MarketplaceRole.Blogger => await bloggers.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken) is { } blogger && Apply(blogger.SetHidden),
            MarketplaceRole.Business => await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken) is { } business && Apply(business.SetHidden),
            MarketplaceRole.BrandFace => await brandFaces.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken) is { } brandFace && Apply(brandFace.SetHidden),
            _ => false
        };
        if (!found)
        {
            throw new InvalidOperationException("Profile was not found.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (cache is not null) await cache.RotateNamespaceVersionAsync(cancellationToken);
        return new ProfileVisibilityDto((int)command.Role, command.Hidden);

        bool Apply(Action<bool> setHidden)
        {
            setHidden(command.Hidden);
            return true;
        }
    }
}
