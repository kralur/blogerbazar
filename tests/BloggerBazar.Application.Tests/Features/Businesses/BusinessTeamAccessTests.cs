using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Businesses;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Application.Features.Team;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Businesses;

// D64 stage A: the company a request acts for is the caller's team membership, and the team role limits the action.
public sealed class BusinessTeamAccessTests
{
    private const long Owner = 22;
    private const long Manager = 77;
    private const long Viewer = 78;
    private const long Admin = 79;
    private const long Outsider = 90;

    [Theory]
    [InlineData(BusinessMemberRole.Owner, BusinessPermission.Own, true)]
    [InlineData(BusinessMemberRole.Admin, BusinessPermission.Own, false)]
    [InlineData(BusinessMemberRole.Admin, BusinessPermission.ManageTeam, true)]
    [InlineData(BusinessMemberRole.Admin, BusinessPermission.ManageProfile, true)]
    [InlineData(BusinessMemberRole.Manager, BusinessPermission.ManageProfile, false)]
    [InlineData(BusinessMemberRole.Manager, BusinessPermission.Operate, true)]
    [InlineData(BusinessMemberRole.Viewer, BusinessPermission.Operate, false)]
    [InlineData(BusinessMemberRole.Viewer, BusinessPermission.View, true)]
    public void Team_roles_follow_the_table_of_rights(BusinessMemberRole role, BusinessPermission permission, bool allowed) =>
        Assert.Equal(allowed, BusinessMember.Allows(role, permission));

    [Fact]
    public async Task A_manager_completes_the_company_deal_and_the_blogger_is_told()
    {
        var (blogger, business, deal) = Setup();
        var bot = new SpyBotClient();
        var handler = new CompleteDealHandler(new FakeDeals(deal), new FakeUsers(User(Manager, MarketplaceRole.Business)), new FakeBloggers(blogger), Team(business), bot);

        var result = await handler.Handle(new CompleteDealCommand(deal.Id, Manager), CancellationToken.None);

        Assert.Equal((int)DealStatus.Completed, result.Status);
        Assert.Equal([blogger.TelegramUserId], bot.NotifiedChats);
    }

    [Fact]
    public async Task A_viewer_sees_the_company_deal_but_cannot_complete_it()
    {
        var (blogger, business, deal) = Setup();
        var deals = new FakeDeals(deal);
        var handler = new CompleteDealHandler(deals, new FakeUsers(User(Viewer, MarketplaceRole.Business)), new FakeBloggers(blogger), Team(business));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new CompleteDealCommand(deal.Id, Viewer), CancellationToken.None));
        Assert.Equal(0, deals.CompletionAttempts);
        Assert.Equal(DealStatus.Active, deal.Status);
    }

    [Fact]
    public async Task Somebody_outside_the_team_finds_no_company_deal()
    {
        var (blogger, business, deal) = Setup();
        var handler = new CompleteDealHandler(new FakeDeals(deal), new FakeUsers(User(Outsider, MarketplaceRole.Business)), new FakeBloggers(blogger), Team(business));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CompleteDealCommand(deal.Id, Outsider), CancellationToken.None));
        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public async Task A_team_member_does_not_open_a_second_company()
    {
        var (_, business, _) = Setup();
        var handler = new CreateBusinessProfileHandler(Team(business), new SpyUnitOfWork());

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            handler.Handle(new CreateBusinessProfileCommand(Manager, "Second company", null, "tashkent", null, null, null, null, null), CancellationToken.None));

        Assert.Equal(BusinessTeamCodes.MemberOfAnotherCompany, error.Code);
    }

    [Fact]
    public async Task An_admin_edits_the_company_profile_and_a_manager_may_not()
    {
        var (_, business, _) = Setup();
        var unitOfWork = new SpyUnitOfWork();
        var handler = new UpdateBusinessProfileHandler(Team(business), unitOfWork);
        UpdateBusinessProfileCommand Edit(long actor) => new(actor, "Kofe Hona", null, "tashkent", null, null, "Coffee in Tashkent", null, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(Edit(Manager), CancellationToken.None));
        Assert.Equal(0, unitOfWork.SaveCalls);

        var result = await handler.Handle(Edit(Admin), CancellationToken.None);
        Assert.Equal("Kofe Hona", result.Name);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task An_admin_editing_the_profile_keeps_the_owners_Telegram_and_phone()
    {
        var (_, business, _) = Setup();
        business.Update(business.Name, "kofe_owner", "tashkent", null, null, null, "+998901112233", null);
        var handler = new UpdateBusinessProfileHandler(Team(business), new SpyUnitOfWork());

        var byAdmin = await handler.Handle(new UpdateBusinessProfileCommand(Admin, "Kofe Hona", "the_admin", "tashkent", null, null, null, "+998905556677", null), CancellationToken.None);
        Assert.Equal("kofe_owner", byAdmin.Username);
        Assert.Equal("+998901112233", business.Phone);

        var byOwner = await handler.Handle(new UpdateBusinessProfileCommand(Owner, "Kofe Hona", "kofe_new", "tashkent", null, null, null, "+998909990000", null), CancellationToken.None);
        Assert.Equal("kofe_new", byOwner.Username);
        Assert.Equal("+998909990000", business.Phone);
    }

    [Fact]
    public async Task The_company_reviews_a_deal_once_whoever_in_the_team_writes_it()
    {
        var (blogger, business, deal) = Setup();
        deal.Complete();
        var reviews = new SideReviews();
        CreateReviewHandler Handler(long actor) =>
            new(new FakeDeals(deal), new FakeUsers(User(actor, MarketplaceRole.Business)), new FakeBloggers(blogger), Team(business), reviews, new SpyUnitOfWork());

        await Handler(Manager).Handle(new CreateReviewCommand(deal.Id, Manager, 5, null), CancellationToken.None);
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(Admin).Handle(new CreateReviewCommand(deal.Id, Admin, 1, null), CancellationToken.None));

        Assert.Contains("already reviewed", second.Message);
        Assert.Equal(ReviewTargetType.Blogger, Assert.Single(reviews.Reviews).TargetType);
    }

    [Fact]
    public async Task A_company_created_with_a_team_says_so_with_the_callers_role()
    {
        var created = new List<BusinessProfile>();
        var handler = new CreateBusinessProfileHandler(new RecordingBusinesses(created), new SpyUnitOfWork());

        await handler.Handle(new CreateBusinessProfileCommand(Owner, "Team company", null, "tashkent", null, null, null, "+998901112233", null, TeamMode: true), CancellationToken.None);

        var company = Assert.Single(created);
        Assert.True(company.TeamMode);
        var mine = await new GetMyBusinessProfileHandler(Team(company)).Handle(new GetMyBusinessProfileQuery(Manager), CancellationToken.None);
        Assert.True(mine!.TeamMode);
        Assert.Equal(TeamRoles.Manager, mine.TeamRole);
    }

    private static (BloggerProfile Blogger, BusinessProfile Business, Deal Deal) Setup()
    {
        var blogger = Blogger(11);
        var business = Business(Owner);
        return (blogger, business, CampaignDeal(blogger, business));
    }

    private static TeamBusinesses Team(BusinessProfile business) => new(business, new Dictionary<long, BusinessMemberRole>
    {
        [Owner] = BusinessMemberRole.Owner,
        [Admin] = BusinessMemberRole.Admin,
        [Manager] = BusinessMemberRole.Manager,
        [Viewer] = BusinessMemberRole.Viewer
    });

    // One company with a team; everybody not listed is outside it.
    private sealed class TeamBusinesses(BusinessProfile business, IReadOnlyDictionary<long, BusinessMemberRole> members) : IBusinessProfileRepository
    {
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(id == business.Id ? business : null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult<BusinessProfile?>(telegramUserId == business.TelegramUserId ? business : null);
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessMembership?> GetMembershipAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult(members.TryGetValue(telegramUserId, out var role) ? new BusinessMembership(business, role) : null);
        public Task<bool> IsMemberAsync(Guid businessId, long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult(businessId == business.Id && members.ContainsKey(telegramUserId));
    }

    // One review per side of a deal, as the unique index (DealId, TargetType) keeps it.
    private sealed class SideReviews : IReviewRepository
    {
        public List<Review> Reviews { get; } = [];
        public Task<bool> ExistsAsync(Guid dealId, ReviewTargetType targetType, CancellationToken cancellationToken) =>
            Task.FromResult(Reviews.Any(review => review.DealId == dealId && review.TargetType == targetType));
        public Task AddAsync(Review review, CancellationToken cancellationToken) { Reviews.Add(review); return Task.CompletedTask; }
        public Task<int> PublishRevealedAsync(Guid? dealId, DateTime nowUtc, CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class RecordingBusinesses(List<BusinessProfile> created) : IBusinessProfileRepository
    {
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(created.SingleOrDefault(company => company.Id == id));
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult(created.SingleOrDefault(company => company.TelegramUserId == telegramUserId));
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) { created.Add(profile); return Task.CompletedTask; }
        public Task<BusinessMembership?> GetMembershipAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BusinessMembership?>(null);
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }
}
