using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.CollaborationRequests;

namespace BloggerBazar.Application.Tests.Features.Admin;

public sealed class AdminFunnelHandlerTests
{
    [Theory]
    [InlineData(null, "30d", 30)]
    [InlineData("30d", "30d", 30)]
    [InlineData("7d", "7d", 7)]
    public async Task Period_starts_that_many_days_back(string? requested, string expected, int days)
    {
        var marketplace = new FakeMarketplace();
        var before = DateTime.UtcNow;

        var funnel = await new GetAdminFunnelHandler(new AllowAdmin(), marketplace).Handle(new GetAdminFunnelQuery(1, requested), CancellationToken.None);

        Assert.Equal(expected, funnel.Period);
        Assert.NotNull(marketplace.RequestedFrom);
        Assert.Equal(funnel.FromUtc, marketplace.RequestedFrom);
        Assert.InRange(marketplace.RequestedFrom!.Value, before.AddDays(-days), DateTime.UtcNow.AddDays(-days));
        Assert.Equal(DateTimeKind.Utc, marketplace.RequestedFrom.Value.Kind);
        Assert.Same(marketplace.Roles, funnel.Roles);
    }

    [Fact]
    public async Task All_time_has_no_start()
    {
        var marketplace = new FakeMarketplace();

        var funnel = await new GetAdminFunnelHandler(new AllowAdmin(), marketplace).Handle(new GetAdminFunnelQuery(1, AdminFunnel.AllTime), CancellationToken.None);

        Assert.Equal("all", funnel.Period);
        Assert.Null(funnel.FromUtc);
        Assert.True(marketplace.Called);
        Assert.Null(marketplace.RequestedFrom);
    }

    [Fact]
    public async Task Non_admin_gets_no_numbers()
    {
        var marketplace = new FakeMarketplace();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetAdminFunnelHandler(new DenyAdmin(), marketplace).Handle(new GetAdminFunnelQuery(2, null), CancellationToken.None));
        Assert.False(marketplace.Called);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("7d", true)]
    [InlineData("30d", true)]
    [InlineData("all", true)]
    [InlineData("1d", false)]
    [InlineData("ALL", false)]
    [InlineData("", false)]
    public void Only_known_periods_pass_validation(string? period, bool valid)
    {
        Assert.Equal(valid, new GetAdminFunnelValidator().Validate(new GetAdminFunnelQuery(1, period)).IsValid);
    }

    private sealed class FakeMarketplace : IAdminMarketplaceReadModel
    {
        public bool Called { get; private set; }
        public DateTime? RequestedFrom { get; private set; }
        public IReadOnlyList<AdminFunnelRoleDto> Roles { get; } = [new("blogger", [new(AdminFunnel.Opened, 3)])];

        public Task<IReadOnlyList<AdminFunnelRoleDto>> GetFunnelAsync(DateTime? fromUtc, CancellationToken cancellationToken)
        {
            Called = true;
            RequestedFrom = fromUtc;
            return Task.FromResult(Roles);
        }

        public Task<IReadOnlyList<AdminBloggerProfileDto>> GetBloggersAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdminCampaignDto>> GetCampaignsAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CollaborationRequestDto>> GetCollaborationRequestsAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
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
