namespace BloggerBazar.Application.Exceptions;

public sealed class ProfileMediaValidationException : Exception
{
    public ProfileMediaValidationException() : base("The uploaded profile image is invalid.")
    {
    }
}
