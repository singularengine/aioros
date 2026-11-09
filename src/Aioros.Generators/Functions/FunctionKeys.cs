using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Functions;

/// <summary>
/// The data keys a server function's implementation declares with <c>[Reads]</c> and <c>[Invalidates]</c>, read at build
/// time: each key becomes code that writes it from the call's arguments, so a key naming an argument that does not exist
/// is a build error rather than a key that never matches.
/// </summary>
internal sealed class FunctionKeys
{
    private const string ReadsName = "Aioros.Functions.ReadsAttribute";
    private const string InvalidatesName = "Aioros.Functions.InvalidatesAttribute";

    private FunctionKeys(bool declared, List<string> reads, List<string> invalidates, List<(Location? Location, string Key, string Problem)> problems)
    {
        Declared = declared;
        Reads = reads;
        Invalidates = invalidates;
        Problems = problems;
    }

    /// <summary>Whether the implementation declares the function's keys; one that does not refreshes the whole page.</summary>
    public bool Declared { get; }

    /// <summary>The C# expressions of the read keys.</summary>
    public List<string> Reads { get; }

    /// <summary>The C# expressions of the invalidated keys.</summary>
    public List<string> Invalidates { get; }

    public List<(Location? Location, string Key, string Problem)> Problems { get; }

    public static FunctionKeys Of(IMethodSymbol method, IEnumerable<INamedTypeSymbol> implementations)
    {
        var attributes = implementations
            .Select(implementation => implementation.FindImplementationForInterfaceMember(method) as IMethodSymbol)
            .Where(member => member is not null)
            .SelectMany(member => member!.GetAttributes())
            .Where(attribute => attribute.AttributeClass?.ToDisplayString() is ReadsName or InvalidatesName)
            .ToList();
        var (reads, invalidates, problems) = (new List<string>(), new List<string>(), new List<(Location?, string, string)>());
        foreach (var attribute in attributes)
        {
            var target = attribute.AttributeClass!.ToDisplayString() == ReadsName ? reads : invalidates;
            var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
            foreach (var key in attribute.ConstructorArguments.SelectMany(argument => argument.Kind == TypedConstantKind.Array ? argument.Values : [argument]).Select(value => value.Value as string))
            {
                var (code, problem) = Expression(key ?? string.Empty, method);
                if (problem is null)
                {
                    target.Add(code);
                }
                else
                {
                    problems.Add((location, key ?? string.Empty, problem));
                }
            }
        }

        return new FunctionKeys(attributes.Count > 0, reads, invalidates, problems);
    }

    /// <summary>
    /// The statements that record the call's keys once the implementation completed, and evict from the data cache what
    /// it invalidated: its keys, or everything for a function without key information called outside a load.
    /// </summary>
    public void Record(StringBuilder code, string indent)
    {
        if (!Declared)
        {
            code.Append(indent).Append("if (keys__ is null)\n").Append(indent).Append("{\n")
                .Append(indent).Append("    global::Aioros.Runtime.Functions.DataKeys.Current?.Undeclared();\n")
                .Append(indent).Append("    cache__?.InvalidateAll();\n").Append(indent).Append("}\n");
            return;
        }

        if (Reads.Count + Invalidates.Count == 0)
        {
            return;
        }

        code.Append(indent).Append("if ((keys__ ?? global::Aioros.Runtime.Functions.DataKeys.Current) is { } recorded__)\n").Append(indent).Append("{\n");
        foreach (var read in Reads)
        {
            code.Append(indent).Append("    recorded__.Read(").Append(read).Append(");\n");
        }

        foreach (var invalidate in Invalidates)
        {
            code.Append(indent).Append("    recorded__.Invalidate(").Append(invalidate).Append(");\n");
        }

        code.Append(indent).Append("}\n");
        foreach (var invalidate in Invalidates)
        {
            code.Append(indent).Append("cache__?.Invalidate(").Append(invalidate).Append(");\n");
        }
    }

    private static (string Code, string? Problem) Expression(string key, IMethodSymbol method)
    {
        if (key.Trim().Length == 0)
        {
            return (string.Empty, "a key cannot be empty");
        }

        var parts = new List<string>();
        var position = 0;
        while (position < key.Length)
        {
            var open = key.IndexOf('{', position);
            var close = key.IndexOf('}', position);
            if (close >= 0 && (open < 0 || close < open))
            {
                return (string.Empty, "it closes a placeholder it never opened");
            }

            if (open < 0)
            {
                parts.Add(Literal(key.Substring(position)));
                break;
            }

            if (open > position)
            {
                parts.Add(Literal(key.Substring(position, open - position)));
            }

            if (close < 0)
            {
                return (string.Empty, "a placeholder is left open");
            }

            var (value, problem) = Placeholder(key.Substring(open + 1, close - open - 1), method);
            if (problem is not null)
            {
                return (string.Empty, problem);
            }

            parts.Add(value);
            position = close + 1;
        }

        return (string.Join(" + ", parts), null);
    }

    private static (string Code, string? Problem) Placeholder(string path, IMethodSymbol method)
    {
        var names = path.Split('.');
        var parameter = method.Parameters.FirstOrDefault(candidate => candidate.Name == names[0] && candidate.Type.ToDisplayString() != "System.Threading.CancellationToken");
        if (parameter is null)
        {
            return (string.Empty, $"'{names[0]}' is no parameter of '{method.Name}'");
        }

        var access = new StringBuilder(Escape(parameter.Name));
        var type = parameter.Type;
        foreach (var name in names.Skip(1))
        {
            var member = Members(type).FirstOrDefault(candidate => candidate.Name == name);
            if (member is null)
            {
                return (string.Empty, $"'{type.ToDisplayString()}' has no member '{name}'");
            }

            access.Append(type.IsValueType && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T ? "." : "?.").Append(Escape(name));
            type = member is IPropertySymbol property ? property.Type : ((IFieldSymbol)member).Type;
        }

        return ("global::System.Convert.ToString(" + access + ", global::System.Globalization.CultureInfo.InvariantCulture)", null);
    }

    private static IEnumerable<ISymbol> Members(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers().Where(member => !member.IsStatic && member.DeclaredAccessibility == Accessibility.Public && member is IPropertySymbol { Parameters.Length: 0 } or IFieldSymbol))
            {
                yield return member;
            }
        }
    }

    private static string Literal(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string Escape(string name) => Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(name) == Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ? name : "@" + name;
}
