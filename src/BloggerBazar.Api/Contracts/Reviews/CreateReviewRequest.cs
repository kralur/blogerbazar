namespace BloggerBazar.Api.Contracts.Reviews;

public sealed record CreateReviewRequest(int Rating, string? Comment);
