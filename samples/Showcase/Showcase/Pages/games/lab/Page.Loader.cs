using Aioros.Pages;
using Showcase.Games;

namespace Showcase.Pages.Games.Lab;

public sealed class LabLoader(ILabFunctions lab)
{
    public async Task<LoaderResult<LabSnapshot>> LoadAsync() =>
        LoaderResult.With(
            await lab.SnapshotAsync(),
            "Concurrency lab · Aioros Games",
            new PageMetadata("Hundreds of virtual players browse, search, buy and review at once through EF Core, live."));
}
