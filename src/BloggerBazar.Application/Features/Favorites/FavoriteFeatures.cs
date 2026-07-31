using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Favorites;

public sealed record FavoriteBloggerDto(
    Guid BloggerId,
    string Name,
    string City,
    IReadOnlyCollection<string> Categories,
    string? AvatarUrl,
    int TotalFollowers,
    DateTime CreatedAtUtc);

public sealed record FavoritesPageDto(IReadOnlyList<FavoriteBloggerDto> Items, int Total, int Page, int PageSize);

public sealed record GetFavoritesQuery(long TelegramUserId, int Page, int PageSize) : IRequest<FavoritesPageDto>;

public sealed class GetFavoritesQueryValidator : AbstractValidator<GetFavoritesQuery>
{
    public GetFavoritesQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class GetFavoritesQueryHandler(
    IPlatformUserRepository users,
    IFavoritesReadModel favorites) : IRequestHandler<GetFavoritesQuery, FavoritesPageDto>
{
    public async Task<FavoritesPageDto> Handle(GetFavoritesQuery query, CancellationToken cancellationToken)
    {
        var user = await FavoriteAccess.GetEligibleUserAsync(users, query.TelegramUserId, cancellationToken);
        return await favorites.GetFavoritesAsync(user.Id, query.Page, query.PageSize, cancellationToken);
    }
}

public sealed record SaveFavoriteCommand(long TelegramUserId, Guid BloggerId) : IRequest<FavoriteOperationDto>;

public sealed record RemoveFavoriteCommand(long TelegramUserId, Guid BloggerId) : IRequest<FavoriteOperationDto>;

public sealed record FavoriteOperationDto(bool IsFavorite);

public sealed class SaveFavoriteCommandValidator : AbstractValidator<SaveFavoriteCommand>
{
    public SaveFavoriteCommandValidator() => RuleFor(command => command.BloggerId).NotEmpty();
}

public sealed class RemoveFavoriteCommandValidator : AbstractValidator<RemoveFavoriteCommand>
{
    public RemoveFavoriteCommandValidator() => RuleFor(command => command.BloggerId).NotEmpty();
}

public sealed class SaveFavoriteCommandHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IFavoriteRepository favorites,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork) : IRequestHandler<SaveFavoriteCommand, FavoriteOperationDto>
{
    public async Task<FavoriteOperationDto> Handle(SaveFavoriteCommand command, CancellationToken cancellationToken)
    {
        var user = await FavoriteAccess.GetEligibleUserAsync(users, command.TelegramUserId, cancellationToken);
        var blogger = await bloggers.GetByIdAsync(command.BloggerId, cancellationToken)
            ?? throw new InvalidOperationException("Blogger was not found.");

        if (blogger.Status != BloggerStatus.Approved)
        {
            throw new InvalidOperationException("Blogger was not found.");
        }

        if (blogger.TelegramUserId == command.TelegramUserId)
        {
            throw new InvalidOperationException("You cannot save your own blogger profile.");
        }

        if (await favorites.GetAsync(user.Id, blogger.Id, cancellationToken) is not null)
        {
            return new FavoriteOperationDto(true);
        }

        await favorites.AddAsync(Favorite.Create(user.Id, blogger.Id), cancellationToken);
        await auditLogs.AddAsync(AuditLog.Create(command.TelegramUserId, "favorite.saved", "BloggerProfile", blogger.Id.ToString()), cancellationToken);

        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            return new FavoriteOperationDto(true);
        }

        return new FavoriteOperationDto(true);
    }
}

public sealed class RemoveFavoriteCommandHandler(
    IPlatformUserRepository users,
    IFavoriteRepository favorites,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveFavoriteCommand, FavoriteOperationDto>
{
    public async Task<FavoriteOperationDto> Handle(RemoveFavoriteCommand command, CancellationToken cancellationToken)
    {
        var user = await FavoriteAccess.GetEligibleUserAsync(users, command.TelegramUserId, cancellationToken);
        if (!await favorites.DeleteAsync(user.Id, command.BloggerId, cancellationToken))
        {
            return new FavoriteOperationDto(false);
        }

        await auditLogs.AddAsync(AuditLog.Create(command.TelegramUserId, "favorite.removed", "BloggerProfile", command.BloggerId.ToString()), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new FavoriteOperationDto(false);
    }
}

internal static class FavoriteAccess
{
    public static async Task<PlatformUser> GetEligibleUserAsync(IPlatformUserRepository users, long telegramUserId, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("The platform user was not found.");

        if (user.IsBlocked || user.IsDeleted || user.SelectedMarketplaceRole is not (MarketplaceRole.Business or MarketplaceRole.BrandFace))
        {
            throw new UnauthorizedAccessException("This marketplace role cannot manage favorites.");
        }

        return user;
    }
}
