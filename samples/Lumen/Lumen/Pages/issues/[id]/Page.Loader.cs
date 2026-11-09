using Aioros.Functions;
using Aioros.Pages;
using Lumen.Data;
using Lumen.ViewModels;

namespace Lumen.Pages.Issues;

[Cached(60)]
public sealed class IssueLoader(IIssueFunctions issues)
{
    public async Task<LoaderResult<IssueData>> LoadAsync(string id) =>
        await issues.FindAsync(id) is { } issue
            ? LoaderResult.With(new IssueData(issue), $"{issue.Id} · {issue.Title}", new PageMetadata(issue.Description, "/issues/" + issue.Id, null, "article"))
            : LoaderResult.NotFound();
}
