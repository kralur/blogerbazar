using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Api.Contracts.Admin;

public sealed record GrantCreditsRequest(long TelegramUserId, int Amount, CreditSource Source = CreditSource.AdminGrant, string? Note = null);
