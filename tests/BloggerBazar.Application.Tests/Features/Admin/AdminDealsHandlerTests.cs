using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.CollaborationRequests;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Admin;

public sealed class AdminDealsHandlerTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("active", DealStatus.Active)]
    [InlineData("completed", DealStatus.Completed)]
    [InlineData("cancelled", DealStatus.Cancelled)]
    public async Task Asks_for_the_chosen_status(string? filter, DealStatus? expected)
    {
        var marketplace = new FakeMarketplace();

        var deals = await new GetAdminDealsHandler(marketplace, new AllowAdmin()).Handle(new GetAdminDealsQuery(1, filter), CancellationToken.None);

        Assert.True(marketplace.Called);
        Assert.Equal(expected, marketplace.RequestedStatus);
        Assert.Same(marketplace.Deals, deals);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(5000, 200)]
    public async Task Keeps_the_page_size_within_limits(int take, int expected)
    {
        var marketplace = new FakeMarketplace();

        await new GetAdminDealsHandler(marketplace, new AllowAdmin()).Handle(new GetAdminDealsQuery(1, null, take), CancellationToken.None);

        Assert.Equal(expected, marketplace.RequestedTake);
    }

    [Fact]
    public async Task Non_admin_gets_no_deals()
    {
        var marketplace = new FakeMarketplace();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetAdminDealsHandler(marketplace, new DenyAdmin()).Handle(new GetAdminDealsQuery(2), CancellationToken.None));
        Assert.False(marketplace.Called);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("active", true)]
    [InlineData("completed", true)]
    [InlineData("cancelled", true)]
    [InlineData("Active", false)]
    [InlineData("1", false)]
    [InlineData("", false)]
    public void Only_known_statuses_pass_validation(string? status, bool valid)
    {
        Assert.Equal(valid, new GetAdminDealsValidator().Validate(new GetAdminDealsQuery(1, status)).IsValid);
    }

    private sealed class AllowAdmin : IAdminAccessPolicy { public void EnsureAllowed(long telegramUserId) { } }
    private sealed class DenyAdmin : IAdminAccessPolicy { public void EnsureAllowed(long telegramUserId) => throw new UnauthorizedAccessException(); }

    private sealed class FakeMarketplace : IAdminMarketplaceReadModel
    {
        public bool Called { get; private set; }
        public DealStatus? RequestedStatus { get; private set; }
        public int RequestedTake { get; private set; }
        public IReadOnlyList<AdminDealDto> Deals { get; } =
            [new(Guid.NewGuid(), "Launch", false, "Cafe", 10, "blogger", "Madina", 20, (int)DealStatus.Active, null, DateTime.UtcNow, null)];

        public Task<IReadOnlyList<AdminDealDto>> GetDealsAsync(DealStatus? status, int take, CancellationToken cancellationToken)
        {
            Called = true;
            RequestedStatus = status;
            RequestedTake = take;
            return Task.FromResult(Deals);
        }

        public Task<IReadOnlyList<AdminBloggerProfileDto>> GetBloggersAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdminCampaignDto>> GetCampaignsAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CollaborationRequestDto>> GetCollaborationRequestsAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
