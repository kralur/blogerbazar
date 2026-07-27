using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Api.Contracts.Payments;

public sealed record CreateContactUnlockOrderRequest(ContactTargetType TargetType, Guid TargetId);
