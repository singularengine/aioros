using Aioros.Pages;

namespace Lumen.Pages;

public static class HomeLoader
{
    public static LoaderOutcome Load() => LoaderResult.Redirect("/issues");
}
