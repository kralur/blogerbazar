using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.CollaborationRequests;
using BloggerBazar.Application.Features.Complaints;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Complaints;

public sealed class AdminComplaintReviewHandlerTests
{
    private static AdminComplaintDto Complaint(Guid id, string targetType, long? owner, long reporter = 55, string status = "open") =>
        new(id, targetType, Guid.NewGuid(), "Target", "fraud", null, status, reporter, "Reporter", 0, DateTime.UtcNow, null, null, owner);

    [Fact]
    public async Task Collects_the_accused_record_the_reporter_history_and_their_shared_deals()
    {
        var id = Guid.NewGuid();
        var other = Complaint(Guid.NewGuid(), "blogger", 77, reporter: 66);
        var complaints = new FakeComplaints(Complaint(id, "blogger", 77)) { ByTargets = [Complaint(id, "blogger", 77), other], ReporterCounts = new Dictionary<string, int> { ["open"] = 1, ["dismissed"] = 3 } };
        var review = new FakeReview { Targets = [Guid.NewGuid()], Shared = [new("deal", Guid.NewGuid(), "Launch", (int)DealStatus.Cancelled, DateTime.UtcNow)] };
        var marketplace = new FakeMarketplace();

        var result = await new GetAdminComplaintReviewHandler(new Allow(), complaints, review, marketplace).Handle(new GetAdminComplaintReviewQuery(1, id), CancellationToken.None);

        Assert.Equal(77, result.AccusedTelegramUserId);
        Assert.Equal(77, marketplace.RequestedUser);
        Assert.Equal(77, result.Accused?.User.TelegramUserId);
        // The case itself is not "another complaint".
        Assert.Equal([other.Id], result.OtherComplaints.Select(item => item.Id));
        Assert.Equal((4, 1, 0, 3), (result.Reporter.Filed, result.Reporter.Open, result.Reporter.Resolved, result.Reporter.Dismissed));
        Assert.Equal((55L, 77L), review.SharedBetween);
        Assert.Single(result.SharedHistory);
        Assert.Equal(4.5, result.Reviews.Average);
    }

    [Fact]
    public async Task For_a_deal_the_accused_is_the_other_participant()
    {
        var id = Guid.NewGuid();
        var review = new FakeReview { OtherSide = 88 };

        var result = await new GetAdminComplaintReviewHandler(new Allow(), new FakeComplaints(Complaint(id, "deal", null)), review, new FakeMarketplace()).Handle(new GetAdminComplaintReviewQuery(1, id), CancellationToken.None);

        Assert.Equal(88, result.AccusedTelegramUserId);
        Assert.Equal(("deal", 55L), (review.OtherSideAsked!.Value.Type, review.OtherSideAsked.Value.Reporter));
    }

    [Fact]
    public async Task Without_an_accused_only_the_case_and_the_reporter_are_shown()
    {
        var id = Guid.NewGuid();
        var review = new FakeReview { OtherSide = null };
        var marketplace = new FakeMarketplace();

        var result = await new GetAdminComplaintReviewHandler(new Allow(), new FakeComplaints(Complaint(id, "offer", null)), review, marketplace).Handle(new GetAdminComplaintReviewQuery(1, id), CancellationToken.None);

        Assert.Null(result.AccusedTelegramUserId);
        Assert.Null(result.Accused);
        Assert.Empty(result.OtherComplaints);
        Assert.Empty(result.SharedHistory);
        Assert.Null(marketplace.RequestedUser);
    }

    [Fact]
    public async Task An_unknown_case_is_not_found_and_a_non_admin_gets_nothing()
    {
        var complaints = new FakeComplaints(null);

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => new GetAdminComplaintReviewHandler(new Allow(), complaints, new FakeReview(), new FakeMarketplace()).Handle(new GetAdminComplaintReviewQuery(1, Guid.NewGuid()), CancellationToken.None));
        Assert.Contains("not found", missing.Message);
        var untouched = new FakeComplaints(null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetAdminComplaintReviewHandler(new Deny(), untouched, new FakeReview(), new FakeMarketplace()).Handle(new GetAdminComplaintReviewQuery(2, Guid.NewGuid()), CancellationToken.None));
        Assert.False(untouched.Asked);
    }

    private sealed class Allow : IAdminAccessPolicy { public void EnsureAllowed(long telegramUserId) { } }
    private sealed class Deny : IAdminAccessPolicy { public void EnsureAllowed(long telegramUserId) => throw new UnauthorizedAccessException(); }

    private sealed class FakeComplaints(AdminComplaintDto? complaint) : IComplaintRepository
    {
        public bool Asked { get; private set; }
        public IReadOnlyList<AdminComplaintDto> ByTargets { get; init; } = [];
        public IReadOnlyDictionary<string, int> ReporterCounts { get; init; } = new Dictionary<string, int>();
        public Task<AdminComplaintDto?> GetForAdminAsync(Guid id, CancellationToken cancellationToken) { Asked = true; return Task.FromResult(complaint); }
        public Task<IReadOnlyList<AdminComplaintDto>> ListForAdminByTargetsAsync(IReadOnlyCollection<Guid> targetIds, int take, CancellationToken cancellationToken) => Task.FromResult(ByTargets);
        public Task<IReadOnlyDictionary<string, int>> CountByReporterAsync(long reporterTelegramUserId, CancellationToken cancellationToken) => Task.FromResult(ReporterCounts);
        public Task<Complaint?> FindOpenAsync(long reporterTelegramUserId, string targetType, Guid targetId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountSinceAsync(long reporterTelegramUserId, DateTime sinceUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Complaint?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAsync(Complaint value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdminComplaintDto>> ListForAdminAsync(string? status, int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountOpenAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeReview : IAdminComplaintReviewReadModel
    {
        public long? OtherSide { get; init; }
        public IReadOnlyList<Guid> Targets { get; init; } = [];
        public IReadOnlyList<AdminSharedHistoryDto> Shared { get; init; } = [];
        public (string Type, long Reporter)? OtherSideAsked { get; private set; }
        public (long, long)? SharedBetween { get; private set; }
        public Task<long?> GetOtherSideAsync(string targetType, Guid targetId, long reporterTelegramUserId, CancellationToken cancellationToken) { OtherSideAsked = (targetType, reporterTelegramUserId); return Task.FromResult(OtherSide); }
        public Task<IReadOnlyList<Guid>> GetComplaintTargetIdsAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(Targets);
        public Task<AdminReviewSummaryDto> GetReviewsAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(new AdminReviewSummaryDto(2, 4.5, []));
        public Task<AdminComplaintPersonDto?> GetPersonAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<AdminComplaintPersonDto?>(new(telegramUserId, "Reporter", null, DateTime.UtcNow, false, false));
        public Task<IReadOnlyList<AdminSharedHistoryDto>> GetSharedHistoryAsync(long firstTelegramUserId, long secondTelegramUserId, CancellationToken cancellationToken) { SharedBetween = (firstTelegramUserId, secondTelegramUserId); return Task.FromResult(Shared); }
    }

    private sealed class FakeMarketplace : IAdminMarketplaceReadModel
    {
        public long? RequestedUser { get; private set; }
        public Task<AdminUserDetailsDto?> GetUserDetailsAsync(long telegramUserId, CancellationToken cancellationToken)
        {
            RequestedUser = telegramUserId;
            var user = new AdminPlatformUserDto(telegramUserId, "Accused", null, PlatformRole.Member, false, false, null, null, DateTime.UtcNow);
            return Task.FromResult<AdminUserDetailsDto?>(new(user, [], 0, 0, 0, [], 0, 0, 0));
        }
        public Task<IReadOnlyList<AdminBloggerProfileDto>> GetBloggersAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdminCampaignDto>> GetCampaignsAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CollaborationRequestDto>> GetCollaborationRequestsAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
