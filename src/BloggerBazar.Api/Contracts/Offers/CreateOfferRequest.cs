namespace BloggerBazar.Api.Contracts.Offers;

// Exactly one of BloggerId and BrandFaceId (D48).
public sealed record CreateOfferRequest(Guid? BloggerId, string Format, int? OfferedBudget, DateTime? Deadline, string Message, Guid? BrandFaceId = null);
