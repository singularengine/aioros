using Aioros.Pages;
using Showcase.Games;

namespace Showcase.Pages.Games;

public sealed class GameLoader(IGameStore store)
{
    public async Task<LoaderResult<GameData>> LoadAsync(int id) =>
        await store.GameAsync(id) is { } detail
            ? LoaderResult.With(new GameData(id, detail), detail.Card.Title + " · Aioros Games", new PageMetadata($"{detail.Card.Title} by {detail.Card.Studio}: {detail.Summary}"))
            : LoaderResult.NotFound();
}
