using System.Text.Json;

namespace Aioros.Runtime.Functions;

/// <summary>Finds the arguments of a server function in the JSON object of a call, for the generated readers.</summary>
public static class ServerFunctionArguments
{
    public static bool TryGet(JsonElement arguments, string name, out JsonElement value)
    {
        value = default;
        return arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Undefined;
    }
}
