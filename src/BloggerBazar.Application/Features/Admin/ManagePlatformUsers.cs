using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Admin;

// VerifiedPhone and the profile flags let an admin find a person and see which roles they have (additive).
// LastSeenAtUtc and Source (D56) were added for the web admin: the last app open (at most hourly) and the ad label.
public sealed record AdminPlatformUserDto(long TelegramUserId, string FirstName, string? Username, PlatformRole Role, bool IsBlocked, bool IsDeleted, DateTime? DeletedAtUtc, long? DeletedByTelegramUserId, DateTime CreatedAtUtc,
    string? VerifiedPhone = null, bool HasBloggerProfile = false, bool HasBusinessProfile = false, bool HasBrandFaceProfile = false,
    DateTime? LastSeenAtUtc = null, string? Source = null)
{
    public static AdminPlatformUserDto From(PlatformUser user) => new(user.TelegramUserId, user.FirstName, user.Username, user.Role, user.IsBlocked, user.IsDeleted, user.DeletedAtUtc, user.DeletedByTelegramUserId, user.CreatedAtUtc, user.VerifiedPhone,
        LastSeenAtUtc: user.LastSeenAtUtc, Source: user.Source);
}

// Filter values for the admin user list; anything else means all users.
public static class AdminUserFilters
{
    public const string Bloggers = "bloggers";
    public const string Businesses = "businesses";
    public const string BrandFaces = "brandFaces";
    public const string Admins = "admins";
    public const string Blocked = "blocked";
    public const string Deleted = "deleted";
    public const string New = "new";
    public const int NewDays = 7;
}

public sealed record AuditLogDto(Guid Id, long ActorTelegramUserId, string Action, string TargetType, string TargetId, string? Details, string? CorrelationId, DateTime CreatedAtUtc)
{
    public static AuditLogDto From(AuditLog entry) => new(entry.Id, entry.ActorTelegramUserId, entry.Action, entry.TargetType, entry.TargetId, entry.Details, entry.CorrelationId, entry.CreatedAtUtc);
}

public sealed record GetAdminUsersQuery(long ActorTelegramUserId, int Take = 100, string? Search = null, string? Filter = null) : IRequest<IReadOnlyList<AdminPlatformUserDto>>;
// From and To are Tashkent calendar days, both included (web admin date filter); without them the newest entries.
public sealed record GetAuditLogQuery(long ActorTelegramUserId, int Take = 100, DateOnly? From = null, DateOnly? To = null) : IRequest<IReadOnlyList<AuditLogDto>>;

public sealed class GetAuditLogValidator : AbstractValidator<GetAuditLogQuery>
{
    public GetAuditLogValidator()
    {
        RuleFor(query => query).Must(query => query.From is null || query.To is null || query.From <= query.To);
    }
}
public sealed record UpdatePlatformUserRoleCommand(long ActorTelegramUserId, long TargetTelegramUserId, PlatformRole Role) : IRequest<AdminPlatformUserDto>;
public sealed record SetPlatformUserBlockedCommand(long ActorTelegramUserId, long TargetTelegramUserId, bool IsBlocked) : IRequest<AdminPlatformUserDto>;

public sealed class GetAdminUsersHandler(IPlatformUserRepository users, BloggerBazar.Application.Abstractions.Security.IAdminAccessPolicy access, IAdminMarketplaceReadModel? marketplace = null)
    : IRequestHandler<GetAdminUsersQuery, IReadOnlyList<AdminPlatformUserDto>>
{
    public async Task<IReadOnlyList<AdminPlatformUserDto>> Handle(GetAdminUsersQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        var take = Math.Clamp(query.Take, 1, 200);
        // Search by phone, name, @username or Telegram id, with a role/status filter.
        if (marketplace is not null)
        {
            return await marketplace.SearchUsersAsync(string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(), query.Filter, take, cancellationToken);
        }

        return (await users.GetAllAsync(take, cancellationToken)).Select(AdminPlatformUserDto.From).ToArray();
    }
}

// Admin user card: everything about one person in one screen, to judge a complaint or a dispute.
// Role is "blogger", "brandFace" or "business" (D26 strings); deals are the newest ten on any of the person's profiles.
public sealed record AdminUserProfileDto(string Role, Guid Id, string Name, bool IsHidden, bool IsDeleted);
public sealed record AdminUserDealDto(Guid Id, string Role, string? PartnerName, string? Title, int Status, int? AgreedPrice, DateTime CreatedAtUtc);
public sealed record AdminUserDetailsDto(
    AdminPlatformUserDto User,
    IReadOnlyList<AdminUserProfileDto> Profiles,
    int ActiveDeals,
    int CompletedDeals,
    int CancelledDeals,
    IReadOnlyList<AdminUserDealDto> RecentDeals,
    int ComplaintsAgainst,
    int OpenComplaintsAgainst,
    int ComplaintsFiled);

public sealed record GetAdminUserDetailsQuery(long ActorTelegramUserId, long TelegramUserId) : IRequest<AdminUserDetailsDto>;

public sealed class GetAdminUserDetailsHandler(BloggerBazar.Application.Abstractions.Security.IAdminAccessPolicy access, IAdminMarketplaceReadModel marketplace)
    : IRequestHandler<GetAdminUserDetailsQuery, AdminUserDetailsDto>
{
    public async Task<AdminUserDetailsDto> Handle(GetAdminUserDetailsQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        return await marketplace.GetUserDetailsAsync(query.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Platform user was not found.");
    }
}

public sealed class GetAuditLogHandler(IAuditLogRepository auditLogs, BloggerBazar.Application.Abstractions.Security.IAdminAccessPolicy access)
    : IRequestHandler<GetAuditLogQuery, IReadOnlyList<AuditLogDto>>
{
    public async Task<IReadOnlyList<AuditLogDto>> Handle(GetAuditLogQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        var take = Math.Clamp(query.Take, 1, 500);
        if (query.From is null && query.To is null) return (await auditLogs.GetRecentAsync(take, cancellationToken)).Select(AuditLogDto.From).ToArray();
        var fromUtc = query.From is { } from ? AdminAnalytics.StartUtc(from) : (DateTime?)null;
        var toUtc = query.To is { } to ? AdminAnalytics.StartUtc(to.AddDays(1)) : (DateTime?)null;
        return (await auditLogs.GetRecentAsync(take, fromUtc, toUtc, cancellationToken)).Select(AuditLogDto.From).ToArray();
    }
}

public sealed class UpdatePlatformUserRoleHandler(IPlatformUserRepository users, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdatePlatformUserRoleCommand, AdminPlatformUserDto>
{
    public async Task<AdminPlatformUserDto> Handle(UpdatePlatformUserRoleCommand command, CancellationToken cancellationToken)
    {
        var actor = await RequireOwnerAsync(users, command.ActorTelegramUserId, cancellationToken);
        var target = await users.GetByTelegramUserIdAsync(command.TargetTelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("The target Telegram user has not registered yet.");
        if (target.Role == PlatformRole.Owner && command.Role != PlatformRole.Owner)
        {
            throw new InvalidOperationException("The owner role cannot be removed through this endpoint.");
        }

        // The owner comes only from the deployment settings (Administration:TelegramUserIds): granted here it could
        // never be taken back or blocked, so one wrong tap would hand out permanent full access.
        if (command.Role == PlatformRole.Owner && target.Role != PlatformRole.Owner)
        {
            throw new InvalidOperationException("The owner role cannot be granted through this endpoint.");
        }
        target.SetRole(command.Role);
        await auditLogs.AddAsync(AuditLog.Create(actor.TelegramUserId, "platform-user.role-updated", "PlatformUser", target.TelegramUserId.ToString(), command.Role.ToString()), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AdminPlatformUserDto.From(target);
    }

    internal static async Task<PlatformUser> RequireOwnerAsync(IPlatformUserRepository users, long telegramUserId, CancellationToken cancellationToken) =>
        await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken) is { Role: PlatformRole.Owner, IsBlocked: false, IsDeleted: false } user
            ? user
            : throw new UnauthorizedAccessException("Only the owner can manage platform roles.");
}

public sealed class SetPlatformUserBlockedHandler(IPlatformUserRepository users, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork, ICatalogCache? cache = null)
    : IRequestHandler<SetPlatformUserBlockedCommand, AdminPlatformUserDto>
{
    public async Task<AdminPlatformUserDto> Handle(SetPlatformUserBlockedCommand command, CancellationToken cancellationToken)
    {
        var actor = await UpdatePlatformUserRoleHandler.RequireOwnerAsync(users, command.ActorTelegramUserId, cancellationToken);
        var target = await users.GetByTelegramUserIdAsync(command.TargetTelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("The target Telegram user has not registered yet.");
        if (target.Role == PlatformRole.Owner)
        {
            throw new InvalidOperationException("The owner account cannot be blocked.");
        }
        target.SetBlocked(command.IsBlocked);
        await auditLogs.AddAsync(AuditLog.Create(actor.TelegramUserId, command.IsBlocked ? "platform-user.blocked" : "platform-user.unblocked", "PlatformUser", target.TelegramUserId.ToString()), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (cache is not null)
        {
            await cache.RotateNamespaceVersionAsync(cancellationToken);
            await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
        }
        return AdminPlatformUserDto.From(target);
    }
}
