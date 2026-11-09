using Aioros.Pages;
using Showcase.Models;

namespace Showcase.Pages.Finance;

public sealed class FinanceLoader(IFinanceFunctions finance)
{
    public async Task<LoaderResult<FinanceData>> LoadAsync() =>
        LoaderResult.With(
            await finance.SnapshotAsync(),
            "Finance · Aioros Showcase",
            new PageMetadata("A live financial dashboard written in XAML: prices, candlesticks, a portfolio and trades."));
}
