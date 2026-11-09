using Aioros.Generated;
using Aioros.Pages;
using Showcase.Board;
using Showcase.Models;

namespace Showcase.Pages.Kanban;

public sealed class KanbanLoader(IBoardFunctions board)
{
    public async Task<LoaderResult<KanbanData>> LoadAsync() =>
        LoaderResult.With(
            KanbanData.Of(await board.LoadAsync(), AiorosSources.Text("Pages/kanban/Page.axaml") ?? string.Empty),
            "Kanban · Aioros Showcase",
            new PageMetadata("A kanban board in plain XAML: typed templates, converters, multi-bindings, bound classes, animations and commands, rendered on the server."));
}
