using Aioros.Pages;
using Lumen.Data;

namespace Lumen.Pages.Dashboard;

public sealed class DashboardLoader(IDashboardFunctions dashboard)
{
    public async Task<LoaderResult<DashboardData>> LoadAsync() =>
        LoaderResult.With(await dashboard.SnapshotAsync(), "Dashboard", new PageMetadata("Real-time sales, orders and traffic.", "/dashboard"));
}
