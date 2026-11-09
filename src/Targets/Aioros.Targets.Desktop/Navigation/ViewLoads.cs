using Aioros.Runtime.Rendering;

namespace Aioros.Targets.Desktop.Navigation;

/// <summary>
/// The refreshes of the shown page's views, one per view instance (or item of a repeated one): a newer refresh of the same view cancels the older one,
/// so the newest wins per instance (React Router aborts a stale load), and a navigation cancels them all. Owned by the
/// navigator on the UI thread.
/// </summary>
internal sealed class ViewLoads
{
    private readonly Dictionary<ViewTarget, CancellationTokenSource> _loading = [];

    public async Task<CancellationTokenSource> StartAsync(ViewTarget view)
    {
        if (_loading.Remove(view, out var older))
        {
            await older.CancelAsync();
            older.Dispose();
        }

        var loading = new CancellationTokenSource();
        _loading[view] = loading;
        return loading;
    }

    public void Finish(ViewTarget view, CancellationTokenSource loading)
    {
        if (_loading.TryGetValue(view, out var current) && ReferenceEquals(current, loading))
        {
            _loading.Remove(view);
        }

        loading.Dispose();
    }

    public void CancelAll()
    {
        foreach (var loading in _loading.Values)
        {
            loading.Cancel();
            loading.Dispose();
        }

        _loading.Clear();
    }
}
