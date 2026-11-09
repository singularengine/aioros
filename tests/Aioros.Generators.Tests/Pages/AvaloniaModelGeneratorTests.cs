namespace Aioros.Generators.Tests.Pages;

public sealed class AvaloniaModelGeneratorTests
{
    private const string Avalonia = """
        namespace Avalonia
        {
            public class AvaloniaObject { }
            public struct Thickness { public Thickness(double uniform) { } }
        }

        namespace Avalonia.Media
        {
            public struct Color { public static Color Parse(string text) => default; }
            public abstract class Geometry : Avalonia.AvaloniaObject { public static Geometry Parse(string data) => null!; }
            public sealed class SolidColorBrush : Avalonia.AvaloniaObject { public SolidColorBrush(Color color) { } }
        }

        namespace Avalonia.Media.Imaging
        {
            public class Bitmap { public Bitmap(System.IO.Stream stream) { } }
        }

        namespace Avalonia.Platform
        {
            public static class AssetLoader { public static System.IO.Stream Open(System.Uri uri) => null!; }
        }

        namespace Avalonia.Threading
        {
            public sealed class DispatcherTimer { }
        }

        namespace Avalonia.Input
        {
            public sealed class KeyGesture { public static KeyGesture Parse(string gesture) => null!; }
        }

        namespace Avalonia.Data.Converters
        {
            public sealed class FuncValueConverter<TIn, TOut>(System.Func<TIn?, TOut> convert) { }
        }
        """;

    private const string Models = """
        using Avalonia;
        using Avalonia.Media;

        namespace App.Models
        {
            public sealed class GeometryModel
            {
                private static readonly Geometry Logo = Geometry.Parse("M0 0h1");
                public bool HasLogo => Logo is not null;
            }

            public sealed class BrushModel
            {
                public BrushModel() => Fill = new SolidColorBrush(Color.Parse("#fff"));
                public object Fill { get; }
            }

            public static class Icons
            {
                public static object Make() => Geometry.Parse("M1 1h1");
            }

            public sealed class HelperModel
            {
                public object Icon { get; } = Icons.Make();
            }

            public sealed class ShellModel
            {
                static ShellModel() => Ticks = new Avalonia.Threading.DispatcherTimer();
                public static object Ticks { get; private set; } = null!;
            }

            public sealed class PictureModel
            {
                public object Picture { get; } = new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(new System.Uri("avares://App/a.png")));
            }

            public sealed record PictureData(string Name);

            public sealed class LoadedModel(PictureData data) { public PictureData Data { get; } = data; }

            public sealed class ValuesModel
            {
                public Color Accent { get; } = Color.Parse("#f00");
                public Thickness Margin { get; } = new(4);
                public object Gesture { get; } = Avalonia.Input.KeyGesture.Parse("Ctrl+D1");
                public static readonly object Picture = new Avalonia.Data.Converters.FuncValueConverter<string, object>(path => new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(new System.Uri(path!))));
            }

            public sealed class PlainModel { public string Name { get; } = "plain"; }
        }

        namespace App.Views
        {
            public partial class Gallery { }
        }
        """;

    private const string PictureLoader = """
        using System.Threading;
        using System.Threading.Tasks;
        using Aioros.Pages;
        namespace App.Loaders;
        public sealed class PictureLoader : ILoader<App.Models.PictureData>
        {
            private readonly object _icon = Avalonia.Media.Geometry.Parse("M2 2h1");
            public ValueTask<LoaderResult<App.Models.PictureData>> LoadAsync(PageRequest request, CancellationToken cancellationToken) =>
                ValueTask.FromResult<LoaderResult<App.Models.PictureData>>(new App.Models.PictureData(_icon.ToString()!));
        }
        """;

    [Fact]
    [Trait("AC", "GENERATOR-119")]
    public void PagesWhoseModelsCreateAvaloniaObjectsAreMarked()
    {
        var result = new GeneratorHarness()
            .Library("Avalonia.Base", Avalonia, Libraries.Framework)
            .Source(Models)
            .Source(PictureLoader, "Pages/loaded/Page.Loader.cs")
            .Xaml("Pages/geometry/Page.axaml", "<TextBlock />", "GeometryModel")
            .Xaml("Pages/brush/Page.axaml", "<TextBlock />", "BrushModel")
            .Xaml("Pages/helper/Page.axaml", "<TextBlock />", "HelperModel")
            .Xaml("Pages/shell/Layout.axaml", "<a:Outlet />", "ShellModel")
            .Xaml("Pages/shell/inner/Page.axaml", "<TextBlock />", "PlainModel")
            .Xaml("Views/Gallery.axaml", "<TextBlock />", "PictureModel", " x:Class=\"App.Views.Gallery\"")
            .Xaml("Pages/viewed/Page.axaml", "<v:Gallery />", "PlainModel", " xmlns:v=\"using:App.Views\"")
            .Xaml("Pages/loaded/Page.axaml", "<TextBlock />", "LoadedModel")
            .Xaml("Pages/values/Page.axaml", "<TextBlock />", "ValuesModel")
            .Xaml("Pages/plain/Page.axaml", "<TextBlock />", "PlainModel")
            .Run();
        var marked = result.Pages(out _).Where(page => page.ModelsUseAvalonia).Select(page => page.Route).Order(StringComparer.Ordinal);

        Assert.Empty(result.WarningsAsErrors());
        Assert.Equal(["/brush", "/geometry", "/helper", "/loaded", "/shell/inner", "/viewed"], marked);
    }
}
