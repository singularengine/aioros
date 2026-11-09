using System.Diagnostics;
using System.Xml.Linq;

namespace Aioros.Cli.Tests.Installer;

public sealed class MacBundleTests
{
    [Fact]
    [Trait("AC", "INSTALL-08")]
    public async Task TheMacBundleDeclaresTheSchemeAsync()
    {
        var work = Directory.CreateTempSubdirectory("aioros-plist-");
        try
        {
            var targets = Path.Combine(Root(AppContext.BaseDirectory), "src", "Aioros", "build", "Aioros.targets");
            var shop = await ProjectAsync(work.FullName, "Shop", targets, "<AiorosScheme>shop</AiorosScheme><AiorosBundleId>dev.example.shop</AiorosBundleId>");
            var plain = await ProjectAsync(work.FullName, "Plain", targets, string.Empty);

            var mac = await BundleAsync(shop, "osx-arm64", "1.2.0-beta.1");
            var linux = await BundleAsync(shop, "linux-x64", "1.2.0");
            var unschemed = await BundleAsync(plain, "osx-arm64", "1.2.0");

            Assert.NotNull(mac);
            var values = Values(XDocument.Parse(mac).Root!.Element("dict")!);
            Assert.Equal("Shop", values["CFBundleExecutable"]);
            Assert.Equal("Shop & <Co>", values["CFBundleName"]);
            Assert.Equal("dev.example.shop", values["CFBundleIdentifier"]);
            Assert.Equal("1.2.0", values["CFBundleShortVersionString"]);
            Assert.Equal("1.2.0-beta.1", values["CFBundleVersion"]);
            Assert.Equal("APPL", values["CFBundlePackageType"]);
            var type = XDocument.Parse(mac).Descendants("key").Single(key => key.Value == "CFBundleURLTypes").ElementsAfterSelf().First().Element("dict")!;
            Assert.Equal("dev.example.shop", Values(type)["CFBundleURLName"]);
            Assert.Equal(["shop"], type.Elements("array").Single().Elements("string").Select(scheme => scheme.Value));
            Assert.Contains("Shop &amp; &lt;Co&gt;", mac, StringComparison.Ordinal);
            Assert.Null(linux);
            Assert.Null(unschemed);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private static Dictionary<string, string> Values(XElement dict) =>
        dict.Elements("key").Where(key => key.ElementsAfterSelf().FirstOrDefault()?.Name == "string")
            .ToDictionary(key => key.Value, key => key.ElementsAfterSelf().First().Value);

    private static async Task<string> ProjectAsync(string work, string name, string targets, string properties)
    {
        var folder = Directory.CreateDirectory(Path.Combine(work, name)).FullName;
        var project = Path.Combine(folder, name + ".csproj");
        await File.WriteAllTextAsync(project, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>{name}</AssemblyName>
                <Product>{name} &amp; &lt;Co&gt;</Product>
                {properties}
              </PropertyGroup>
              <Import Project="{targets}" />
            </Project>
            """, TestContext.Current.CancellationToken);
        return project;
    }

    private static async Task<string?> BundleAsync(string project, string runtime, string version)
    {
        var publish = Path.Combine(Path.GetDirectoryName(project)!, "publish-" + runtime) + Path.DirectorySeparatorChar;
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in (string[])["msbuild", project, "-t:AiorosMacBundle", "-restore:false", "-nologo", "-p:RuntimeIdentifier=" + runtime, "-p:Version=" + version, "-p:PublishDir=" + publish])
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var inherited in start.Environment.Keys.Where(key => key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase) || key.StartsWith("DOTNET_HOST", StringComparison.Ordinal)).ToList())
        {
            start.Environment.Remove(inherited);
        }

        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, await output + await error);
        var plist = Path.Combine(publish, "Info.plist");
        return File.Exists(plist) ? await File.ReadAllTextAsync(plist, TestContext.Current.CancellationToken) : null;
    }

    private static string Root(string directory) =>
        File.Exists(Path.Combine(directory, "Aioros.slnx")) ? directory : Root(Path.GetDirectoryName(directory) ?? throw new InvalidOperationException("The repository root was not found."));
}
