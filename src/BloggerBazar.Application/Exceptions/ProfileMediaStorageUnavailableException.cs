namespace BloggerBazar.Application.Exceptions;

public sealed class ProfileMediaStorageUnavailableException : Exception
{
    public ProfileMediaStorageUnavailableException() : base("Profile media storage is unavailable.")
    {
    }
}
