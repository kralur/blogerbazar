using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Complaints;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Complaints;

public sealed class ComplaintHandlersTests
{
    private const long BloggerTelegramId = 11;
    private const long BusinessTelegramId = 22;
    private const long StrangerTelegramId = 33;

    [Fact]
    public async Task Business_reports_a_blogger_profile()
    {
        var world = new World();

        var result = await world.Report(BusinessTelegramId, ComplaintTargets.Blogger, world.Blogger.Id, "fake");

        Assert.False(result.AlreadyReported);
        Assert.Equal(ComplaintStatuses.Open, result.Status);
        var saved = Assert.Single(world.Complaints.Items);
        Assert.Equal(MarketplaceRole.Business, saved.ReporterRole);
        Assert.Equal("fake", saved.Reason);
    }

    [Fact]
    public async Task Reporting_the_same_target_again_keeps_one_open_case()
    {
        var world = new World();
        var first = await world.Report(BusinessTelegramId, ComplaintTargets.Blogger, world.Blogger.Id, "spam");

        var second = await world.Report(BusinessTelegramId, ComplaintTargets.Blogger, world.Blogger.Id, "fraud");

        Assert.True(second.AlreadyReported);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(world.Complaints.Items);
    }

    [Fact]
    public async Task Own_profile_cannot_be_reported()
    {
        var world = new World();

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => world.Report(BloggerTelegramId, ComplaintTargets.Blogger, world.Blogger.Id, "spam"));
        Assert.Equal(ComplaintCodes.Own, error.Code);
    }

    [Fact]
    public async Task Own_campaign_cannot_be_reported_but_another_one_can()
    {
        var world = new World();

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => world.Report(BusinessTelegramId, ComplaintTargets.Campaign, world.Campaign.Id, "spam"));
        Assert.Equal(ComplaintCodes.Own, error.Code);
        var fromBlogger = await world.Report(BloggerTelegramId, ComplaintTargets.Campaign, world.Campaign.Id, "spam");
        Assert.False(fromBlogger.AlreadyReported);
    }

    [Fact]
    public async Task Missing_target_is_not_found()
    {
        var world = new World();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => world.Report(BusinessTelegramId, ComplaintTargets.BrandFace, Guid.NewGuid(), "spam"));
        Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_deal_is_reported_only_by_its_participants()
    {
        var world = new World();

        var participant = await world.Report(BloggerTelegramId, ComplaintTargets.Deal, world.Deal.Id, "noShow");
        Assert.False(participant.AlreadyReported);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => world.Report(StrangerTelegramId, ComplaintTargets.Deal, world.Deal.Id, "noShow"));
        Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Daily_limit_stops_a_flood()
    {
        var world = new World();
        for (var index = 0; index < CreateComplaintHandler.DailyLimit; index++)
        {
            world.Complaints.Items.Add(Complaint.Create(BusinessTelegramId, MarketplaceRole.Business, ComplaintTargets.Blogger, Guid.NewGuid(), "spam", null, DateTime.UtcNow));
        }

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => world.Report(BusinessTelegramId, ComplaintTargets.Blogger, world.Blogger.Id, "spam"));
        Assert.Equal(ComplaintCodes.DailyLimit, error.Code);
    }

    [Fact]
    public async Task Blocked_or_role_less_account_cannot_report()
    {
        var world = new World();
        world.BusinessUser.SetBlocked(true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => world.Report(BusinessTelegramId, ComplaintTargets.Blogger, world.Blogger.Id, "spam"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => world.Report(StrangerTelegramId + 1, ComplaintTargets.Blogger, world.Blogger.Id, "spam"));
    }

    [Fact]
    public async Task Validator_requires_known_values_and_words_for_other()
    {
        var validator = new CreateComplaintValidator();
        var target = Guid.NewGuid();

        Assert.True((await validator.ValidateAsync(new CreateComplaintCommand(1, "blogger", target, "spam", null))).IsValid);
        Assert.False((await validator.ValidateAsync(new CreateComplaintCommand(1, "user", target, "spam", null))).IsValid);
        Assert.False((await validator.ValidateAsync(new CreateComplaintCommand(1, "blogger", target, "Spam", null))).IsValid);
        Assert.False((await validator.ValidateAsync(new CreateComplaintCommand(1, "blogger", target, "other", "  "))).IsValid);
        Assert.True((await validator.ValidateAsync(new CreateComplaintCommand(1, "blogger", target, "other", "Asks for money upfront"))).IsValid);
    }

    [Fact]
    public async Task Admin_closes_an_open_complaint_once_and_it_is_logged()
    {
        var complaint = Complaint.Create(BusinessTelegramId, MarketplaceRole.Business, ComplaintTargets.Blogger, Guid.NewGuid(), "spam", null, DateTime.UtcNow);
        var complaints = new FakeComplaints { Items = { complaint } };
        var audit = new FakeAuditLogs();
        var handler = new CloseComplaintHandler(complaints, new AllowAdmin(), audit, new SpyUnitOfWork());

        var result = await handler.Handle(new CloseComplaintCommand(1, complaint.Id, ComplaintStatuses.Resolved, "Warned the user"), CancellationToken.None);

        Assert.Equal(ComplaintStatuses.Resolved, result.Status);
        Assert.Equal("Warned the user", complaint.ResolutionNote);
        Assert.Equal("complaint.resolved", Assert.Single(audit.Entries).Action);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CloseComplaintCommand(1, complaint.Id, ComplaintStatuses.Dismissed, null), CancellationToken.None));
    }

    [Fact]
    public async Task Non_admin_cannot_see_or_close_complaints()
    {
        var complaints = new FakeComplaints();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetAdminComplaintsHandler(complaints, new DenyAdmin()).Handle(new GetAdminComplaintsQuery(5), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new CloseComplaintHandler(complaints, new DenyAdmin(), new FakeAuditLogs(), new SpyUnitOfWork())
            .Handle(new CloseComplaintCommand(5, Guid.NewGuid(), ComplaintStatuses.Resolved, null), CancellationToken.None));
    }

    private sealed class World
    {
        public World()
        {
            Blogger = DealTestData.Blogger(BloggerTelegramId);
            Business = DealTestData.Business(BusinessTelegramId);
            Campaign = Campaign.Create(Business.Id, "Campaign", "Description", ["beauty"], ["Reel"], 100, 200, "tashkent", null);
            Deal = CampaignDeal(Blogger, Business);
            BusinessUser = User(BusinessTelegramId, MarketplaceRole.Business);
            Users = new FakeUsers(User(BloggerTelegramId, MarketplaceRole.Blogger), BusinessUser, User(StrangerTelegramId, MarketplaceRole.Blogger), User(StrangerTelegramId + 1, null));
        }

        public BloggerProfile Blogger { get; }
        public BusinessProfile Business { get; }
        public Campaign Campaign { get; }
        public Deal Deal { get; }
        public PlatformUser BusinessUser { get; }
        public FakeUsers Users { get; }
        public FakeComplaints Complaints { get; } = new();

        public Task<ComplaintDto> Report(long telegramUserId, string targetType, Guid targetId, string reason) =>
            new CreateComplaintHandler(Complaints, Users, new FakeBloggers(Blogger, DealTestData.Blogger(StrangerTelegramId)), new FakeBusinesses(Business), new FakeBrandFaces(),
                    new Campaigns(Campaign), new FakeDeals(Deal), new NoOffers(), new SpyUnitOfWork())
                .Handle(new CreateComplaintCommand(telegramUserId, targetType, targetId, reason, null), CancellationToken.None);
    }

    private sealed class FakeComplaints : IComplaintRepository
    {
        public List<Complaint> Items { get; init; } = [];
        public Task<Complaint?> FindOpenAsync(long reporterTelegramUserId, string targetType, Guid targetId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.ReporterTelegramUserId == reporterTelegramUserId && item.TargetType == targetType && item.TargetId == targetId && item.Status == ComplaintStatuses.Open));
        public Task<int> CountSinceAsync(long reporterTelegramUserId, DateTime sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Count(item => item.ReporterTelegramUserId == reporterTelegramUserId && item.CreatedAtUtc >= sinceUtc));
        public Task<Complaint?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task AddAsync(Complaint complaint, CancellationToken cancellationToken) { Items.Add(complaint); return Task.CompletedTask; }
        public Task<IReadOnlyList<AdminComplaintDto>> ListForAdminAsync(string? status, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AdminComplaintDto>>([]);
        public Task<int> CountOpenAsync(CancellationToken cancellationToken) => Task.FromResult(Items.Count(item => item.Status == ComplaintStatuses.Open));
    }

    private sealed class Campaigns(params Campaign[] items) : ICampaignRepository
    {
        public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(items.SingleOrDefault(item => item.Id == id));
        public Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken) => Task.FromResult(items.SingleOrDefault(item => item.Id == id && item.BusinessId == businessId));
        public Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Campaign>>(items);
        public Task AddAsync(Campaign campaign, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoOffers : ICollaborationRequestRepository
    {
        public Task<CollaborationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<CollaborationRequest?>(null);
        public Task<bool> ExistsDealAsync(Guid requestId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(CollaborationRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeAuditLogs : IAuditLogRepository
    {
        public List<AuditLog> Entries { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) { Entries.Add(entry); return Task.CompletedTask; }
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuditLog>>(Entries);
    }

    private sealed class AllowAdmin : IAdminAccessPolicy
    {
        public void EnsureAllowed(long telegramUserId) { }
    }

    private sealed class DenyAdmin : IAdminAccessPolicy
    {
        public void EnsureAllowed(long telegramUserId) => throw new UnauthorizedAccessException("Admin only.");
    }
}
