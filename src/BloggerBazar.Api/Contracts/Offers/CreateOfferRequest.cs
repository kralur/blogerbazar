namespace BloggerBazar.Api.Contracts.Offers;

public sealed record CreateOfferRequest(Guid BloggerId, string Format, int? OfferedBudget, DateTime? Deadline, string Message);
