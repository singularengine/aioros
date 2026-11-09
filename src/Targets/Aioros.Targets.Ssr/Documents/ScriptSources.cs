namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The framework's browser scripts as the build minified them with esbuild from their readable sources (decision
/// "Readable framework scripts"), embedded in this assembly by name: <c>app</c> for the framework's script, the module's
/// name for each module.
/// </summary>
internal static class ScriptSources
{
    public static string Read(string name)
    {
        using var stream = typeof(ScriptSources).Assembly.GetManifestResourceStream($"Aioros.Targets.Ssr.Scripts.{name}.js")
            ?? throw new InvalidOperationException($"The script '{name}' was not embedded: build Aioros.Targets.Ssr with its scripts (eng/install-scripts.sh).");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
