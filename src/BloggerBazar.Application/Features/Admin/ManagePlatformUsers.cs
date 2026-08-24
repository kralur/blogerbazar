using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using MediatR;

namespace BloggerBazar.Application.Features.Admin;

public sealed record AdminPlatformUserDto(long TelegramUserId, string FirstName, string? Username, PlatformRole Role, bool IsBlocked, bool IsDeleted, DateTime? DeletedAtUtc, long? DeletedByTelegramUserId, DateTime CreatedAtUtc)
{
    public static AdminPlatformUserDto From(PlatformUser user) => new(user.TelegramUserId, user.FirstName, user.Username, user.Role, user.IsBlocked, user.IsDeleted, user.DeletedAtUtc, user.DeletedByTelegramUserId, user.CreatedAtUtc);
}

public sealed record AuditLogDto(Guid Id, long ActorTelegramUserId, string Action, string TargetType, string TargetId, string? Details, string? CorrelationId, DateTime CreatedAtUtc)
{
    public static AuditLogDto From(AuditLog entry) => new(entry.Id, entry.ActorTelegramUserId, entry.Action, entry.TargetType, entry.TargetId, entry.Details, entry.CorrelationId, entry.CreatedAtUtc);
}

public sealed record GetAdminUsersQuery(long ActorTelegramUserId, int Take = 100) : IRequest<IReadOnlyList<AdminPlatformUserDto>>;
public sealed record GetAuditLogQuery(long ActorTelegramUserId, int Take = 100) : IRequest<IReadOnlyList<AuditLogDto>>;
public sealed record UpdatePlatformUserRoleCommand(long ActorTelegramUserId, long TargetTelegramUserId, PlatformRole Role) : IRequest<AdminPlatformUserDto>;
public sealed record SetPlatformUserBlockedCommand(long ActorTelegramUserId, long TargetTelegramUserId, bool IsBlocked) : IRequest<AdminPlatformUserDto>;

public sealed class GetAdminUsersHandler(IPlatformUserRepository users, BloggerBazar.Application.Abstractions.Security.IAdminAccessPolicy access)
    : IRequestHandler<GetAdminUsersQuery, IReadOnlyList<AdminPlatformUserDto>>
{
    public async Task<IReadOnlyList<AdminPlatformUserDto>> Handle(GetAdminUsersQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        return (await users.GetAllAsync(Math.Clamp(query.Take, 1, 200), cancellationToken)).Select(AdminPlatformUserDto.From).ToArray();
    }
}

public sealed class GetAuditLogHandler(IAuditLogRepository auditLogs, BloggerBazar.Application.Abstractions.Security.IAdminAccessPolicy access)
    : IRequestHandler<GetAuditLogQuery, IReadOnlyList<AuditLogDto>>
{
    public async Task<IReadOnlyList<AuditLogDto>> Handle(GetAuditLogQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        return (await auditLogs.GetRecentAsync(Math.Clamp(query.Take, 1, 200), cancellationToken)).Select(AuditLogDto.From).ToArray();
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
        if (cache is not null) await cache.RotateNamespaceVersionAsync(cancellationToken);
        return AdminPlatformUserDto.From(target);
    }
}
