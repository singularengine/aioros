using System.Linq;
using System.Text;

namespace Aioros.Generators.Pages;

/// <summary>
/// Emits the writer and reader of a type marked with System.Text.Json's <c>[JsonDerivedType]</c>: the discriminator
/// property first, then the runtime type's members, as System.Text.Json writes them.
/// </summary>
internal static class DataUnions
{
    private const string Element = "global::System.Text.Json.JsonElement";
    private const string Kinds = "global::System.Text.Json.JsonValueKind.";

    public static void Emit(StringBuilder code, DataShape shape)
    {
        var property = DataShapes.Literal(shape.Property);
        code.Append("        if (v is null)\n        {\n            w.WriteNullValue();\n            return;\n        }\n\n");
        code.Append("        var type = v.GetType();\n");
        foreach (var derived in shape.Derived)
        {
            code.Append("        if (type == typeof(").Append(derived.Shape.Name).Append("))\n        {\n");
            Tagged(code, derived, property);
            code.Append("            return;\n        }\n\n");
        }

        if (shape.Fallback)
        {
            code.Append("        if (type == typeof(").Append(shape.Name).Append("))\n        {\n");
            Untagged(code, shape);
            code.Append("            return;\n        }\n\n");
        }

        if (shape.Unknown == 2)
        {
            foreach (var derived in shape.Derived.OrderByDescending(derived => DataShapes.Depth(derived.Shape.Type)))
            {
                code.Append("        if (v is ").Append(derived.Shape.Name).Append(")\n        {\n");
                Tagged(code, derived, property);
                code.Append("            return;\n        }\n\n");
            }
        }

        if (shape.Unknown != 0 && shape.Fallback)
        {
            Untagged(code, shape);
        }
        else
        {
            code.Append("        throw new global::System.NotSupportedException(\"Runtime type '\" + type + \"' is not supported by polymorphic type '")
                .Append(shape.Type.ToDisplayString()).Append("'.\");\n");
        }

        code.Append("    }\n");
        Reader(code, shape, property);
    }

    private static void Tagged(StringBuilder code, DerivedShape derived, string property)
    {
        code.Append("            w.WriteStartObject();\n");
        code.Append(derived.Literal.StartsWith("\"", System.StringComparison.Ordinal) ? "            w.WriteString(" : "            w.WriteNumber(")
            .Append(property).Append(", ").Append(derived.Literal).Append(");\n");
        code.Append("            P").Append(derived.Shape.Id).Append("(w, (").Append(derived.Shape.Declared).Append(")v);\n");
        code.Append("            w.WriteEndObject();\n");
    }

    private static void Untagged(StringBuilder code, DataShape shape)
    {
        code.Append("            w.WriteStartObject();\n            P").Append(shape.Element!.Id).Append("(w, v);\n            w.WriteEndObject();\n");
    }

    private static void Reader(StringBuilder code, DataShape shape, string property)
    {
        code.Append("\n    internal static ").Append(shape.Declared).Append(" R").Append(shape.Id).Append('(').Append(Element).Append(" e)\n    {\n");
        code.Append("        if (e.ValueKind == ").Append(Kinds).Append("Null)\n        {\n            return null!;\n        }\n\n");
        code.Append("        if (e.TryGetProperty(").Append(property).Append(", out var kind))\n        {\n");
        foreach (var derived in shape.Derived)
        {
            code.Append(derived.Literal.StartsWith("\"", System.StringComparison.Ordinal)
                    ? "            if (kind.ValueKind == " + Kinds + "String && kind.ValueEquals(" + derived.Literal + "))\n"
                    : "            if (kind.ValueKind == " + Kinds + "Number && kind.TryGetInt32(out var number" + derived.Shape.Id + ") && number" + derived.Shape.Id + " == " + derived.Literal + ")\n")
                .Append("            {\n                return R").Append(derived.Shape.Id).Append("(e);\n            }\n\n");
        }

        code.Append("            throw new global::System.Text.Json.JsonException(\"The type discriminator '\" + kind.ToString() + \"' names no type derived from '")
            .Append(shape.Type.ToDisplayString()).Append("'.\");\n        }\n\n");
        code.Append(shape.Fallback
            ? "        return R" + shape.Element!.Id + "(e);\n"
            : "        throw new global::System.Text.Json.JsonException(\"A '" + shape.Type.ToDisplayString() + "' needs its type discriminator.\");\n");
        code.Append("    }\n");
    }
}
