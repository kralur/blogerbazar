using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Businesses;
using BloggerBazar.Application.Features.Deals;
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
