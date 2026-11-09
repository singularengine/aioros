using Aioros.Pages;
using ViewScale.Data;

namespace ViewScale.Views;

public sealed class RowsLoader(IRowSource rows) : ILoader<RowsData>
{
    public async ValueTask<LoaderResult<RowsData>> LoadAsync(PageRequest request, CancellationToken cancellationToken)
    {
        var offset = Random.Shared.Next(RowSet.Total - RowSet.PageSize);
        return new LoaderResult<RowsData>(new RowsData(offset, await rows.PageAsync(offset, cancellationToken)));
    }
}
