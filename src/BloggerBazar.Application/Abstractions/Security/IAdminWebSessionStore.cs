namespace BloggerBazar.Application.Abstractions.Security;

// Server-side sessions of the web admin (Redis when configured). The browser holds only a random token
// in an HttpOnly cookie; removing the entry ends the session at once.
public interface IAdminWebSessionStore
{
    // False when this signed sign-in was already used: the same Telegram data cannot open a second session.
    Task<bool> TryUseSignInAsync(string hash, CancellationToken cancellationToken);
    Task<AdminWebSessionTicket> CreateAsync(long telegramUserId, CancellationToken cancellationToken);
    // Null when the session is unknown, idle for too long or past its fixed end.
    Task<AdminWebSession?> GetAsync(string token, CancellationToken cancellationToken);
    Task RemoveAsync(string token, CancellationToken cancellationToken);
    // Ends every session of this person that exists now, on any browser; sessions opened later are not affected.
    Task RemoveAllAsync(long telegramUserId, CancellationToken cancellationToken);
}

public sealed record AdminWebSession(long TelegramUserId, DateTime CreatedAtUtc, DateTime ExpiresAtUtc);

public sealed record AdminWebSessionTicket(string Token, AdminWebSession Session);
