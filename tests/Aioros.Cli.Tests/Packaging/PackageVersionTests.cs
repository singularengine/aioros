namespace Aioros.Cli.Tests.Packaging;

public sealed class PackageVersionTests
{
    private static readonly string[] Packable = ["src/Aioros/Aioros.csproj", "templates/Aioros.Templates.csproj"];

    [Fact]
    [Trait("AC", "CLI-41")]
    public async Task TheVersionTakesASuffixAndSamplesAreNotPackedAsync()
    {
        foreach (var project in Packable.Select(static relative => Path.Combine(PackagedBuild.Repository, relative)))
        {
            Assert.Equal("0.1.0-preview.1", await PackagedBuild.PropertyAsync(project, "Version"));
            Assert.Equal("0.1.0-preview.914.abc", await PackagedBuild.PropertyAsync(project, "Version", "-p:VersionSuffix=preview.914.abc"));
            Assert.Equal("1.2.3", await PackagedBuild.PropertyAsync(project, "Version", "-p:Version=1.2.3"));
        }

        var samples = Directory.EnumerateFiles(Path.Combine(PackagedBuild.Repository, "samples"), "*.csproj", SearchOption.AllDirectories)
            .Where(static project => !project.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(samples);
        foreach (var project in samples)
        {
            Assert.Equal("false", await PackagedBuild.PropertyAsync(project, "IsPackable"), ignoreCase: true);
        }
    }
}
