using System.Security.Authentication;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.BrowserSessions;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.BrowserSessions;

// D64 stage B: who may sign a browser in, the number check, and the session that follows.
public sealed class BrowserSignInTests
{
    private const long Owner = 22;
    private const long Viewer = 78;
    private const long Blogger = 11;
    private static readonly BrowserDevice Laptop = new("chrome", "windows");

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36", "chrome", "windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0", "edge", "windows")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 14_6) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15", "safari", "macos")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:131.0) Gecko/20100101 Firefox/131.0", "firefox", "linux")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 YaBrowser/24.10.0.0 Safari/537.36", "yandex", "windows")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/130.0 Mobile/15E148 Safari/604.1", "chrome", "ios")]
    [InlineData("curl/8.5.0", "other", "other")]
    [InlineData(null, "other", "other")]
    public void The_browser_and_system_are_read_from_the_user_agent(string? userAgent, string browser, string os) =>
        Assert.Equal(new BrowserDevice(browser, os), BrowserDevices.FromUserAgent(userAgent));

    [Fact]
    public async Task The_mini_app_preview_says_who_may_sign_in()
    {
        var (store, users, businesses) = Setup();
        var ticket = await store.StartSignInAsync(Laptop, CancellationToken.None);
        var handler = new PreviewBrowserSignInHandler(store, users, businesses);

        var viewer = await handler.Handle(new PreviewBrowserSignInQuery(Viewer, ticket.Code), CancellationToken.None);
        var blogger = await handler.Handle(new PreviewBrowserSignInQuery(Blogger, ticket.Code), CancellationToken.None);

        Assert.Equal(BrowserEligibility.Ok, viewer.Eligibility);
        Assert.Equal("Kofe Hona", viewer.CompanyName);
        Assert.Equal(("chrome", "windows"), (viewer.Browser, viewer.Os));
        Assert.Equal(ticket.SignIn.Options, viewer.Options);
        Assert.Equal(BrowserEligibility.NoCompany, blogger.Eligibility);
        Assert.Null(blogger.CompanyName);
    }

    [Fact]
    public async Task A_member_with_another_role_selected_is_asked_to_switch_to_business()
    {
        var (store, users, businesses) = Setup(ownerRole: MarketplaceRole.Blogger);
        var ticket = await store.StartSignInAsync(Laptop, CancellationToken.None);

        var preview = await new PreviewBrowserSignInHandler(store, users, businesses).Handle(new PreviewBrowserSignInQuery(Owner, ticket.Code), CancellationToken.None);
        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            new ConfirmBrowserSignInHandler(store, users, businesses).Handle(new ConfirmBrowserSignInCommand(Owner, ticket.Code, ticket.SignIn.Number), CancellationToken.None));

        Assert.Equal(BrowserEligibility.SwitchToBusiness, preview.Eligibility);
        Assert.Equal(BrowserSessionCodes.BusinessRoleRequired, error.Code);
        Assert.Equal(BrowserSignInStatuses.Pending, (await store.GetSignInAsync(ticket.Code, CancellationToken.None))?.Status);
    }

    [Fact]
    public async Task Somebody_outside_any_company_cannot_confirm()
    {
        var (store, users, businesses) = Setup();
        var ticket = await store.StartSignInAsync(Laptop, CancellationToken.None);

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            new ConfirmBrowserSignInHandler(store, users, businesses).Handle(new ConfirmBrowserSignInCommand(Blogger, ticket.Code, ticket.SignIn.Number), CancellationToken.None));

        Assert.Equal(BrowserSessionCodes.CompanyRequired, error.Code);
    }

    // Device-code phishing: "open this link and tap 42" sent in a chat. Telegram signs where the link was opened from.
    [Theory]
    [InlineData("private")]
    [InlineData("group")]
    [InlineData("supergroup")]
    [InlineData("channel")]
    public async Task A_link_somebody_sent_in_a_chat_cannot_be_confirmed(string chatType)
    {
        var (store, users, businesses) = Setup();
        var ticket = await store.StartSignInAsync(Laptop, CancellationToken.None);

        var preview = await new PreviewBrowserSignInHandler(store, users, businesses).Handle(new PreviewBrowserSignInQuery(Owner, ticket.Code, chatType), CancellationToken.None);
        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            new ConfirmBrowserSignInHandler(store, users, businesses).Handle(new ConfirmBrowserSignInCommand(Owner, ticket.Code, ticket.SignIn.Number, chatType), CancellationToken.None));
        var check = await CheckHandler(store, users, businesses).Handle(new CheckBrowserSignInCommand(ticket.Code, ticket.Secret), CancellationToken.None);

        Assert.Equal(BrowserEligibility.SharedLink, preview.Eligibility);
        Assert.Empty(preview.Options);
        Assert.Null(preview.CompanyName);
        Assert.Equal(BrowserSessionCodes.LinkShared, error.Code);
        Assert.Equal(BrowserSignInStatuses.Declined, check.Status);
    }

    [Fact]
    public async Task A_link_opened_from_the_persons_own_chat_or_a_qr_code_is_fine()
    {
        var (store, users, businesses) = Setup();
        var fromSavedMessages = await store.StartSignInAsync(Laptop, CancellationToken.None);
        var fromQrCode = await store.StartSignInAsync(Laptop, CancellationToken.None);
        var confirm = new ConfirmBrowserSignInHandler(store, users, businesses);

        await confirm.Handle(new ConfirmBrowserSignInCommand(Owner, fromSavedMessages.Code, fromSavedMessages.SignIn.Number, "sender"), CancellationToken.None);
        await confirm.Handle(new ConfirmBrowserSignInCommand(Owner, fromQrCode.Code, fromQrCode.SignIn.Number), CancellationToken.None);

        Assert.Equal(BrowserSignInStatuses.Approved, (await store.GetSignInAsync(fromSavedMessages.Code, CancellationToken.None))?.Status);
        Assert.Equal(BrowserSignInStatuses.Approved, (await store.GetSignInAsync(fromQrCode.Code, CancellationToken.None))?.Status);
    }

    [Fact]
    public async Task A_wrong_number_cancels_the_sign_in_for_good()
    {
        var (store, users, businesses) = Setup();
        var ticket = await store.StartSignInAsync(Laptop, CancellationToken.None);
        var wrong = ticket.SignIn.Options.First(option => option != ticket.SignIn.Number);
        var handler = new ConfirmBrowserSignInHandler(store, users, businesses);

        var mismatch = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => handler.Handle(new ConfirmBrowserSignInCommand(Owner, ticket.Code, wrong), CancellationToken.None));
        var retry = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ConfirmBrowserSignInCommand(Owner, ticket.Code, ticket.SignIn.Number), CancellationToken.None));
        var check = await CheckHandler(store, users, businesses).Handle(new CheckBrowserSignInCommand(ticket.Code, ticket.Secret), CancellationToken.None);

        Assert.Equal(BrowserSessionCodes.NumberMismatch, mismatch.Code);
        Assert.Contains("not found", retry.Message);
        Assert.Equal(BrowserSignInStatuses.Declined, check.Status);
        Assert.Null(check.Ticket);
    }

    [Fact]
    public async Task Confirming_opens_one_session_for_the_browser_records_it_and_tells_the_person()
    {
        var (store, users, businesses) = Setup();
        var ticket = await store.StartSignInAsync(Laptop, CancellationToken.None);
        var audit = new RecordingAuditLogs();
        var bot = new SpyBotClient();
        var check = CheckHandler(store, users, businesses, audit, bot);

        var before = await check.Handle(new CheckBrowserSignInCommand(ticket.Code, ticket.Secret), CancellationToken.None);
        await new ConfirmBrowserSignInHandler(store, users, businesses).Handle(new ConfirmBrowserSignInCommand(Viewer, ticket.Code, ticket.SignIn.Number), CancellationToken.None);
        var stranger = await check.Handle(new CheckBrowserSignInCommand(ticket.Code, new string('a', 43)), CancellationToken.None);
        var signedIn = await check.Handle(new CheckBrowserSignInCommand(ticket.Code, ticket.Secret), CancellationToken.None);
        var again = await check.Handle(new CheckBrowserSignInCommand(ticket.Code, ticket.Secret), CancellationToken.None);

        Assert.Equal(BrowserSignInStatuses.Pending, before.Status);
        Assert.Equal(BrowserSignInStatuses.Expired, stranger.Status);
        Assert.Equal(BrowserSignInStatuses.Approved, signedIn.Status);
        Assert.Equal(Viewer, signedIn.Ticket?.Session.TelegramUserId);
        Assert.Equal(BrowserSignInStatuses.Expired, again.Status);
        Assert.Null(again.Ticket);
        Assert.Equal(Viewer, (await store.GetSessionAsync(signedIn.Ticket!.Token, CancellationToken.None))?.TelegramUserId);
        Assert.Equal([BrowserSessionAuditActions.SignedIn], audit.Actions);
        Assert.Equal([Viewer], bot.NotifiedChats);
        Assert.Equal(["/settings"], bot.Routes);
        // Both languages for a person who never chose one, with the Tashkent time of the sign-in.
        var tashkentTime = signedIn.Ticket.Session.CreatedAtUtc.AddHours(5).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains($"в браузере: Chrome, Windows, {tashkentTime}", bot.Texts.Single());
        Assert.Contains($"kirildi: Chrome, Windows, {tashkentTime}", bot.Texts.Single());
        Assert.Equal(["Мои браузеры"], bot.Buttons);
    }

    [Fact]
    public async Task Somebody_removed_from_the_team_after_confirming_gets_no_session()
    {
        var (store, users, businesses) = Setup();
        var ticket = await store.StartSignInAsync(Laptop, CancellationToken.None);
        await new ConfirmBrowserSignInHandler(store, users, businesses).Handle(new ConfirmBrowserSignInCommand(Viewer, ticket.Code, ticket.SignIn.Number), CancellationToken.None);
        businesses.Remove(Viewer);

        var check = await CheckHandler(store, users, businesses).Handle(new CheckBrowserSignInCommand(ticket.Code, ticket.Secret), CancellationToken.None);

        Assert.Equal(BrowserSignInStatuses.Declined, check.Status);
        Assert.Null(check.Ticket);
    }

    [Fact]
    public async Task Declining_in_the_mini_app_tells_the_browser()
    {
        var (store, users, businesses) = Setup();
        var ticket = await store.StartSignInAsync(Laptop, CancellationToken.None);

        await new DeclineBrowserSignInHandler(store).Handle(new DeclineBrowserSignInCommand(Owner, ticket.Code), CancellationToken.None);
        var check = await CheckHandler(store, users, businesses).Handle(new CheckBrowserSignInCommand(ticket.Code, ticket.Secret), CancellationToken.None);

        Assert.Equal(BrowserSignInStatuses.Declined, check.Status);
    }

    [Fact]
    public async Task Each_browser_request_drops_the_session_once_the_person_leaves_the_team_switches_role_or_is_blocked()
    {
        var (store, users, businesses) = Setup();
        var removed = await store.CreateSessionAsync(Viewer, Laptop, CancellationToken.None);
        var switched = await store.CreateSessionAsync(Owner, Laptop, CancellationToken.None);

        var identity = await BrowserSessionAccess.RequireAsync(store, users, businesses, removed.Token, CancellationToken.None);
        businesses.Remove(Viewer);
        users.Get(Owner).SelectMarketplaceRole(MarketplaceRole.Blogger);

        Assert.Equal(Viewer, identity.User.TelegramUserId);
        await Assert.ThrowsAsync<AuthenticationException>(() => BrowserSessionAccess.RequireAsync(store, users, businesses, removed.Token, CancellationToken.None));
        await Assert.ThrowsAsync<AuthenticationException>(() => BrowserSessionAccess.RequireAsync(store, users, businesses, switched.Token, CancellationToken.None));
        // Coming back does not revive the session: a new sign-in is needed.
        users.Get(Owner).SelectMarketplaceRole(MarketplaceRole.Business);
        Assert.Null(await store.GetSessionAsync(switched.Token, CancellationToken.None));

        var blocked = await store.CreateSessionAsync(Owner, Laptop, CancellationToken.None);
        users.Get(Owner).SetBlocked(true);
        await Assert.ThrowsAsync<AuthenticationException>(() => BrowserSessionAccess.RequireAsync(store, users, businesses, blocked.Token, CancellationToken.None));
    }

    private static CheckBrowserSignInHandler CheckHandler(IBrowserSessionStore store, IPlatformUserRepository users, IBusinessProfileRepository businesses, RecordingAuditLogs? audit = null, SpyBotClient? bot = null) =>
        new(store, users, businesses, audit ?? new RecordingAuditLogs(), new SpyUnitOfWork(), bot ?? new SpyBotClient());

    private static (IBrowserSessionStore Store, Users Users, Team Businesses) Setup(MarketplaceRole ownerRole = MarketplaceRole.Business)
    {
        var store = BrowserSessionStoreTests.CreateStore(new BrowserSessionStoreTests.RecordingCache(), TimeProvider.System);
        var users = new Users(User(Owner, ownerRole), User(Viewer, MarketplaceRole.Business), User(Blogger, MarketplaceRole.Blogger));
        var company = Business(Owner, "Kofe Hona");
        return (store, users, new Team(company, new Dictionary<long, BusinessMemberRole> { [Owner] = BusinessMemberRole.Owner, [Viewer] = BusinessMemberRole.Viewer }));
    }

    private sealed class Users(params PlatformUser[] users) : IPlatformUserRepository
    {
        public PlatformUser Get(long telegramUserId) => users.Single(user => user.TelegramUserId == telegramUserId);
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(users);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(users.Length);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    // One company with a team; everybody not listed is outside it.
    private sealed class Team(BusinessProfile business, Dictionary<long, BusinessMemberRole> members) : IBusinessProfileRepository
    {
        public void Remove(long telegramUserId) => members.Remove(telegramUserId);
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(id == business.Id ? business : null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult<BusinessProfile?>(telegramUserId == business.TelegramUserId ? business : null);
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessMembership?> GetMembershipAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult(members.TryGetValue(telegramUserId, out var role) ? new BusinessMembership(business, role) : null);
    }

    private sealed class RecordingAuditLogs : IAuditLogRepository
    {
        public List<string> Actions { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) { Actions.Add(entry.Action); return Task.CompletedTask; }
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuditLog>>([]);
    }
}
