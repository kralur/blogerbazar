namespace BloggerBazar.Application.Exceptions;

// A 409 the client can tell apart from other conflicts by its stable code (snake_case, like other problem codes).
public sealed class BusinessRuleConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
