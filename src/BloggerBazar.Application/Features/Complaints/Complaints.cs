using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Complaints;

// D52: any role reports a profile, campaign, offer or deal; an admin resolves or dismisses it. No automatic blocking.
public static class ComplaintTargets
{
    public const string Blogger = "blogger";
    public const string BrandFace = "brandFace";
    public const string Business = "business";
    public const string Campaign = "campaign";
    public const string Offer = "offer";
    public const string Deal = "deal";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Blogger, BrandFace, Business, Campaign, Offer, Deal };
}

public static class ComplaintReasons
{
    public const string Other = "other";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { "spam", "fraud", "fake", "offensive", "noShow", Other };
}

public static class ComplaintStatuses
{
    public const string Open = Complaint.OpenStatus;
    public const string Resolved = "resolved";
    public const string Dismissed = "dismissed";
    public static readonly IReadOnlySet<string> Closing = new HashSet<string>(StringComparer.Ordinal) { Resolved, Dismissed };
}

public static class ComplaintCodes
{
    public const string Own = "complaint_own";
    public const string DailyLimit = "complaint_daily_limit";
}

public sealed record ComplaintDto(Guid Id, string Status, bool AlreadyReported);

public sealed record AdminComplaintDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string? TargetName,
    string Reason,
    string? Comment,
    string Status,
    long ReporterTelegramUserId,
    string? ReporterName,
    int ReporterRole,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    string? ResolutionNote,
    // Who answers for a profile or campaign, so the admin can block them from the case; null for offers and deals (two sides).
    long? OwnerTelegramUserId = null,
    bool OwnerIsBlocked = false);

public sealed record CreateComplaintCommand(long TelegramUserId, string TargetType, Guid TargetId, string Reason, string? Comment) : IRequest<ComplaintDto>;
public sealed record GetAdminComplaintsQuery(long ActorTelegramUserId, string? Status = ComplaintStatuses.Open, int Take = 100) : IRequest<IReadOnlyList<AdminComplaintDto>>;
public sealed record CloseComplaintCommand(long ActorTelegramUserId, Guid ComplaintId, string Status, string? Note) : IRequest<ComplaintDto>;

public sealed class CreateComplaintValidator : AbstractValidator<CreateComplaintCommand>
{
    public const int MaxCommentLength = 500;

    public CreateComplaintValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.TargetId).NotEmpty();
        RuleFor(command => command.TargetType).Must(ComplaintTargets.All.Contains);
        RuleFor(command => command.Reason).Must(ComplaintReasons.All.Contains);
        RuleFor(command => command.Comment).MaximumLength(MaxCommentLength);
        // "Other" says nothing by itself, so it needs a few words.
        RuleFor(command => command.Comment).Must(comment => comment?.Trim().Length >= 5).When(command => command.Reason == ComplaintReasons.Other);
    }
}

public sealed class CloseComplaintValidator : AbstractValidator<CloseComplaintCommand>
{
    public CloseComplaintValidator()
    {
        RuleFor(command => command.ComplaintId).NotEmpty();
        RuleFor(command => command.Status).Must(ComplaintStatuses.Closing.Contains);
        RuleFor(command => command.Note).MaximumLength(CreateComplaintValidator.MaxCommentLength);
    }
}

public sealed class CreateComplaintHandler(
    IComplaintRepository complaints,
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IBrandFaceProfileRepository brandFaces,
    ICampaignRepository campaigns,
    IDealRepository deals,
    ICollaborationRequestRepository offers,
    IUnitOfWork unitOfWork,
    ITelegramBotClient? botClient = null,
    ILogger<CreateComplaintHandler>? logger = null) : IRequestHandler<CreateComplaintCommand, ComplaintDto>
{
    public const int DailyLimit = 10;

    public async Task<ComplaintDto> Handle(CreateComplaintCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted || user.SelectedMarketplaceRole is not { } role)
        {
            throw new UnauthorizedAccessException("An active marketplace account is required.");
        }

        await EnsureTargetAsync(command, cancellationToken);

        // Reporting the same thing twice keeps one open case instead of a pile of duplicates.
        if (await complaints.FindOpenAsync(command.TelegramUserId, command.TargetType, command.TargetId, cancellationToken) is { } existing)
        {
            return new ComplaintDto(existing.Id, existing.Status, true);
        }

        var now = DateTime.UtcNow;
        if (await complaints.CountSinceAsync(command.TelegramUserId, now.AddDays(-1), cancellationToken) >= DailyLimit)
        {
            throw new BusinessRuleConflictException(ComplaintCodes.DailyLimit, "Daily complaint limit reached.");
        }

        var comment = string.IsNullOrWhiteSpace(command.Comment) ? null : command.Comment.Trim();
        var complaint = Complaint.Create(command.TelegramUserId, role, command.TargetType, command.TargetId, command.Reason, comment, now);
        await complaints.AddAsync(complaint, cancellationToken);
        // The partial unique index (one open case per reporter and target) settles a double tap.
        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            var winner = await complaints.FindOpenAsync(command.TelegramUserId, command.TargetType, command.TargetId, cancellationToken)
                ?? throw new InvalidOperationException("The complaint conflicts with another one.");
            return new ComplaintDto(winner.Id, winner.Status, true);
        }

        // Owners and admins hear about each new case in the bot chat; a failed message never fails the report.
        foreach (var adminId in (await users.GetAdminTelegramUserIdsAsync(cancellationToken)).Where(id => id != command.TelegramUserId))
        {
            await BestEffortTelegramNotification.SendAsync(botClient, logger, adminId, BotMessages.NewComplaint(complaint.TargetType, complaint.Reason), "/admin", cancellationToken);
        }

        return new ComplaintDto(complaint.Id, complaint.Status, false);
    }

    // Public things must exist and not be the reporter's own; private ones (offer, deal) only for their participants.
    private async Task EnsureTargetAsync(CreateComplaintCommand command, CancellationToken cancellationToken)
    {
        var reporter = command.TelegramUserId;
        long? ownerTelegramUserId = command.TargetType switch
        {
            ComplaintTargets.Blogger => await bloggers.GetByIdAsync(command.TargetId, cancellationToken) is { IsDeleted: false } blogger ? blogger.TelegramUserId : throw NotFound(),
            ComplaintTargets.BrandFace => await brandFaces.GetByIdAsync(command.TargetId, cancellationToken) is { IsDeleted: false } brandFace ? brandFace.TelegramUserId : throw NotFound(),
            ComplaintTargets.Business => await businesses.GetByIdAsync(command.TargetId, cancellationToken) is { IsDeleted: false } business ? business.TelegramUserId : throw NotFound(),
            ComplaintTargets.Campaign => await campaigns.GetByIdAsync(command.TargetId, cancellationToken) is { } campaign
                ? (await businesses.GetByIdAsync(campaign.BusinessId, cancellationToken))?.TelegramUserId
                : throw NotFound(),
            ComplaintTargets.Offer => await RequireOfferAsync(command, cancellationToken),
            ComplaintTargets.Deal => await RequireDealAsync(command, cancellationToken),
            _ => throw NotFound()
        };

        if (ownerTelegramUserId == reporter)
        {
            throw new BusinessRuleConflictException(ComplaintCodes.Own, "You cannot report your own profile or campaign.");
        }
    }

    private async Task<long?> RequireOfferAsync(CreateComplaintCommand command, CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, command.TelegramUserId, cancellationToken, brandFaces) ?? throw NotFound();
        _ = await offers.GetOfferForParticipantAsync(command.TargetId, participant.Role, participant.ProfileId, cancellationToken) ?? throw NotFound();
        return null;
    }

    private async Task<long?> RequireDealAsync(CreateComplaintCommand command, CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, command.TelegramUserId, cancellationToken, brandFaces) ?? throw NotFound();
        _ = await deals.GetForParticipantAsync(command.TargetId, participant.Role, participant.ProfileId, cancellationToken) ?? throw NotFound();
        return null;
    }

    private static InvalidOperationException NotFound() => new("Complaint target was not found.");
}

public sealed class GetAdminComplaintsHandler(IComplaintRepository complaints, IAdminAccessPolicy access)
    : IRequestHandler<GetAdminComplaintsQuery, IReadOnlyList<AdminComplaintDto>>
{
    public async Task<IReadOnlyList<AdminComplaintDto>> Handle(GetAdminComplaintsQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        var status = query.Status is { } value && (value == ComplaintStatuses.Open || ComplaintStatuses.Closing.Contains(value)) ? value : null;
        return await complaints.ListForAdminAsync(status, Math.Clamp(query.Take, 1, 200), cancellationToken);
    }
}

public sealed class CloseComplaintHandler(IComplaintRepository complaints, IAdminAccessPolicy access, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork)
    : IRequestHandler<CloseComplaintCommand, ComplaintDto>
{
    public async Task<ComplaintDto> Handle(CloseComplaintCommand command, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(command.ActorTelegramUserId);
        var complaint = await complaints.GetByIdAsync(command.ComplaintId, cancellationToken)
            ?? throw new InvalidOperationException("Complaint was not found.");
        var note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim();
        complaint.Close(command.Status, command.ActorTelegramUserId, note, DateTime.UtcNow);
        await auditLogs.AddAsync(AuditLog.Create(command.ActorTelegramUserId, $"complaint.{command.Status}", "Complaint", complaint.Id.ToString(), $"{complaint.TargetType}:{complaint.TargetId}"), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ComplaintDto(complaint.Id, complaint.Status, false);
    }
}
