namespace BloggerBazar.Application.Abstractions.Security;

public interface IPlatformUserAccessPolicy
{
    void EnsureActive(long telegramUserId);
}
