using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Pages;

internal enum ShapeKind
{
    Value,
    Nullable,
    Sequence,
    Map,
    Record,
    Union,
}

internal sealed class DataMember(string name, string json, DataShape shape)
{
    public string Name { get; } = name;

    public string Json { get; } = json;

    public DataShape Shape { get; } = shape;
}

internal sealed class DerivedShape(string discriminator, string literal, DataShape shape)
{
    public string Discriminator { get; } = discriminator;

    public string Literal { get; } = literal;

    public DataShape Shape { get; } = shape;
}

internal sealed class DataShape(ShapeKind kind, ITypeSymbol type, int id)
{
    public static readonly SymbolDisplayFormat Annotated = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
        SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public ShapeKind Kind { get; } = kind;

    public ITypeSymbol Type { get; } = type;

    public int Id { get; } = id;

    public string Write { get; set; } = string.Empty;

    public string Read { get; set; } = string.Empty;

    public string Collect { get; set; } = string.Empty;

    public DataShape? Element { get; set; }

    public string Item { get; set; } = string.Empty;

    public IReadOnlyList<DataMember> Parameters { get; set; } = [];

    public IReadOnlyList<DataMember> Settable { get; set; } = [];

    public string Property { get; set; } = "$type";

    public IReadOnlyList<DerivedShape> Derived { get; set; } = [];

    public int Unknown { get; set; }

    public bool Fallback { get; set; }

    public string? Problem { get; set; }

    public string Name => Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public string Declared => Type.ToDisplayString(Annotated);
}
