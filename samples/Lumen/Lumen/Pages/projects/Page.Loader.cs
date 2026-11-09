using Aioros.Pages;
using Lumen.Data;
using Lumen.ViewModels;

namespace Lumen.Pages.Projects;

public sealed class ProjectsLoader(IIssueFunctions issues)
{
    public async Task<LoaderResult<IssueList>> LoadAsync() =>
        LoaderResult.With(new IssueList([.. await issues.ListAsync()]), "Projects · Lumen", new PageMetadata("The v2 launch board, column by column.", "/projects"));
}
