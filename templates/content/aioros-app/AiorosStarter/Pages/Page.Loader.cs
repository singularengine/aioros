using Aioros.Pages;
using AiorosStarter.Models;

namespace AiorosStarter.Pages;

public sealed class HomeLoader(ClickCounter counter)
{
    public LoaderResult<Clicks> Load() => LoaderResult.With(new Clicks(counter.Count), "Welcome to Aioros");
}
