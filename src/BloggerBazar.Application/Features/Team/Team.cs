using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Businesses;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Team;

// D64 stage C: a company's team. The owner (or an admin) makes a one-time link for a role; the first person to open it
// asks for access, and the owner or an admin sees their name and number and accepts or declines. Rights per role:
// docs/business-teams.md. The team works only in a company "with a team" (corporate mode); free: the owner + 2.

public static class TeamCodes
{
    public const string TeamModeOff = "team_mode_off";
    public const string TeamFull = "team_full";
    public const string TeamNotEmpty = "team_not_empty";
    public const string InvitationUsed = "team_invitation_used";
    public const string AlreadyInTeam = "team_already_member";
    public const string PhoneRequired = "phone_not_verified";
    public const string TooManyInvitations = "team_too_many_invitations";
}

public static class TeamLimits
{
    // The owner and two people for free (owner's answer 3, 2026-11-09); more comes with the corporate plan (stage D).
    public const int FreeSeats = 3;
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromHours(48);
    // Links and requests not yet decided; a cap keeps a company from piling them up.
    public const int MaxPendingInvitations = 10;
}

// D26 strings for roles and invitation states.
public static class TeamRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Manager = "manager";
    public const string Viewer = "viewer";

    public static string ToApi(BusinessMemberRole role) => role switch
    {
        BusinessMemberRole.Owner => Owner,
        BusinessMemberRole.Admin => Admin,
        BusinessMemberRole.Manager => Manager,
        _ => Viewer
    };

    public static BusinessMemberRole? FromApi(string? role) => role switch
    {
        Admin => BusinessMemberRole.Admin,
        Manager => BusinessMemberRole.Manager,
        Viewer => BusinessMemberRole.Viewer,
        _ => null
    };
}

public static class TeamInvitationStates
{
    public const string Open = "open";
    public const string Requested = "requested";
    public const string RequestedByYou = "requestedByYou";
    // The person's own request was decided: let in or turned down.
    public const string Joined = "joined";
    public const string Declined = "declined";
    public const string Used = "used";
    public const string Expired = "expired";
}

public sealed record TeamMemberDto(Guid Id, string FirstName, string? Username, string Role, DateTime JoinedAtUtc, bool IsMe);
// Phone: only for those who decide (owner, admins).
public sealed record TeamRequestDto(Guid Id, string FirstName, string? Username, string? Phone, string Role, DateTime RequestedAtUtc);
public sealed record TeamInvitationDto(Guid Id, string Role, DateTime ExpiresAtUtc);
public sealed record TeamDto(
    Guid CompanyId,
    string CompanyName,
    bool TeamMode,
    string MyRole,
    bool CanManage,
    int Seats,
    int SeatsUsed,
    IReadOnlyList<TeamMemberDto> Members,
    IReadOnlyList<TeamRequestDto> Requests,
    IReadOnlyList<TeamInvitationDto> Invitations);
public sealed record TeamInvitationCreated(Guid Id, string Code, string Role, DateTime ExpiresAtUtc);
public sealed record TeamInvitationPreviewDto(string CompanyName, string? CompanyLogoUrl, string Role, string State);

public sealed record GetTeamQuery(long TelegramUserId) : IRequest<TeamDto?>;
public sealed record SetTeamModeCommand(long TelegramUserId, bool TeamMode) : IRequest<TeamDto>;
public sealed record CreateTeamInvitationCommand(long TelegramUserId, string Role) : IRequest<TeamInvitationCreated>;
public sealed record RevokeTeamInvitationCommand(long TelegramUserId, Guid InvitationId) : IRequest;
public sealed record PreviewTeamInvitationQuery(long TelegramUserId, string Code) : IRequest<TeamInvitationPreviewDto>;
public sealed record RequestTeamAccessCommand(long TelegramUserId, string Code) : IRequest<TeamInvitationPreviewDto>;
// SiteUrl: the address of the site for the welcome message (the Mini App's own address), when configured.
public sealed record AcceptTeamRequestCommand(long TelegramUserId, Guid InvitationId, string? SiteUrl = null) : IRequest<TeamDto>;
public sealed record DeclineTeamRequestCommand(long TelegramUserId, Guid InvitationId) : IRequest<TeamDto>;
public sealed record ChangeTeamRoleCommand(long TelegramUserId, Guid MemberId, string Role) : IRequest<TeamDto>;
public sealed record RemoveTeamMemberCommand(long TelegramUserId, Guid MemberId) : IRequest<TeamDto>;
public sealed record LeaveTeamCommand(long TelegramUserId) : IRequest;
public sealed record SendWorkOnComputerLinkCommand(long TelegramUserId, string SiteUrl) : IRequest;

public sealed class CreateTeamInvitationValidator : AbstractValidator<CreateTeamInvitationCommand>
{
    public CreateTeamInvitationValidator() => RuleFor(command => command.Role).Must(role => TeamRoles.FromApi(role) is not null);
}

public sealed class ChangeTeamRoleValidator : AbstractValidator<ChangeTeamRoleCommand>
{
    public ChangeTeamRoleValidator() => RuleFor(command => command.Role).Must(role => TeamRoles.FromApi(role) is not null);
}

public sealed class TeamCodeValidator : AbstractValidator<RequestTeamAccessCommand>
{
    public TeamCodeValidator() => RuleFor(command => command.Code).NotEmpty().MaximumLength(64);
}

public sealed class TeamPreviewCodeValidator : AbstractValidator<PreviewTeamInvitationQuery>
{
    public TeamPreviewCodeValidator() => RuleFor(query => query.Code).NotEmpty().MaximumLength(64);
}

internal static class TeamAccess
{
    // The caller's company and role; 404 when they are in no company or work as another role now (rule 1),
    // 403 when their role in the team does not allow the action.
    public static async Task<BusinessMembership> RequireAsync(
        IPlatformUserRepository users,
        IBusinessProfileRepository businesses,
        long telegramUserId,
        BusinessPermission permission,
        CancellationToken cancellationToken)
    {
        var membership = await FindAsync(users, businesses, telegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Company was not found.");
        return membership.Allows(permission) ? membership : throw new UnauthorizedAccessException("The caller's role in the company does not allow this action.");
    }

    public static async Task<BusinessMembership?> FindAsync(IPlatformUserRepository users, IBusinessProfileRepository businesses, long telegramUserId, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted || user.SelectedMarketplaceRole != MarketplaceRole.Business)
        {
            return null;
        }

        return await businesses.GetMembershipAsync(telegramUserId, cancellationToken);
    }

    // Admins look after managers and viewers; only the owner makes, changes or removes admins.
    public static void EnsureMayHandle(BusinessMembership caller, BusinessMemberRole role)
    {
        if (role == BusinessMemberRole.Owner || (role == BusinessMemberRole.Admin && caller.Role != BusinessMemberRole.Owner))
        {
            throw new UnauthorizedAccessException("Only the owner manages admins.");
        }
    }

    public static void EnsureTeamMode(BusinessProfile company)
    {
        if (!company.TeamMode)
        {
            throw new BusinessRuleConflictException(TeamCodes.TeamModeOff, "Switch the company to working with a team first.");
        }
    }

    public static string HashCode(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();

    public static async Task<TeamDto> ViewAsync(IBusinessTeamRepository team, BusinessMembership membership, long telegramUserId, CancellationToken cancellationToken)
    {
        var company = membership.Business;
        var canManage = membership.Allows(BusinessPermission.ManageTeam);
        var members = await team.GetMembersAsync(company.Id, cancellationToken);
        var pending = canManage ? await team.GetPendingInvitationsAsync(company.Id, DateTime.UtcNow, cancellationToken) : [];
        return new TeamDto(
            company.Id,
            company.Name,
            company.TeamMode,
            TeamRoles.ToApi(membership.Role),
            canManage,
            TeamLimits.FreeSeats,
            members.Count,
            [.. members.Select(member => new TeamMemberDto(member.Id, member.FirstName, member.Username, TeamRoles.ToApi(member.Role), member.JoinedAtUtc, member.TelegramUserId == telegramUserId))],
            [.. pending.Where(row => row.Status == BusinessInvitationStatus.Requested).Select(row => new TeamRequestDto(
                row.Id, row.RequesterFirstName ?? "", row.RequesterUsername, row.RequesterPhone, TeamRoles.ToApi(row.Role), row.RequestedAtUtc ?? row.CreatedAtUtc))],
            [.. pending.Where(row => row.Status == BusinessInvitationStatus.Open).Select(row => new TeamInvitationDto(row.Id, TeamRoles.ToApi(row.Role), row.ExpiresAtUtc))]);
    }
}

public sealed class GetTeamHandler(IPlatformUserRepository users, IBusinessProfileRepository businesses, IBusinessTeamRepository team) : IRequestHandler<GetTeamQuery, TeamDto?>
{
    public async Task<TeamDto?> Handle(GetTeamQuery query, CancellationToken cancellationToken)
    {
        var membership = await TeamAccess.FindAsync(users, businesses, query.TelegramUserId, cancellationToken);
        return membership is null ? null : await TeamAccess.ViewAsync(team, membership, query.TelegramUserId, cancellationToken);
    }
}

// "Alone" ⇄ "with a team": only the owner. Back to "alone" only without other people; open links are closed.
public sealed class SetTeamModeHandler(IPlatformUserRepository users, IBusinessProfileRepository businesses, IBusinessTeamRepository team, IUnitOfWork unitOfWork) : IRequestHandler<SetTeamModeCommand, TeamDto>
{
    public async Task<TeamDto> Handle(SetTeamModeCommand command, CancellationToken cancellationToken)
    {
        var membership = await TeamAccess.RequireAsync(users, businesses, command.TelegramUserId, BusinessPermission.Own, cancellationToken);
        var company = membership.Business;
        if (!command.TeamMode)
        {
            if (await team.CountActiveMembersAsync(company.Id, cancellationToken) > 1)
            {
                throw new BusinessRuleConflictException(TeamCodes.TeamNotEmpty, "Remove the team before working alone.");
            }

            await team.CloseInvitationsAsync(company.Id, command.TelegramUserId, DateTime.UtcNow, cancellationToken);
        }

        company.SetTeamMode(command.TeamMode);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await TeamAccess.ViewAsync(team, membership, command.TelegramUserId, cancellationToken);
    }
}

public sealed class CreateTeamInvitationHandler(IPlatformUserRepository users, IBusinessProfileRepository businesses, IBusinessTeamRepository team, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateTeamInvitationCommand, TeamInvitationCreated>
{
    public async Task<TeamInvitationCreated> Handle(CreateTeamInvitationCommand command, CancellationToken cancellationToken)
    {
        var membership = await TeamAccess.RequireAsync(users, businesses, command.TelegramUserId, BusinessPermission.ManageTeam, cancellationToken);
        var role = TeamRoles.FromApi(command.Role) ?? throw new InvalidOperationException("Unknown role.");
        TeamAccess.EnsureMayHandle(membership, role);
        TeamAccess.EnsureTeamMode(membership.Business);
        if (await team.CountActiveMembersAsync(membership.Business.Id, cancellationToken) >= TeamLimits.FreeSeats)
        {
            throw new BusinessRuleConflictException(TeamCodes.TeamFull, "The team has no free places.");
        }

        if ((await team.GetPendingInvitationsAsync(membership.Business.Id, DateTime.UtcNow, cancellationToken)).Count >= TeamLimits.MaxPendingInvitations)
        {
            throw new BusinessRuleConflictException(TeamCodes.TooManyInvitations, "Too many links wait for an answer.");
        }

        var code = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
        var invitation = BusinessInvitation.Create(membership.Business.Id, TeamAccess.HashCode(code), role, command.TelegramUserId, DateTime.UtcNow, TeamLimits.InvitationLifetime);
        await team.AddInvitationAsync(invitation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new TeamInvitationCreated(invitation.Id, code, TeamRoles.ToApi(role), invitation.ExpiresAtUtc);
    }
}

public sealed class RevokeTeamInvitationHandler(IPlatformUserRepository users, IBusinessProfileRepository businesses, IBusinessTeamRepository team, IUnitOfWork unitOfWork) : IRequestHandler<RevokeTeamInvitationCommand>
{
    public async Task Handle(RevokeTeamInvitationCommand command, CancellationToken cancellationToken)
    {
        var membership = await TeamAccess.RequireAsync(users, businesses, command.TelegramUserId, BusinessPermission.ManageTeam, cancellationToken);
        var invitation = await team.GetInvitationAsync(membership.Business.Id, command.InvitationId, cancellationToken)
            ?? throw new InvalidOperationException("Invitation was not found.");
        TeamAccess.EnsureMayHandle(membership, invitation.Role);
        invitation.Revoke(command.TelegramUserId, DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// What the person who opened the link sees: the company, the role, and whether the link can still be used.
public sealed class PreviewTeamInvitationHandler(IBusinessTeamRepository team) : IRequestHandler<PreviewTeamInvitationQuery, TeamInvitationPreviewDto>
{
    public async Task<TeamInvitationPreviewDto> Handle(PreviewTeamInvitationQuery query, CancellationToken cancellationToken)
    {
        var invitation = await team.GetInvitationByCodeHashAsync(TeamAccess.HashCode(query.Code), cancellationToken)
            ?? throw new InvalidOperationException("Invitation was not found.");
        return TeamInvitationPreview.Of(invitation, query.TelegramUserId, DateTime.UtcNow);
    }
}

internal static class TeamInvitationPreview
{
    public static TeamInvitationPreviewDto Of(BusinessInvitation invitation, long telegramUserId, DateTime nowUtc)
    {
        var state = invitation.Status switch
        {
            BusinessInvitationStatus.Open when invitation.ExpiresAtUtc > nowUtc => TeamInvitationStates.Open,
            BusinessInvitationStatus.Open => TeamInvitationStates.Expired,
            BusinessInvitationStatus.Requested when invitation.RequestedByTelegramUserId == telegramUserId => TeamInvitationStates.RequestedByYou,
            BusinessInvitationStatus.Accepted when invitation.RequestedByTelegramUserId == telegramUserId => TeamInvitationStates.Joined,
            BusinessInvitationStatus.Declined when invitation.RequestedByTelegramUserId == telegramUserId => TeamInvitationStates.Declined,
            _ => TeamInvitationStates.Used
        };
        return new TeamInvitationPreviewDto(invitation.Business.Name, invitation.Business.LogoUrl, TeamRoles.ToApi(invitation.Role), state);
    }
}

// The person asks for access with the link: the link becomes theirs, the owner and admins hear about it.
public sealed class RequestTeamAccessHandler(
    IBusinessTeamRepository team,
    IBusinessProfileRepository businesses,
    IPlatformUserRepository users,
    ITelegramBotClient? botClient = null,
    ILogger<RequestTeamAccessHandler>? logger = null) : IRequestHandler<RequestTeamAccessCommand, TeamInvitationPreviewDto>
{
    public async Task<TeamInvitationPreviewDto> Handle(RequestTeamAccessCommand command, CancellationToken cancellationToken)
    {
        var invitation = await team.GetInvitationByCodeHashAsync(TeamAccess.HashCode(command.Code), cancellationToken)
            ?? throw new InvalidOperationException("Invitation was not found.");
        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Invitation was not found.");
        // D41: the owner sees the number Telegram confirmed, so it must be there first.
        if (string.IsNullOrWhiteSpace(user.VerifiedPhone))
        {
            throw new BusinessRuleConflictException(TeamCodes.PhoneRequired, "Share the phone number from Telegram first.");
        }

        // One person, one company (D64): a member or owner of a company does not join another one.
        if (await businesses.GetMembershipAsync(command.TelegramUserId, cancellationToken) is { } current)
        {
            throw new BusinessRuleConflictException(
                current.Business.Id == invitation.BusinessId ? TeamCodes.AlreadyInTeam : BusinessTeamCodes.MemberOfAnotherCompany,
                "The person already works for a company.");
        }

        var nowUtc = DateTime.UtcNow;
        // Asking again changes nothing.
        if (TeamInvitationPreview.Of(invitation, command.TelegramUserId, nowUtc) is { State: TeamInvitationStates.RequestedByYou } same)
        {
            return same;
        }

        if (!invitation.IsOpenAt(nowUtc) || !await team.TryRequestAsync(invitation.Id, command.TelegramUserId, nowUtc, cancellationToken))
        {
            throw new BusinessRuleConflictException(TeamCodes.InvitationUsed, "This invitation can no longer be used.");
        }

        var text = BotMessages.TeamAccessRequested(user.FirstName, user.Username, user.VerifiedPhone!, invitation.Business.Name, invitation.Role);
        foreach (var decider in await team.GetDecidersAsync(invitation.BusinessId, cancellationToken))
        {
            await BestEffortTelegramNotification.SendAsync(botClient, logger, decider, text, "/team", cancellationToken);
        }

        return new TeamInvitationPreviewDto(invitation.Business.Name, invitation.Business.LogoUrl, TeamRoles.ToApi(invitation.Role), TeamInvitationStates.RequestedByYou);
    }
}

public sealed class AcceptTeamRequestHandler(
    IBusinessProfileRepository businesses,
    IBusinessTeamRepository team,
    IPlatformUserRepository users,
    IUnitOfWork unitOfWork,
    ITelegramBotClient? botClient = null,
    ILogger<AcceptTeamRequestHandler>? logger = null) : IRequestHandler<AcceptTeamRequestCommand, TeamDto>
{
    public async Task<TeamDto> Handle(AcceptTeamRequestCommand command, CancellationToken cancellationToken)
    {
        var membership = await TeamAccess.RequireAsync(users, businesses, command.TelegramUserId, BusinessPermission.ManageTeam, cancellationToken);
        var company = membership.Business;
        var invitation = await team.GetInvitationAsync(company.Id, command.InvitationId, cancellationToken)
            ?? throw new InvalidOperationException("Request was not found.");
        TeamAccess.EnsureMayHandle(membership, invitation.Role);
        TeamAccess.EnsureTeamMode(company);
        if (invitation.Status != BusinessInvitationStatus.Requested || invitation.RequestedByTelegramUserId is not { } newcomerId)
        {
            throw new InvalidOperationException("Request was not found.");
        }

        if (await team.CountActiveMembersAsync(company.Id, cancellationToken) >= TeamLimits.FreeSeats)
        {
            throw new BusinessRuleConflictException(TeamCodes.TeamFull, "The team has no free places.");
        }

        var nowUtc = DateTime.UtcNow;
        var newcomer = await users.GetByTelegramUserIdAsync(newcomerId, cancellationToken);
        if (newcomer is null || newcomer.IsBlocked || newcomer.IsDeleted || await businesses.GetMembershipAsync(newcomerId, cancellationToken) is not null)
        {
            // They joined another company or left meanwhile: the request cannot be accepted any more.
            invitation.Decline(command.TelegramUserId, nowUtc);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new BusinessRuleConflictException(BusinessTeamCodes.MemberOfAnotherCompany, "The person can no longer join this company.");
        }

        invitation.Accept(command.TelegramUserId, nowUtc);
        await team.AddMemberAsync(BusinessMember.Join(company.Id, newcomerId, invitation.Role, command.TelegramUserId, nowUtc), cancellationToken);
        // They asked to work for the company: their app opens as the company next time (rule 1, selected role).
        newcomer.SelectMarketplaceRole(MarketplaceRole.Business);
        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            // The unique index on active memberships: they joined another company in the same instant.
            throw new BusinessRuleConflictException(BusinessTeamCodes.MemberOfAnotherCompany, "The person can no longer join this company.");
        }

        await BestEffortTelegramNotification.SendAsync(botClient, logger, newcomerId, BotMessages.TeamJoined(company.Name, invitation.Role, command.SiteUrl), "/", cancellationToken);
        // The owner hears about every new person an admin lets in: access to the company is theirs to watch.
        if (company.TelegramUserId != command.TelegramUserId)
        {
            await BestEffortTelegramNotification.SendAsync(botClient, logger, company.TelegramUserId, BotMessages.TeamMemberAdded(newcomer.FirstName, company.Name, invitation.Role), "/team", cancellationToken);
        }

        return await TeamAccess.ViewAsync(team, membership, command.TelegramUserId, cancellationToken);
    }
}

public sealed class DeclineTeamRequestHandler(
    IPlatformUserRepository users,
    IBusinessProfileRepository businesses,
    IBusinessTeamRepository team,
    IUnitOfWork unitOfWork,
    ITelegramBotClient? botClient = null,
    ILogger<DeclineTeamRequestHandler>? logger = null) : IRequestHandler<DeclineTeamRequestCommand, TeamDto>
{
    public async Task<TeamDto> Handle(DeclineTeamRequestCommand command, CancellationToken cancellationToken)
    {
        var membership = await TeamAccess.RequireAsync(users, businesses, command.TelegramUserId, BusinessPermission.ManageTeam, cancellationToken);
        var invitation = await team.GetInvitationAsync(membership.Business.Id, command.InvitationId, cancellationToken)
            ?? throw new InvalidOperationException("Request was not found.");
        TeamAccess.EnsureMayHandle(membership, invitation.Role);
        if (!invitation.Decline(command.TelegramUserId, DateTime.UtcNow))
        {
            throw new InvalidOperationException("Request was not found.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (invitation.RequestedByTelegramUserId is { } requester)
        {
            await BestEffortTelegramNotification.SendAsync(botClient, logger, requester, BotMessages.TeamRequestDeclined(membership.Business.Name), cancellationToken);
        }

        return await TeamAccess.ViewAsync(team, membership, command.TelegramUserId, cancellationToken);
    }
}

public sealed class ChangeTeamRoleHandler(IPlatformUserRepository users, IBusinessProfileRepository businesses, IBusinessTeamRepository team, IUnitOfWork unitOfWork) : IRequestHandler<ChangeTeamRoleCommand, TeamDto>
{
    public async Task<TeamDto> Handle(ChangeTeamRoleCommand command, CancellationToken cancellationToken)
    {
        var membership = await TeamAccess.RequireAsync(users, businesses, command.TelegramUserId, BusinessPermission.ManageTeam, cancellationToken);
        var member = await team.GetActiveMemberAsync(membership.Business.Id, command.MemberId, cancellationToken)
            ?? throw new InvalidOperationException("Team member was not found.");
        var role = TeamRoles.FromApi(command.Role) ?? throw new InvalidOperationException("Unknown role.");
        TeamAccess.EnsureMayHandle(membership, member.Role);
        TeamAccess.EnsureMayHandle(membership, role);
        member.ChangeRole(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await TeamAccess.ViewAsync(team, membership, command.TelegramUserId, cancellationToken);
    }
}

// Removed at once: the next request from their phone or browser finds no company, and their browsers are signed out.
public sealed class RemoveTeamMemberHandler(
    IPlatformUserRepository users,
    IBusinessProfileRepository businesses,
    IBusinessTeamRepository team,
    IUnitOfWork unitOfWork,
    IBrowserSessionStore? browsers = null,
    ITelegramBotClient? botClient = null,
    ILogger<RemoveTeamMemberHandler>? logger = null) : IRequestHandler<RemoveTeamMemberCommand, TeamDto>
{
    public async Task<TeamDto> Handle(RemoveTeamMemberCommand command, CancellationToken cancellationToken)
    {
        var membership = await TeamAccess.RequireAsync(users, businesses, command.TelegramUserId, BusinessPermission.ManageTeam, cancellationToken);
        var member = await team.GetActiveMemberAsync(membership.Business.Id, command.MemberId, cancellationToken)
            ?? throw new InvalidOperationException("Team member was not found.");
        TeamAccess.EnsureMayHandle(membership, member.Role);
        member.Remove(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (browsers is not null)
        {
            await browsers.RemoveAllSessionsAsync(member.TelegramUserId, cancellationToken);
        }

        await BestEffortTelegramNotification.SendAsync(botClient, logger, member.TelegramUserId, BotMessages.TeamMemberRemoved(membership.Business.Name), cancellationToken);
        return await TeamAccess.ViewAsync(team, membership, command.TelegramUserId, cancellationToken);
    }
}

public sealed class LeaveTeamHandler(
    IPlatformUserRepository users,
    IBusinessProfileRepository businesses,
    IBusinessTeamRepository team,
    IUnitOfWork unitOfWork,
    IBrowserSessionStore? browsers = null) : IRequestHandler<LeaveTeamCommand>
{
    public async Task Handle(LeaveTeamCommand command, CancellationToken cancellationToken)
    {
        await TeamAccess.RequireAsync(users, businesses, command.TelegramUserId, BusinessPermission.View, cancellationToken);
        var member = await team.GetActiveMembershipAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Company was not found.");
        if (member.Role == BusinessMemberRole.Owner)
        {
            throw new UnauthorizedAccessException("The owner does not leave the company; it is transferred or deleted.");
        }

        member.Remove(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (browsers is not null)
        {
            await browsers.RemoveAllSessionsAsync(command.TelegramUserId, cancellationToken);
        }
    }
}

// "Work on a computer": the bot sends the site's address into the person's chat, where Telegram on the computer shows it.
public sealed class SendWorkOnComputerLinkHandler(
    IPlatformUserRepository users,
    IBusinessProfileRepository businesses,
    ITelegramBotClient? botClient = null,
    ILogger<SendWorkOnComputerLinkHandler>? logger = null) : IRequestHandler<SendWorkOnComputerLinkCommand>
{
    public async Task Handle(SendWorkOnComputerLinkCommand command, CancellationToken cancellationToken)
    {
        var membership = await TeamAccess.RequireAsync(users, businesses, command.TelegramUserId, BusinessPermission.View, cancellationToken);
        await BestEffortTelegramNotification.SendAsync(botClient, logger, command.TelegramUserId, BotMessages.WorkOnComputer(membership.Business.Name, command.SiteUrl), cancellationToken);
    }
}
