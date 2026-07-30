using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Marketplace;

namespace BloggerBazar.Application.Tests.Features.Marketplace;

public sealed class GetMarketplaceHomeHandlerTests
{
    [Fact]
    public async Task Delegates_to_the_marketplace_read_model()
    {
        var expected = new MarketplaceHomeDto([], [], [], [], [], [], [], new MarketplaceStatisticsDto(0, 0, 0, 0, null));
        var readModel = new StubMarketplaceHomeReadModel(expected);
        var handler = new GetMarketplaceHomeHandler(readModel);

        var result = await handler.Handle(new GetMarketplaceHomeQuery(), CancellationToken.None);

        Assert.Same(expected, result);
        Assert.True(readModel.WasCalled);
    }

    private sealed class StubMarketplaceHomeReadModel(MarketplaceHomeDto result) : IMarketplaceHomeReadModel
    {
        public bool WasCalled { get; private set; }

        public Task<MarketplaceHomeDto> GetAsync(CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(result);
        }
    }
}
