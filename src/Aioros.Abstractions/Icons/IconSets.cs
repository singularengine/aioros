namespace Aioros.Icons;

/// <summary>
/// A set of icons a library or an app gives the <c>Icon</c> control, declared with <see cref="IconSetAttribute{T}"/>. It
/// lives in the abstractions, which reference nothing, so a control library that references only Avalonia declares
/// its icons without referencing Aioros's runtime or adapter.
/// </summary>
public interface IIconSet
{
    /// <summary>Each kind's SVG markup on a 24-pixel view box, stroked with the icon's foreground.</summary>
    static abstract IEnumerable<KeyValuePair<string, string>> Icons { get; }
}

/// <summary>Declares an icon set of a library or an app, which the app's source generator registers when the host starts.</summary>
/// <typeparam name="T">The icon set.</typeparam>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class IconSetAttribute<T> : Attribute
    where T : IIconSet
{
    /// <summary>The icon set's type.</summary>
    public Type Set => typeof(T);
}
