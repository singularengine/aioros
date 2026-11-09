using Aioros.Pages;
using Showcase.Games;

namespace Showcase.Pages.Login;

public static class SignInLoader
{
    public static LoaderResult<SignInData> Load(string? returnUrl) =>
        LoaderResult.With(
            new SignInData(returnUrl is [ '/', not ('/' or '\\'), ..] ? returnUrl : "/games"),
            "Sign in · Aioros Games",
            new PageMetadata("Sign in to the Aioros Games demo with ASP.NET Core Identity."));
}
