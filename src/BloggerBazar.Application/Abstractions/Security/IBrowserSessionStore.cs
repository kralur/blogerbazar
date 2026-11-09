namespace BloggerBazar.Application.Abstractions.Security;

// D64 stage B: business work in an ordinary browser. The browser asks to sign in and gets a short code; the person
// opens t.me/<bot>?startapp=web_<code> in Telegram and confirms it in the Mini App (signed by Telegram). The browser
// then receives a session: a random token in an HttpOnly cookie, the session itself on the server (Redis when
// configured), so removing it ends the browser's access at once.
public interface IBrowserSessionStore
{
    // A new sign-in request, valid for a couple of minutes. The code goes into the Telegram link; the secret stays
    // in this browser's cookie, so only the browser that asked can collect the session.
    Task<BrowserSignInTicket> StartSignInAsync(BrowserDevice device, CancellationToken cancellationToken);
    // Null when the code is unknown or past its end.
    Task<BrowserSignIn?> GetSignInAsync(string code, CancellationToken cancellationToken);
    // The same, but only for the browser holding the secret of this request.
    Task<BrowserSignIn?> GetSignInForBrowserAsync(string code, string secret, CancellationToken cancellationToken);
    // Pending -> approved by this person, or pending -> declined (approvedBy null). False when it is no longer pending.
    Task<bool> DecideSignInAsync(string code, long? approvedByTelegramUserId, CancellationToken cancellationToken);
    Task RemoveSignInAsync(string code, CancellationToken cancellationToken);

    Task<BrowserSessionTicket> CreateSessionAsync(long telegramUserId, BrowserDevice device, CancellationToken cancellationToken);
    // Null when the session is unknown, idle for too long, past its fixed end or ended everywhere. A found session
    // counts as used now (the idle clock restarts).
    Task<BrowserSession?> GetSessionAsync(string token, CancellationToken cancellationToken);
    // This person's live sessions, newest first; reading the list does not keep them alive.
    Task<IReadOnlyList<BrowserSession>> ListSessionsAsync(long telegramUserId, CancellationToken cancellationToken);
    Task RemoveSessionAsync(string token, CancellationToken cancellationToken);
    // False when this person has no live session with that id.
    Task<bool> RemoveSessionByIdAsync(long telegramUserId, string sessionId, CancellationToken cancellationToken);
    // Ends every session of this person that exists now; sessions opened later are not affected.
    Task RemoveAllSessionsAsync(long telegramUserId, CancellationToken cancellationToken);
}

// Browser and system as D26 strings (BrowserDevices); the clients name them.
public sealed record BrowserDevice(string Browser, string Os);

public static class BrowserSignInStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Declined = "declined";
    public const string Expired = "expired";
}

// Number: shown in the browser; the Mini App offers it among two others (Options), so a sign-in nobody is looking at
// in a browser cannot be confirmed by a reflex tap.
public sealed record BrowserSignIn(
    BrowserDevice Device,
    int Number,
    IReadOnlyList<int> Options,
    string Status,
    long? ApprovedByTelegramUserId,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);

public sealed record BrowserSignInTicket(string Code, string Secret, BrowserSignIn SignIn);

public sealed record BrowserSession(
    string Id,
    long TelegramUserId,
    BrowserDevice Device,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc,
    DateTime ExpiresAtUtc);

public sealed record BrowserSessionTicket(string Token, BrowserSession Session);
