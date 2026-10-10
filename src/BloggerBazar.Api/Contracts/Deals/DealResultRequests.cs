namespace BloggerBazar.Api.Contracts.Deals;

public sealed record SetDealPriceRequest(int Price);

public sealed record AddDealPublicationRequest(string Url, int? Views);

public sealed record UpdateDealPublicationViewsRequest(int? Views);
