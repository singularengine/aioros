using Aioros.Pages;
using Showcase.Games;

namespace Showcase.Pages.Games;

public sealed class LibraryLoader(IGameStore store)
{
    public async Task<LoaderResult<LibraryPage>> LoadAsync() => LoaderResult.With(await store.LibraryAsync(), "Library · Aioros Games");
}
