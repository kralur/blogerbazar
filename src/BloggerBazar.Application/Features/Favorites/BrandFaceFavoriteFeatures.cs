using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Favorites;

public sealed record BrandFaceFavoriteDto(
    Guid Id,
    string Name,
    string City,
    IReadOnlyCollection<string> Categories,
    IReadOnlyCollection<string> Languages,
    int? CollaborationPrice,
    string? AvatarUrl,
    bool IsPromoted);

public sealed record BrandFaceFavoritesPageDto(
    IReadOnlyList<BrandFaceFavoriteDto> Items,
    int Total,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record GetBrandFaceFavoritesQuery(long TelegramUserId, int Page, int PageSize) : IRequest<BrandFaceFavoritesPageDto>;
public sealed record SaveBrandFaceFavoriteCommand(long TelegramUserId, Guid BrandFaceId) : IRequest<FavoriteOperationDto>;
public sealed record RemoveBrandFaceFavoriteCommand(long TelegramUserId, Guid BrandFaceId) : IRequest<FavoriteOperationDto>;

public sealed class GetBrandFaceFavoritesQueryValidator : AbstractValidator<GetBrandFaceFavoritesQuery>
{
    public GetBrandFaceFavoritesQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class SaveBrandFaceFavoriteCommandValidator : AbstractValidator<SaveBrandFaceFavoriteCommand>
{
    public SaveBrandFaceFavoriteCommandValidator() => RuleFor(command => command.BrandFaceId).NotEmpty();
}

public sealed class RemoveBrandFaceFavoriteCommandValidator : AbstractValidator<RemoveBrandFaceFavoriteCommand>
{
    public RemoveBrandFaceFavoriteCommandValidator() => RuleFor(command => command.BrandFaceId).NotEmpty();
}

public sealed class GetBrandFaceFavoritesQueryHandler(
    IPlatformUserRepository users,
    IBrandFaceFavoritesReadModel favorites) : IRequestHandler<GetBrandFaceFavoritesQuery, BrandFaceFavoritesPageDto>
{
    public async Task<BrandFaceFavoritesPageDto> Handle(GetBrandFaceFavoritesQuery query, CancellationToken cancellationToken)
    {
        var user = await BrandFaceFavoriteAccess.GetBusinessUserAsync(users, query.TelegramUserId, cancellationToken);
        return await favorites.GetBrandFaceFavoritesAsync(user.Id, query.Page, query.PageSize, cancellationToken);
    }
}

public sealed class SaveBrandFaceFavoriteCommandHandler(
    IPlatformUserRepository users,
    IBrandFaceProfileRepository brandFaces,
    IBrandFaceFavoriteRepository favorites,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork) : IRequestHandler<SaveBrandFaceFavoriteCommand, FavoriteOperationDto>
{
    public async Task<FavoriteOperationDto> Handle(SaveBrandFaceFavoriteCommand command, CancellationToken cancellationToken)
    {
        var user = await BrandFaceFavoriteAccess.GetBusinessUserAsync(users, command.TelegramUserId, cancellationToken);
        var brandFace = await brandFaces.GetByIdAsync(command.BrandFaceId, cancellationToken)
            ?? throw new InvalidOperationException("Brand Face was not found.");

        if (brandFace.TelegramUserId == command.TelegramUserId)
        {
            throw new InvalidOperationException("You cannot save your own Brand Face profile.");
        }

        if (await favorites.GetAsync(user.Id, brandFace.Id, cancellationToken) is not null)
        {
            return new FavoriteOperationDto(true);
        }

        await favorites.AddAsync(BrandFaceFavorite.Create(user.Id, brandFace.Id), cancellationToken);
        await auditLogs.AddAsync(AuditLog.Create(command.TelegramUserId, "brand-face-favorite.saved", "BrandFaceProfile", brandFace.Id.ToString()), cancellationToken);

        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            return new FavoriteOperationDto(true);
        }

        return new FavoriteOperationDto(true);
    }
}

public sealed class RemoveBrandFaceFavoriteCommandHandler(
    IPlatformUserRepository users,
    IBrandFaceFavoriteRepository favorites,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveBrandFaceFavoriteCommand, FavoriteOperationDto>
{
    public async Task<FavoriteOperationDto> Handle(RemoveBrandFaceFavoriteCommand command, CancellationToken cancellationToken)
    {
        var user = await BrandFaceFavoriteAccess.GetBusinessUserAsync(users, command.TelegramUserId, cancellationToken);
        if (!await favorites.DeleteAsync(user.Id, command.BrandFaceId, cancellationToken))
        {
            return new FavoriteOperationDto(false);
        }

        await auditLogs.AddAsync(AuditLog.Create(command.TelegramUserId, "brand-face-favorite.removed", "BrandFaceProfile", command.BrandFaceId.ToString()), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new FavoriteOperationDto(false);
    }
}

internal static class BrandFaceFavoriteAccess
{
    public static async Task<PlatformUser> GetBusinessUserAsync(IPlatformUserRepository users, long telegramUserId, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("The platform user was not found.");

        if (user.IsBlocked || user.IsDeleted || user.SelectedMarketplaceRole != MarketplaceRole.Business)
        {
            throw new UnauthorizedAccessException("Only businesses can manage Brand Face favorites.");
        }

        return user;
    }
}
