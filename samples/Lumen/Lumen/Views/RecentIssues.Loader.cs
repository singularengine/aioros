using Lumen.Data;
using Lumen.ViewModels;

namespace Lumen.Views;

public sealed class RecentIssuesLoader(IRecentFunctions recent)
{
    public async Task<RecentIssuesData> LoadAsync() => new(await recent.RecentAsync(), await recent.UnreadAsync());
}
