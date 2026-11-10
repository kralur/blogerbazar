using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Businesses;
using BloggerBazar.Application.Features.Team;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Team;

// D64 stage C1: a company "with a team" invites people by a one-time link; the owner or an admin sees who asks, with
// the number Telegram confirmed, and lets them in. Free: the owner and two people.
public sealed class TeamTests
{
    private const long Owner = 500;
    private const long Admin = 501;
    private const long Manager = 502;
    private const long Newcomer = 600;
    private const long Stranger = 601;
    private const string Site = "https://app.example.test";

    [Fact]
    public async Task The_owner_invites_a_person_who_asks_and_is_let_in()
    {
        var world = new World();
        var bot = new SpyBotClient();

        var link = await world.Invite(Owner, TeamRoles.Manager);
        var preview = await new PreviewTeamInvitationHandler(world.Team).Handle(new PreviewTeamInvitationQuery(Newcomer, link.Code), CancellationToken.None);
        var asked = await world.RequestHandler(bot).Handle(new RequestTeamAccessCommand(Newcomer, link.Code), CancellationToken.None);

        Assert.Equal(TeamInvitationStates.Open, preview.State);
        Assert.Equal("Kofe Hona", preview.CompanyName);
        Assert.Equal(TeamInvitationStates.RequestedByYou, asked.State);
        Assert.Equal([Owner], bot.NotifiedChats);
        Assert.Contains("+998901234567", bot.Texts[0]);
        Assert.Equal("/team", bot.Routes[0]);

        var view = await new GetTeamHandler(world.Users, world.Businesses, world.Team).Handle(new GetTeamQuery(Owner), CancellationToken.None);
        var request = Assert.Single(view!.Requests);
        Assert.Equal("+998901234567", request.Phone);
        Assert.Equal(TeamRoles.Manager, request.Role);
        Assert.Empty(view.Invitations);

        var accepted = await world.AcceptHandler(bot).Handle(new AcceptTeamRequestCommand(Owner, request.Id, Site), CancellationToken.None);

        Assert.Equal(2, accepted.SeatsUsed);
        Assert.Contains(accepted.Members, member => member.Role == TeamRoles.Manager && member.FirstName == "Newcomer");
        Assert.Empty(accepted.Requests);
        Assert.Equal(MarketplaceRole.Business, world.User(Newcomer).SelectedMarketplaceRole);
        Assert.Equal(Newcomer, bot.NotifiedChats[^1]);
        Assert.Contains(Site, bot.Texts[^1]);
        var again = await new PreviewTeamInvitationHandler(world.Team).Handle(new PreviewTeamInvitationQuery(Newcomer, link.Code), CancellationToken.None);
        Assert.Equal(TeamInvitationStates.Joined, again.State);
    }

    [Fact]
    public async Task A_company_working_alone_makes_no_invitation_links()
    {
        var world = new World(teamMode: false);

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => world.Invite(Owner, TeamRoles.Manager));

        Assert.Equal(TeamCodes.TeamModeOff, error.Code);
    }

    [Fact]
    public async Task The_free_team_is_the_owner_and_two_people()
    {
        var world = new World();
        world.AddMember(Admin, BusinessMemberRole.Admin);
        var link = await world.Invite(Owner, TeamRoles.Viewer);
        await world.RequestHandler().Handle(new RequestTeamAccessCommand(Newcomer, link.Code), CancellationToken.None);
        world.AddMember(Manager, BusinessMemberRole.Manager);

        var create = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => world.Invite(Owner, TeamRoles.Viewer));
        var accept = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            world.AcceptHandler().Handle(new AcceptTeamRequestCommand(Owner, link.Id), CancellationToken.None));

        Assert.Equal(TeamCodes.TeamFull, create.Code);
        Assert.Equal(TeamCodes.TeamFull, accept.Code);
        Assert.Equal(3, await world.Team.CountActiveMembersAsync(world.Company.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Links_waiting_for_an_answer_are_capped()
    {
        var world = new World();
        for (var i = 0; i < TeamLimits.MaxPendingInvitations; i++)
        {
            await world.Invite(Owner, TeamRoles.Viewer);
        }

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => world.Invite(Owner, TeamRoles.Viewer));

        Assert.Equal(TeamCodes.TooManyInvitations, error.Code);
    }

    [Fact]
    public async Task Only_the_owner_handles_admins()
    {
        var world = new World();
        world.AddMember(Admin, BusinessMemberRole.Admin);
        world.AddMember(Manager, BusinessMemberRole.Manager);
        var adminRow = world.Member(Admin);
        var managerRow = world.Member(Manager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => world.Invite(Admin, TeamRoles.Admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new ChangeTeamRoleHandler(world.Users, world.Businesses, world.Team, world.UnitOfWork).Handle(new ChangeTeamRoleCommand(Admin, managerRow.Id, TeamRoles.Admin), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            world.RemoveHandler().Handle(new RemoveTeamMemberCommand(Admin, adminRow.Id), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            world.RemoveHandler().Handle(new RemoveTeamMemberCommand(Admin, world.Member(Owner).Id), CancellationToken.None));

        var changed = await new ChangeTeamRoleHandler(world.Users, world.Businesses, world.Team, world.UnitOfWork)
            .Handle(new ChangeTeamRoleCommand(Admin, managerRow.Id, TeamRoles.Viewer), CancellationToken.None);
        Assert.Contains(changed.Members, member => member.Id == managerRow.Id && member.Role == TeamRoles.Viewer);

        await world.RemoveHandler().Handle(new RemoveTeamMemberCommand(Owner, adminRow.Id), CancellationToken.None);
        Assert.False(adminRow.IsActive);
    }

    [Fact]
    public async Task Managers_and_viewers_see_the_team_but_not_who_asks_and_do_not_manage_it()
    {
        var world = new World();
        world.AddMember(Manager, BusinessMemberRole.Manager);
        var link = await world.Invite(Owner, TeamRoles.Viewer);
        await world.RequestHandler().Handle(new RequestTeamAccessCommand(Newcomer, link.Code), CancellationToken.None);

        var view = await new GetTeamHandler(world.Users, world.Businesses, world.Team).Handle(new GetTeamQuery(Manager), CancellationToken.None);

        Assert.False(view!.CanManage);
        Assert.Empty(view.Requests);
        Assert.Equal(2, view.Members.Count);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => world.Invite(Manager, TeamRoles.Viewer));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            world.AcceptHandler().Handle(new AcceptTeamRequestCommand(Manager, link.Id), CancellationToken.None));
    }

    [Fact]
    public async Task The_team_is_reached_only_while_working_as_the_company()
    {
        var world = new World();
        world.User(Owner).SelectMarketplaceRole(MarketplaceRole.Blogger);

        var view = await new GetTeamHandler(world.Users, world.Businesses, world.Team).Handle(new GetTeamQuery(Owner), CancellationToken.None);
        var invite = await Assert.ThrowsAsync<InvalidOperationException>(() => world.Invite(Owner, TeamRoles.Viewer));

        Assert.Null(view);
        Assert.Contains("not found", invite.Message);
    }

    [Fact]
    public async Task Asking_needs_the_phone_number_from_Telegram()
    {
        var world = new World();
        world.AddUser(PlatformUser.Create(Stranger, "No phone", null));
        var link = await world.Invite(Owner, TeamRoles.Viewer);

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            world.RequestHandler().Handle(new RequestTeamAccessCommand(Stranger, link.Code), CancellationToken.None));

        Assert.Equal(TeamCodes.PhoneRequired, error.Code);
    }

    [Fact]
    public async Task A_person_already_working_for_a_company_does_not_ask_to_join()
    {
        var world = new World();
        world.AddMember(Manager, BusinessMemberRole.Manager, phone: "+998901110000");
        var link = await world.Invite(Owner, TeamRoles.Viewer);

        var same = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            world.RequestHandler().Handle(new RequestTeamAccessCommand(Manager, link.Code), CancellationToken.None));

        Assert.Equal(TeamCodes.AlreadyInTeam, same.Code);
    }

    [Fact]
    public async Task A_link_is_used_once_and_asking_again_changes_nothing()
    {
        var world = new World();
        var other = PlatformUser.Create(Stranger, "Other", null);
        other.VerifyPhone("+998907776655", DateTime.UtcNow);
        world.AddUser(other);
        var bot = new SpyBotClient();
        var link = await world.Invite(Owner, TeamRoles.Viewer);
        await world.RequestHandler(bot).Handle(new RequestTeamAccessCommand(Newcomer, link.Code), CancellationToken.None);

        var again = await world.RequestHandler(bot).Handle(new RequestTeamAccessCommand(Newcomer, link.Code), CancellationToken.None);
        var taken = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            world.RequestHandler(bot).Handle(new RequestTeamAccessCommand(Stranger, link.Code), CancellationToken.None));
        var seen = await new PreviewTeamInvitationHandler(world.Team).Handle(new PreviewTeamInvitationQuery(Stranger, link.Code), CancellationToken.None);

        Assert.Equal(TeamInvitationStates.RequestedByYou, again.State);
        Assert.Equal(TeamCodes.InvitationUsed, taken.Code);
        Assert.Equal(TeamInvitationStates.Used, seen.State);
        Assert.Single(bot.NotifiedChats);
    }

    [Fact]
    public async Task An_expired_link_can_no_longer_be_used()
    {
        var world = new World();
        var link = await world.Invite(Owner, TeamRoles.Viewer);
        var invitation = world.Team.Invitations.Single();
        typeof(BusinessInvitation).GetProperty(nameof(BusinessInvitation.ExpiresAtUtc))!.SetValue(invitation, DateTime.UtcNow.AddMinutes(-1));

        var preview = await new PreviewTeamInvitationHandler(world.Team).Handle(new PreviewTeamInvitationQuery(Newcomer, link.Code), CancellationToken.None);
        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            world.RequestHandler().Handle(new RequestTeamAccessCommand(Newcomer, link.Code), CancellationToken.None));

        Assert.Equal(TeamInvitationStates.Expired, preview.State);
        Assert.Equal(TeamCodes.InvitationUsed, error.Code);
    }

    [Fact]
    public async Task An_unknown_code_finds_nothing()
    {
        var world = new World();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PreviewTeamInvitationHandler(world.Team).Handle(new PreviewTeamInvitationQuery(Newcomer, "not-a-real-code-123"), CancellationToken.None));

        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public async Task The_owner_hears_when_an_admin_lets_someone_in()
    {
        var world = new World();
        world.AddMember(Admin, BusinessMemberRole.Admin);
        var bot = new SpyBotClient();
        var link = await world.Invite(Admin, TeamRoles.Viewer);
        await world.RequestHandler(bot).Handle(new RequestTeamAccessCommand(Newcomer, link.Code), CancellationToken.None);

        await world.AcceptHandler(bot).Handle(new AcceptTeamRequestCommand(Admin, link.Id, Site), CancellationToken.None);

        // Asked: the owner and the admin; accepted: the newcomer, then the owner.
        Assert.Equal([Owner, Admin, Newcomer, Owner], bot.NotifiedChats);
        Assert.Contains("Newcomer", bot.Texts[^1]);
    }

    [Fact]
    public async Task A_declined_person_hears_about_it_and_the_link_is_closed()
    {
        var world = new World();
        var bot = new SpyBotClient();
        var link = await world.Invite(Owner, TeamRoles.Viewer);
        await world.RequestHandler().Handle(new RequestTeamAccessCommand(Newcomer, link.Code), CancellationToken.None);

        var view = await new DeclineTeamRequestHandler(world.Users, world.Businesses, world.Team, world.UnitOfWork, bot)
            .Handle(new DeclineTeamRequestCommand(Owner, link.Id), CancellationToken.None);

        Assert.Empty(view.Requests);
        Assert.Equal([Newcomer], bot.NotifiedChats);
        Assert.Equal(BusinessInvitationStatus.Declined, world.Team.Invitations.Single().Status);
        var seen = await new PreviewTeamInvitationHandler(world.Team).Handle(new PreviewTeamInvitationQuery(Newcomer, link.Code), CancellationToken.None);
        Assert.Equal(TeamInvitationStates.Declined, seen.State);
    }

    [Fact]
    public async Task Back_to_working_alone_only_without_other_people()
    {
        var world = new World();
        world.AddMember(Manager, BusinessMemberRole.Manager);
        await world.Invite(Owner, TeamRoles.Viewer);
        var handler = new SetTeamModeHandler(world.Users, world.Businesses, world.Team, world.UnitOfWork);

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => handler.Handle(new SetTeamModeCommand(Owner, false), CancellationToken.None));
        Assert.Equal(TeamCodes.TeamNotEmpty, error.Code);

        await world.RemoveHandler().Handle(new RemoveTeamMemberCommand(Owner, world.Member(Manager).Id), CancellationToken.None);
        var alone = await handler.Handle(new SetTeamModeCommand(Owner, false), CancellationToken.None);

        Assert.False(alone.TeamMode);
        Assert.False(world.Company.TeamMode);
        Assert.Equal(BusinessInvitationStatus.Revoked, world.Team.Invitations.Single().Status);
    }

    [Fact]
    public async Task Only_the_owner_switches_the_mode()
    {
        var world = new World(teamMode: false);
        world.AddMember(Admin, BusinessMemberRole.Admin);
        var handler = new SetTeamModeHandler(world.Users, world.Businesses, world.Team, world.UnitOfWork);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new SetTeamModeCommand(Admin, true), CancellationToken.None));
        var withTeam = await handler.Handle(new SetTeamModeCommand(Owner, true), CancellationToken.None);

        Assert.True(withTeam.TeamMode);
    }

    [Fact]
    public async Task A_removed_member_is_signed_out_of_browsers_and_told()
    {
        var world = new World();
        world.AddMember(Manager, BusinessMemberRole.Manager);
        var bot = new SpyBotClient();
        var browsers = new SpyBrowserSessions();

        var view = await world.RemoveHandler(bot, browsers).Handle(new RemoveTeamMemberCommand(Owner, world.Member(Manager).Id), CancellationToken.None);

        Assert.DoesNotContain(view.Members, member => member.FirstName == "Manager");
        Assert.Equal([Manager], browsers.SignedOutEverywhere);
        Assert.Equal([Manager], bot.NotifiedChats);
    }

    [Fact]
    public async Task A_member_leaves_and_the_owner_cannot()
    {
        var world = new World();
        world.AddMember(Manager, BusinessMemberRole.Manager);
        var browsers = new SpyBrowserSessions();
        var handler = new LeaveTeamHandler(world.Users, world.Businesses, world.Team, world.UnitOfWork, browsers);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new LeaveTeamCommand(Owner), CancellationToken.None));
        await handler.Handle(new LeaveTeamCommand(Manager), CancellationToken.None);

        Assert.False(world.Member(Manager).IsActive);
        Assert.Equal([Manager], browsers.SignedOutEverywhere);
    }

    [Fact]
    public async Task Work_on_a_computer_sends_the_site_address_to_the_person()
    {
        var world = new World();
        var bot = new SpyBotClient();

        await new SendWorkOnComputerLinkHandler(world.Users, world.Businesses, bot).Handle(new SendWorkOnComputerLinkCommand(Owner, Site), CancellationToken.None);

        Assert.Equal([Owner], bot.NotifiedChats);
        Assert.Contains(Site, bot.Texts.Single());
        Assert.Null(bot.Routes.Single());
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("manager", true)]
    [InlineData("viewer", true)]
    [InlineData("owner", false)]
    [InlineData("Admin", false)]
    [InlineData("", false)]
    public void Invitation_roles_are_the_fixed_strings(string role, bool valid) =>
        Assert.Equal(valid, new CreateTeamInvitationValidator().Validate(new CreateTeamInvitationCommand(Owner, role)).IsValid);

    // One company "Kofe Hona" with its owner, a person with a confirmed phone who may join, and the team in memory.
    private sealed class World
    {
        private readonly List<PlatformUser> users = [];

        public World(bool teamMode = true)
        {
            Company = Business(Owner, "Kofe Hona");
            Company.SetTeamMode(teamMode);
            Team = new InMemoryTeam(Company, users);
            Businesses = new TeamBusinesses(Company, Team);
            Users = new ListUsers(users);
            AddUser(DealTestData.User(Owner, MarketplaceRole.Business));
            Team.Members.Add(BusinessMember.Owner(Company.Id, Owner, DateTime.UtcNow));
            var newcomer = PlatformUser.Create(Newcomer, "Newcomer", "new_person");
            newcomer.VerifyPhone("+998901234567", DateTime.UtcNow);
            AddUser(newcomer);
        }

        public BusinessProfile Company { get; }
        public InMemoryTeam Team { get; }
        public TeamBusinesses Businesses { get; }
        public IPlatformUserRepository Users { get; }
        public SpyUnitOfWork UnitOfWork { get; } = new();

        public void AddUser(PlatformUser user) => users.Add(user);

        public PlatformUser User(long telegramUserId) => users.Single(user => user.TelegramUserId == telegramUserId);

        public void AddMember(long telegramUserId, BusinessMemberRole role, string? phone = null)
        {
            var user = PlatformUser.Create(telegramUserId, role.ToString(), null);
            user.SelectMarketplaceRole(MarketplaceRole.Business);
            if (phone is not null) user.VerifyPhone(phone, DateTime.UtcNow);
            AddUser(user);
            Team.Members.Add(BusinessMember.Join(Company.Id, telegramUserId, role, Owner, DateTime.UtcNow));
        }

        public BusinessMember Member(long telegramUserId) => Team.Members.Single(member => member.TelegramUserId == telegramUserId);

        public Task<TeamInvitationCreated> Invite(long actor, string role) =>
            new CreateTeamInvitationHandler(Users, Businesses, Team, UnitOfWork).Handle(new CreateTeamInvitationCommand(actor, role), CancellationToken.None);

        public RequestTeamAccessHandler RequestHandler(SpyBotClient? bot = null) => new(Team, Businesses, Users, bot);

        public AcceptTeamRequestHandler AcceptHandler(SpyBotClient? bot = null) => new(Businesses, Team, Users, UnitOfWork, bot);

        public RemoveTeamMemberHandler RemoveHandler(SpyBotClient? bot = null, SpyBrowserSessions? browsers = null) => new(Users, Businesses, Team, UnitOfWork, browsers, bot);
    }

    private sealed class ListUsers(List<PlatformUser> users) : IPlatformUserRepository
    {
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(users);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(users.Count);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) { users.Add(user); return Task.CompletedTask; }
    }

    // Membership comes from the in-memory team, so a person let in is in the company at once.
    private sealed class TeamBusinesses(BusinessProfile company, InMemoryTeam team) : IBusinessProfileRepository
    {
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(id == company.Id ? company : null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult<BusinessProfile?>(telegramUserId == company.TelegramUserId ? company : null);
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessMembership?> GetMembershipAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult(team.Members.SingleOrDefault(member => member.IsActive && member.TelegramUserId == telegramUserId) is { } member
                ? new BusinessMembership(company, member.Role)
                : null);
    }

    private sealed class InMemoryTeam(BusinessProfile company, List<PlatformUser> users) : IBusinessTeamRepository
    {
        public List<BusinessMember> Members { get; } = [];
        public List<BusinessInvitation> Invitations { get; } = [];

        private PlatformUser? UserOf(long? telegramUserId) => users.SingleOrDefault(user => user.TelegramUserId == telegramUserId);

        public Task<IReadOnlyList<TeamMemberRow>> GetMembersAsync(Guid businessId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TeamMemberRow>>([.. Members.Where(member => member.BusinessId == businessId && member.IsActive)
                .Select(member => new TeamMemberRow(member.Id, member.TelegramUserId, UserOf(member.TelegramUserId)?.FirstName ?? "", UserOf(member.TelegramUserId)?.Username, member.Role, member.JoinedAtUtc))]);

        public Task<IReadOnlyList<TeamInvitationRow>> GetPendingInvitationsAsync(Guid businessId, DateTime nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TeamInvitationRow>>([.. Invitations
                .Where(invitation => invitation.BusinessId == businessId && (invitation.IsOpenAt(nowUtc) || invitation.Status == BusinessInvitationStatus.Requested))
                .Select(invitation => new TeamInvitationRow(invitation.Id, invitation.Role, invitation.Status, invitation.CreatedAtUtc, invitation.ExpiresAtUtc,
                    invitation.RequestedByTelegramUserId, UserOf(invitation.RequestedByTelegramUserId)?.FirstName, UserOf(invitation.RequestedByTelegramUserId)?.Username,
                    UserOf(invitation.RequestedByTelegramUserId)?.VerifiedPhone, invitation.RequestedAtUtc))]);

        public Task<int> CountActiveMembersAsync(Guid businessId, CancellationToken cancellationToken) =>
            Task.FromResult(Members.Count(member => member.BusinessId == businessId && member.IsActive));

        public Task<BusinessMember?> GetActiveMemberAsync(Guid businessId, Guid memberId, CancellationToken cancellationToken) =>
            Task.FromResult(Members.SingleOrDefault(member => member.BusinessId == businessId && member.Id == memberId && member.IsActive));

        public Task<BusinessMember?> GetActiveMembershipAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult(Members.SingleOrDefault(member => member.TelegramUserId == telegramUserId && member.IsActive));

        public Task<IReadOnlyList<long>> GetDecidersAsync(Guid businessId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<long>>([.. Members.Where(member => member.BusinessId == businessId && member.IsActive && member.Role is BusinessMemberRole.Owner or BusinessMemberRole.Admin)
                .OrderBy(member => member.Role).Select(member => member.TelegramUserId)]);

        public Task AddMemberAsync(BusinessMember member, CancellationToken cancellationToken) { Members.Add(member); return Task.CompletedTask; }

        public Task AddInvitationAsync(BusinessInvitation invitation, CancellationToken cancellationToken)
        {
            // What the real repository's Include gives: the company with the invitation.
            typeof(BusinessInvitation).GetProperty(nameof(BusinessInvitation.Business))!.SetValue(invitation, company);
            Invitations.Add(invitation);
            return Task.CompletedTask;
        }

        public Task<bool> TryRequestAsync(Guid invitationId, long telegramUserId, DateTime nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Invitations.Single(invitation => invitation.Id == invitationId).Request(telegramUserId, nowUtc));

        public Task<BusinessInvitation?> GetInvitationByCodeHashAsync(string codeHash, CancellationToken cancellationToken) =>
            Task.FromResult(Invitations.SingleOrDefault(invitation => invitation.CodeHash == codeHash));

        public Task<BusinessInvitation?> GetInvitationAsync(Guid businessId, Guid invitationId, CancellationToken cancellationToken) =>
            Task.FromResult(Invitations.SingleOrDefault(invitation => invitation.BusinessId == businessId && invitation.Id == invitationId));

        public Task CloseInvitationsAsync(Guid businessId, long closedByTelegramUserId, DateTime nowUtc, CancellationToken cancellationToken)
        {
            foreach (var invitation in Invitations.Where(invitation => invitation.BusinessId == businessId))
            {
                invitation.Revoke(closedByTelegramUserId, nowUtc);
                invitation.Decline(closedByTelegramUserId, nowUtc);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SpyBrowserSessions : IBrowserSessionStore
    {
        public List<long> SignedOutEverywhere { get; } = [];

        public Task RemoveAllSessionsAsync(long telegramUserId, CancellationToken cancellationToken)
        {
            SignedOutEverywhere.Add(telegramUserId);
            return Task.CompletedTask;
        }

        public Task<BrowserSignInTicket> StartSignInAsync(BrowserDevice device, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BrowserSignIn?> GetSignInAsync(string code, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BrowserSignIn?> GetSignInForBrowserAsync(string code, string secret, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DecideSignInAsync(string code, long? approvedByTelegramUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RemoveSignInAsync(string code, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BrowserSessionTicket> CreateSessionAsync(long telegramUserId, BrowserDevice device, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BrowserSession?> GetSessionAsync(string token, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<BrowserSession>> ListSessionsAsync(long telegramUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RemoveSessionAsync(string token, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RemoveSessionByIdAsync(long telegramUserId, string sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
