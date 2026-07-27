namespace BloggerBazar.Application.Abstractions.Security;

public interface IAdminAccessPolicy
{
    void EnsureAllowed(long telegramUserId);
}
