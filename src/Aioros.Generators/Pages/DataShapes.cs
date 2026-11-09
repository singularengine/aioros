using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Pages;

internal sealed class DataShapes(Compilation compilation)
{
    private const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";
    private const string DerivedAttribute = "System.Text.Json.Serialization.JsonDerivedTypeAttribute";
    private const string PolymorphicAttribute = "System.Text.Json.Serialization.JsonPolymorphicAttribute";
    private const string Attributes = "mark it [JsonPolymorphic] and list each concrete type with [JsonDerivedType(typeof(T), \"name\")] (System.Text.Json)";

    private static readonly Dictionary<SpecialType, (string Write, string Read)> Specials = new()
    {
        [SpecialType.System_Boolean] = ("w.WriteBooleanValue({0});", "{0}.GetBoolean()"),
        [SpecialType.System_Byte] = ("w.WriteNumberValue({0});", "{0}.GetByte()"),
        [SpecialType.System_SByte] = ("w.WriteNumberValue({0});", "{0}.GetSByte()"),
        [SpecialType.System_Int16] = ("w.WriteNumberValue({0});", "{0}.GetInt16()"),
        [SpecialType.System_UInt16] = ("w.WriteNumberValue({0});", "{0}.GetUInt16()"),
        [SpecialType.System_Int32] = ("w.WriteNumberValue({0});", "{0}.GetInt32()"),
        [SpecialType.System_UInt32] = ("w.WriteNumberValue({0});", "{0}.GetUInt32()"),
        [SpecialType.System_Int64] = ("w.WriteNumberValue({0});", "{0}.GetInt64()"),
        [SpecialType.System_UInt64] = ("w.WriteNumberValue({0});", "{0}.GetUInt64()"),
        [SpecialType.System_Single] = ("w.WriteNumberValue({0});", "{0}.GetSingle()"),
        [SpecialType.System_Double] = ("w.WriteNumberValue({0});", "{0}.GetDouble()"),
        [SpecialType.System_Decimal] = ("w.WriteNumberValue({0});", "{0}.GetDecimal()"),
        [SpecialType.System_String] = ("w.WriteStringValue({0});", "{0}.GetString()!"),
        [SpecialType.System_Char] = ("w.WriteStringValue({0}.ToString());", "{0}.GetString()![0]"),
        [SpecialType.System_DateTime] = ("w.WriteStringValue({0});", "{0}.GetDateTime()"),
    };

    private static readonly Dictionary<string, (string Write, string Read)> Named = new(StringComparer.Ordinal)
    {
        ["System.Guid"] = ("w.WriteStringValue({0});", "{0}.GetGuid()"),
        ["System.DateTimeOffset"] = ("w.WriteStringValue({0});", "{0}.GetDateTimeOffset()"),
        ["System.DateOnly"] = ("w.WriteStringValue({0}.ToString(\"O\", " + Invariant + "));", "global::System.DateOnly.ParseExact({0}.GetString()!, \"O\", " + Invariant + ")"),
        ["System.TimeOnly"] = ("w.WriteStringValue({0}.ToString(\"O\", " + Invariant + "));", "global::System.TimeOnly.ParseExact({0}.GetString()!, \"O\", " + Invariant + ")"),
        ["System.TimeSpan"] = ("w.WriteStringValue({0}.ToString(\"c\", " + Invariant + "));", "global::System.TimeSpan.ParseExact({0}.GetString()!, \"c\", " + Invariant + ")"),
    };

    private static readonly Dictionary<string, string> Sequences = new(StringComparer.Ordinal)
    {
        ["System.Collections.Generic.List`1"] = "new {0}(items)",
        ["System.Collections.Generic.HashSet`1"] = "new {0}(items)",
        ["System.Collections.Generic.IEnumerable`1"] = "items",
        ["System.Collections.Generic.IReadOnlyList`1"] = "items",
        ["System.Collections.Generic.IReadOnlyCollection`1"] = "items",
        ["System.Collections.Generic.IList`1"] = "items",
        ["System.Collections.Generic.ICollection`1"] = "items",
        ["System.Collections.Generic.ISet`1"] = "new global::System.Collections.Generic.HashSet<{1}>(items)",
        ["System.Collections.Generic.IReadOnlySet`1"] = "new global::System.Collections.Generic.HashSet<{1}>(items)",
        ["System.Collections.Immutable.ImmutableArray`1"] = "global::System.Collections.Immutable.ImmutableArray.Create(items)",
        ["System.Collections.Immutable.ImmutableList`1"] = "global::System.Collections.Immutable.ImmutableList.Create(items)",
        ["System.Collections.Immutable.ImmutableHashSet`1"] = "global::System.Collections.Immutable.ImmutableHashSet.Create(items)",
    };

    private static readonly HashSet<string> Maps = new(StringComparer.Ordinal)
    {
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.IDictionary`2",
        "System.Collections.Generic.IReadOnlyDictionary`2",
    };

    private readonly Dictionary<string, DataShape> _shapes = new(StringComparer.Ordinal);

    public IEnumerable<DataShape> All => _shapes.Values.OrderBy(shape => shape.Id);

    public DataShape Of(ITypeSymbol type)
    {
        type = Bare(type);
        return Shape(Key(type), Kind(type), type);
    }

    public string? Problem(ITypeSymbol type) => Problem(Of(type), type.Name, []);

    public string Describe(ITypeSymbol type) => Describe(Of(type), []);

    public static string Literal(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public static int Depth(ITypeSymbol type)
    {
        var depth = 0;
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            depth++;
        }

        return depth;
    }

    private DataShape RecordOf(ITypeSymbol type)
    {
        type = Bare(type);
        return Kind(type) == ShapeKind.Union ? Shape(Key(type) + "#record", ShapeKind.Record, type) : Of(type);
    }

    private DataShape Shape(string key, ShapeKind kind, ITypeSymbol type)
    {
        if (_shapes.TryGetValue(key, out var known))
        {
            return known;
        }

        var shape = new DataShape(kind, type, _shapes.Count);
        _shapes[key] = shape;
        Analyze(shape);
        return shape;
    }

    private static ITypeSymbol Bare(ITypeSymbol type) =>
        type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated ? type.WithNullableAnnotation(NullableAnnotation.NotAnnotated) : type;

    private static string? Problem(DataShape shape, string path, HashSet<DataShape> seen)
    {
        if (shape.Problem is { } problem)
        {
            return "'" + path + "' " + problem;
        }

        if (!seen.Add(shape))
        {
            return null;
        }

        return shape.Kind switch
        {
            ShapeKind.Nullable => Problem(shape.Element!, path, seen),
            ShapeKind.Sequence => Problem(shape.Element!, path + "[]", seen),
            ShapeKind.Map => Problem(shape.Element!, path + "{}", seen),
            ShapeKind.Record => shape.Parameters.Concat(shape.Settable).Select(member => Problem(member.Shape, path + "." + member.Name, seen)).FirstOrDefault(found => found is not null),
            ShapeKind.Union => shape.Derived.Select(derived => Problem(derived.Shape, path, seen))
                .Append(shape.Fallback ? Problem(shape.Element!, path, seen) : null).FirstOrDefault(found => found is not null),
            _ => null,
        };
    }

    private static string Describe(DataShape shape, HashSet<DataShape> seen) => shape.Problem is not null ? shape.Name : shape.Kind switch
    {
        ShapeKind.Nullable => Describe(shape.Element!, seen) + "?",
        ShapeKind.Sequence => "[" + Describe(shape.Element!, seen) + "]",
        ShapeKind.Map => "{" + Describe(shape.Element!, seen) + "}",
        ShapeKind.Record when seen.Add(shape) => shape.Name + "(" + string.Join(",", shape.Parameters.Concat(shape.Settable).Select(member => member.Json + ":" + Describe(member.Shape, seen))) + ")",
        ShapeKind.Union when seen.Add(shape) => shape.Name + "<" + shape.Property + "," + shape.Unknown.ToString(CultureInfo.InvariantCulture) + ">("
            + string.Join(",", shape.Derived.Select(derived => derived.Literal + ":" + Describe(derived.Shape, seen)))
            + (shape.Fallback ? ",*:" + Describe(shape.Element!, seen) : string.Empty) + ")",
        ShapeKind.Value when shape.Type.TypeKind == TypeKind.Enum => shape.Name + "{" + string.Join(",", shape.Type.GetMembers().OfType<IFieldSymbol>().Where(field => field.HasConstantValue).Select(field => field.Name + "=" + Convert.ToString(field.ConstantValue, CultureInfo.InvariantCulture))) + "}",
        _ => shape.Name,
    };

    private static ShapeKind Kind(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } ? ShapeKind.Nullable
            : Specials.ContainsKey(type.SpecialType) || Named.ContainsKey(MetadataName(type)) || type.TypeKind == TypeKind.Enum ? ShapeKind.Value
            : Collection(type) ?? (IsUnion(type) ? ShapeKind.Union : ShapeKind.Record);

    private static ShapeKind? Collection(ITypeSymbol type) =>
        type is IArrayTypeSymbol || (type is INamedTypeSymbol { IsGenericType: true } sequence && Sequences.ContainsKey(MetadataName(sequence.OriginalDefinition))) ? ShapeKind.Sequence
            : type is INamedTypeSymbol { IsGenericType: true } map && Maps.Contains(MetadataName(map.OriginalDefinition)) ? ShapeKind.Map
            : null;

    private static bool IsUnion(ITypeSymbol type) => type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == DerivedAttribute);

    private void Analyze(DataShape shape)
    {
        var type = shape.Type;
        switch (shape.Kind)
        {
            case ShapeKind.Value:
                Value(shape);
                break;
            case ShapeKind.Nullable:
                shape.Element = Of(((INamedTypeSymbol)type).TypeArguments[0]);
                break;
            case ShapeKind.Sequence when type is IArrayTypeSymbol { Rank: > 1 }:
                shape.Problem = "is a multidimensional array; use an array of arrays";
                break;
            case ShapeKind.Sequence:
                var item = type is IArrayTypeSymbol array ? array.ElementType : ((INamedTypeSymbol)type).TypeArguments[0];
                shape.Element = Of(item);
                shape.Item = item.ToDisplayString(DataShape.Annotated);
                shape.Collect = type is IArrayTypeSymbol ? "items" : string.Format(CultureInfo.InvariantCulture, Sequences[MetadataName(type.OriginalDefinition)], shape.Declared, shape.Item);
                break;
            case ShapeKind.Map when ((INamedTypeSymbol)type).TypeArguments[0].SpecialType != SpecialType.System_String:
                shape.Problem = "is a dictionary whose keys are not strings";
                break;
            case ShapeKind.Map:
                shape.Element = Of(((INamedTypeSymbol)type).TypeArguments[1]);
                shape.Item = ((INamedTypeSymbol)type).TypeArguments[1].ToDisplayString(DataShape.Annotated);
                break;
            case ShapeKind.Union:
                Union(shape);
                break;
            default:
                Record(shape);
                break;
        }
    }

    private static void Value(DataShape shape)
    {
        var type = shape.Type;
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } underlying })
        {
            var (write, read) = Specials[underlying.SpecialType];
            var name = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            shape.Write = write.Replace("{0}", "(" + name + "){0}");
            shape.Read = "(" + shape.Name + ")" + read;
            return;
        }

        (shape.Write, shape.Read) = Specials.TryGetValue(type.SpecialType, out var special) ? special : Named[MetadataName(type)];
    }

    private void Union(DataShape shape)
    {
        var type = shape.Type;
        shape.Problem = Unfit(type, polymorphic: true);
        if (shape.Problem is not null)
        {
            return;
        }

        var polymorphic = type.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == PolymorphicAttribute);
        shape.Property = polymorphic?.NamedArguments.FirstOrDefault(argument => argument.Key == "TypeDiscriminatorPropertyName").Value.Value as string ?? "$type";
        shape.Unknown = polymorphic?.NamedArguments.FirstOrDefault(argument => argument.Key == "UnknownDerivedTypeHandling").Value.Value as int? ?? 0;
        var derived = new List<DerivedShape>();
        foreach (var arguments in type.GetAttributes().Where(attribute => attribute.AttributeClass?.ToDisplayString() == DerivedAttribute).Select(attribute => attribute.ConstructorArguments))
        {
            if (arguments.FirstOrDefault().Value is not ITypeSymbol listed)
            {
                continue;
            }

            if ((arguments.Length > 1 ? arguments[1].Value : null) is not { } discriminator)
            {
                shape.Problem = "is '" + type.ToDisplayString() + "', which lists '" + listed.ToDisplayString() + "' with [JsonDerivedType] without a type discriminator; give it one, as in [JsonDerivedType(typeof("
                    + listed.Name + "), \"" + Camel(listed.Name) + "\")]";
                return;
            }

            var text = Convert.ToString(discriminator, CultureInfo.InvariantCulture)!;
            derived.Add(new DerivedShape(text, discriminator is string ? Literal(text) : text, RecordOf(listed)));
        }

        shape.Derived = derived;
        shape.Fallback = !type.IsAbstract && type.TypeKind != TypeKind.Interface;
        if (shape.Fallback)
        {
            shape.Element = RecordOf(type);
        }
    }

    private void Record(DataShape shape)
    {
        var type = shape.Type;
        shape.Problem = Unfit(type, polymorphic: false);
        if (shape.Problem is not null || type is not INamedTypeSymbol named)
        {
            return;
        }

        var members = Members(named);
        var constructors = named.InstanceConstructors
            .Where(candidate => candidate.DeclaredAccessibility == Accessibility.Public && !IsCopy(candidate, named))
            .OrderByDescending(candidate => candidate.Parameters.Length)
            .ToList();
        if (constructors.Count == 0)
        {
            shape.Problem = "is '" + type.ToDisplayString() + "', which has no public constructor";
            return;
        }

        var constructor = constructors.FirstOrDefault(candidate => candidate.Parameters.All(parameter => Match(members, parameter).Name is not null)) ?? constructors[0];
        var parameters = new List<DataMember>();
        foreach (var parameter in constructor.Parameters)
        {
            var member = Match(members, parameter);
            if (member.Name is null)
            {
                shape.Problem = "is '" + type.ToDisplayString() + "', whose constructor parameter '" + parameter.Name + "' matches no public property of the same type";
                return;
            }

            parameters.Add(new DataMember(member.Name, Camel(member.Name), Of(member.Type)));
        }

        shape.Parameters = parameters;
        shape.Settable = [.. members
            .Where(member => member.Settable && !parameters.Any(parameter => parameter.Name == member.Name))
            .Select(member => new DataMember(member.Name, Camel(member.Name), Of(member.Type)))];
    }

    private string? Unfit(ITypeSymbol type, bool polymorphic)
    {
        var display = type.ToDisplayString();
        if (type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic)
        {
            return "is 'object', whose shape is not known at build time; use a record or a list of one";
        }

        if (type.TypeKind == TypeKind.Delegate)
        {
            return "is a delegate, which is not data";
        }

        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface) || type.IsRefLikeType)
        {
            return "is '" + display + "', which cannot be written as JSON";
        }

        if (!polymorphic && type.TypeKind is TypeKind.Interface)
        {
            return "is '" + display + "', an interface the installed app could not create; use a record, an array or a list, or " + Attributes;
        }

        if (!polymorphic && type.IsAbstract)
        {
            return "is '" + display + "', an abstract type the installed app could not create; " + Attributes + ", or use a sealed record";
        }

        if (type.ContainingNamespace?.ToDisplayString() is { } space && (space == "System" || space.StartsWith("System.", StringComparison.Ordinal) || space.StartsWith("Microsoft.", StringComparison.Ordinal)))
        {
            return "is '" + display + "', a framework type that is not data; use strings, numbers, dates, lists and records";
        }

        return compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)
            ? null
            : "is '" + display + "', which the generated code cannot reach; make it internal or public";
    }

    private static List<(string Name, ITypeSymbol Type, bool Settable)> Members(INamedTypeSymbol type)
    {
        var found = new List<(string Name, ITypeSymbol Type, bool Settable)>();
        for (var current = type; current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); current = current.BaseType)
        {
            foreach (var member in current.GetMembers().Where(member => member.DeclaredAccessibility == Accessibility.Public && !member.IsStatic))
            {
                var entry = member switch
                {
                    IPropertySymbol { IsIndexer: false, ReturnsByRef: false, ReturnsByRefReadonly: false, GetMethod.DeclaredAccessibility: Accessibility.Public } property =>
                        (property.Name, property.Type, property.SetMethod?.DeclaredAccessibility == Accessibility.Public),
                    IFieldSymbol { IsConst: false } field => (field.Name, field.Type, !field.IsReadOnly),
                    _ => (null!, null!, false),
                };
                if (entry.Item1 is not null && !found.Any(known => known.Name == entry.Item1))
                {
                    found.Add(entry);
                }
            }
        }

        return found;
    }

    private static (string Name, ITypeSymbol Type, bool Settable) Match(List<(string Name, ITypeSymbol Type, bool Settable)> members, IParameterSymbol parameter) =>
        members.FirstOrDefault(candidate => string.Equals(candidate.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) && SymbolEqualityComparer.Default.Equals(candidate.Type, parameter.Type));

    private static bool IsCopy(IMethodSymbol constructor, INamedTypeSymbol type) =>
        constructor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, type);

    private static string MetadataName(ITypeSymbol type) =>
        type.ContainingNamespace is null || type.ContainingNamespace.IsGlobalNamespace ? type.MetadataName : type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;

    private static string Key(ITypeSymbol type) => type.ToDisplayString(DataShape.Annotated);

    private static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
}
