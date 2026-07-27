namespace BloggerBazar.Api.Contracts.Campaigns;

public sealed record CreateCampaignRequest(string Title, string Description, string? City, IReadOnlyCollection<string> Categories, int? BudgetFrom, int? BudgetTo, bool PublishImmediately);
