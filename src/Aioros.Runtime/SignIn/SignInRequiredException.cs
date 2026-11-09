namespace Aioros.Runtime.SignIn;

/// <summary>
/// The server answered <c>401</c> to the installed app even after its token was refreshed: the user must sign in. It is an
/// <see cref="UnauthorizedAccessException"/>, so a view model that catches refusals catches it without referencing Aioros.
/// </summary>
public sealed class SignInRequiredException : UnauthorizedAccessException
{
    public SignInRequiredException()
        : base("Sign in to continue.")
    {
    }

    public SignInRequiredException(string message)
        : base(message)
    {
    }

    public SignInRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
