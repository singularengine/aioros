using Aioros.Functions;
using Aioros.Pages;
using Lumen.Data;
using Lumen.ViewModels;

namespace Lumen.Pages.Issues;

[Cached(60)]
public sealed class IssuesLoader(IIssueFunctions issues)
{
    public async Task<LoaderResult<IssueList>> LoadAsync() =>
        LoaderResult.With(new IssueList([.. await issues.ListAsync()]), "Issues · Lumen", new PageMetadata("Every issue of the team, grouped by status.", "/issues"));
}
