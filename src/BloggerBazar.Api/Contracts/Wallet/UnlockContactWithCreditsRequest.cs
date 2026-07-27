using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Api.Contracts.Wallet;

public sealed record UnlockContactWithCreditsRequest(ContactTargetType TargetType, Guid TargetId);
