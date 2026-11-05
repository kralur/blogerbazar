using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;

namespace BloggerBazar.Application.Tests.Features.Admin;

public sealed class AdminSignalsHandlerTests
{
    [Fact]
    public async Task Non_admin_gets_nothing_and_no_query_runs()
    {
        var signals = new FakeSignals();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetAdminSignalsHandler(new Deny(), signals).Handle(new GetAdminSignalsQuery(5), CancellationToken.None));
        Assert.Equal(0, signals.Calls);
    }

    [Fact]
    public async Task Asks_each_list_with_the_rules_and_returns_them_for_the_page()
    {
        var signals = new FakeSignals();

        var result = await new GetAdminSignalsHandler(new Allow(), signals).Handle(new GetAdminSignalsQuery(1), CancellationToken.None);

        Assert.Equal(5, signals.Calls);
        Assert.Equal(AdminSignalRules.QuickDealMinutes, signals.QuickDealMinutes);
        Assert.Equal(AdminSignalRules.ComplaintReporters, signals.MinReporters);
        Assert.Equal(AdminSignalRules.BurstAccounts, signals.MinAccounts);
        Assert.All(signals.Takes, take => Assert.Equal(AdminSignalRules.MaxRows, take));
        Assert.Equal(30, result.Rules.QuickDealMinutes);
        Assert.True(Assert.Single(result.SignupBursts).LowProfiles);
    }

    [Theory]
    [InlineData(-3, 40, 3, true)]   // ended hours ago, under 10% made a profile
    [InlineData(-3, 40, 4, false)]  // exactly 10%: not low
    [InlineData(-1, 40, 0, false)]  // too fresh to judge
    public void A_burst_is_low_on_profiles_only_once_people_had_time(int hoursAgo, int accounts, int withProfile, bool low) =>
        Assert.Equal(low, GetAdminSignalsHandler.IsLowProfiles(new AdminSignupBurstDto("ig", DateTime.UtcNow.AddHours(hoursAgo), accounts, withProfile, false), DateTime.UtcNow));

    private sealed class Allow : IAdminAccessPolicy { public void EnsureAllowed(long telegramUserId) { } }
    private sealed class Deny : IAdminAccessPolicy { public void EnsureAllowed(long telegramUserId) => throw new UnauthorizedAccessException(); }

    private sealed class FakeSignals : IAdminSignalsReadModel
    {
        public int Calls { get; private set; }
        public List<int> Takes { get; } = [];
        public int QuickDealMinutes { get; private set; }
        public int MinReporters { get; private set; }
        public int MinAccounts { get; private set; }

        public Task<IReadOnlyList<AdminDuplicateHandleDto>> GetDuplicateHandlesAsync(int take, CancellationToken cancellationToken) { Calls++; Takes.Add(take); return Task.FromResult<IReadOnlyList<AdminDuplicateHandleDto>>([]); }
        public Task<IReadOnlyList<AdminSharedPhoneDto>> GetSharedPhonesAsync(int take, CancellationToken cancellationToken) { Calls++; Takes.Add(take); return Task.FromResult<IReadOnlyList<AdminSharedPhoneDto>>([]); }
        public Task<IReadOnlyList<AdminQuickDealDto>> GetQuickDealsAsync(DateTime sinceUtc, int maxMinutes, int take, CancellationToken cancellationToken) { Calls++; Takes.Add(take); QuickDealMinutes = maxMinutes; return Task.FromResult<IReadOnlyList<AdminQuickDealDto>>([]); }
        public Task<IReadOnlyList<AdminComplaintTargetDto>> GetComplaintTargetsAsync(DateTime sinceUtc, int minReporters, int take, CancellationToken cancellationToken) { Calls++; Takes.Add(take); MinReporters = minReporters; return Task.FromResult<IReadOnlyList<AdminComplaintTargetDto>>([]); }
        public Task<IReadOnlyList<AdminSignupBurstDto>> GetSignupBurstsAsync(DateTime sinceUtc, int minAccounts, int take, CancellationToken cancellationToken)
        {
            Calls++; Takes.Add(take); MinAccounts = minAccounts;
            return Task.FromResult<IReadOnlyList<AdminSignupBurstDto>>([new AdminSignupBurstDto("ig", DateTime.UtcNow.AddHours(-5), 40, 1, false)]);
        }
    }
}
