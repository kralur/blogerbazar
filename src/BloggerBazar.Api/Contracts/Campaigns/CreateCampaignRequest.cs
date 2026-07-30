namespace BloggerBazar.Api.Contracts.Campaigns;

public sealed record CreateCampaignRequest(string Title, string Description, string? City, IReadOnlyCollection<string> Categories, IReadOnlyCollection<string>? Requirements, int? BudgetFrom, int? BudgetTo, DateTime? Deadline, bool PublishImmediately);
