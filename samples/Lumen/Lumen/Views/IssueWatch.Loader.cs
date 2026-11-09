using Aioros.Pages;
using Lumen.Data;

namespace Lumen.Views;

/// Loads the activity of every issue listed in Recent issues in one call (DataLoader, Phoenix update_many), instead of one
/// call per row.
public sealed class IssueWatchLoader(IRecentFunctions recent) : IBatchLoader<string, IssueActivity>
{
    public async ValueTask<IReadOnlyDictionary<string, IssueActivity>> LoadAsync(IReadOnlyList<string> keys, PageRequest request, CancellationToken cancellationToken) =>
        (await recent.ActivityAsync([.. keys])).ToDictionary(activity => activity.Id);
}
