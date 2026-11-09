using System.Collections.Immutable;
using Aioros.Avalonia.Controls;
using Aioros.Pages;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopDropTests(DesktopFixture fixture)
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "DRAG-09")]
    public async Task DroppedItemsRunTheirCommandAndRevalidateAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var books = ImmutableList.Create("Dune", "Emma", "Ulysses");
        var moves = new List<(string, string, int)>();
        Task MoveAsync((string Item, string Target, int Index) move)
        {
            moves.Add(move);
            books = books.Remove(move.Item).Insert(move.Index, move.Item);
            return Task.CompletedTask;
        }

        var page = new PageDefinition("/shelf", "Pages/shelf/Page.axaml", static _ => null, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)])
        {
            Loader = new LoaderDefinition((_, _, _) => ValueTask.FromResult(new LoaderOutcome(new ShelfData(books))), (_, data) => new Shelving((ShelfData)data!, MoveAsync))
            {
                Receive = static (model, data) => ((Shelving)model).Receive((ShelfData)data!),
            },
        };
        var (revalidated, texts) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [page]);
            await navigator.NavigateAsync("/shelf");
            DesktopFixture.Settle(window);
            window.CaptureRenderedFrame();
            var first = window.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Dune");
            var top = first.TranslatePoint(new Point(first.Bounds.Width / 2, 1), window)!.Value;
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(Drag.Format, "Ulysses"));
            var settled = DesktopViewCommandTests.NextSettledAsync(navigator, bound);
            foreach (var type in new[] { RawDragEventType.DragEnter, RawDragEventType.DragOver, RawDragEventType.Drop })
            {
                window.DragDrop(top, type, data, DragDropEffects.Move, RawInputModifiers.None);
            }

            var result = (await settled, DesktopFixture.Texts(window).ToList());
            window.Close();
            return result;
        });

        Assert.True(revalidated);
        Assert.Equal([("Ulysses", "shelf", 0)], moves);
        Assert.Equal(["Ulysses", "Dune", "Emma"], texts[^3..]);
    }
}
