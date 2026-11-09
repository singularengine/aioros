using Avalonia.Media;

namespace ModelThreads.Models;

/// <summary>The home page's model: a name its field edits, which carries a control's state across posts.</summary>
public sealed class HomeModel
{
    /// <summary>The name the field edits.</summary>
    public string Name { get; set; } = "Ada";
}

/// <summary>A model whose static field parses a geometry, which needs Avalonia's platform.</summary>
public sealed class GeometryModel
{
    private static readonly Geometry Logo = Geometry.Parse("M0 0h10v10z");

    /// <summary>What the page shows.</summary>
    public string Label => Logo.Bounds.Width > 0 ? "geometry" : "empty";
}

/// <summary>A model whose constructor creates a brush, an Avalonia object.</summary>
public sealed class BrushModel
{
    /// <summary>Creates the model with its brush.</summary>
    public BrushModel() => Fill = new SolidColorBrush(Colors.Teal);

    /// <summary>The brush.</summary>
    public SolidColorBrush Fill { get; }

    /// <summary>What the page shows.</summary>
    public string Label => "brush";
}

/// <summary>Makes the service page's icon.</summary>
public interface IIcons
{
    /// <summary>The icon's name, after making a geometry or a brush on the caller's thread when <c>MODEL_THREADS_ICON</c> says so.</summary>
    string Make();
}

/// <summary>The app's icons, which make an Avalonia object on the caller's thread when asked to.</summary>
public sealed class Icons : IIcons
{
    /// <inheritdoc />
    public string Make() => Environment.GetEnvironmentVariable("MODEL_THREADS_ICON") switch
    {
        "geometry" => Geometry.Parse("M0 0h1").GetType().Name,
        "brush" => new SolidColorBrush(Colors.Teal).GetType().Name,
        _ => "icon",
    };
}

/// <summary>A model that gets its icon from a service, which the generator cannot see into.</summary>
public sealed class ServiceModel(IIcons icons)
{
    /// <summary>What the page shows.</summary>
    public string Label { get; } = icons.Make();
}
