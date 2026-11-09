using System.Xml.Linq;

namespace Aioros.Cli.Tests.Packaging;

public sealed class BannedSymbolsTests
{
    private const string Clean = "public static class Library { public static int One => 1; }";

    private const string Blocking = """
        public static class Library
        {
            public static void Block(System.Threading.Tasks.Task task)
            {
                task.Wait();
                System.Threading.Thread.Sleep(1);
            }
        }
        """;

    [Fact]
    [Trait("AC", "CLI-40")]
    public async Task AProjectThatBansTheSameApisBuildsAndKeepsEveryRuleAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var work = Directory.CreateTempSubdirectory("aioros-banned-");
        try
        {
            var analyzer = XDocument.Load(Path.Combine(PackagedBuild.Repository, "Directory.Packages.props")).Descendants("PackageVersion")
                .Single(static package => package.Attribute("Include")?.Value == "Microsoft.CodeAnalysis.BannedApiAnalyzers").Attribute("Version")!.Value;
            await File.WriteAllTextAsync(Path.Combine(work.FullName, "Consumer.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="{analyzer}" PrivateAssets="all" />
                    <AdditionalFiles Include="BannedSymbols.txt" />
                  </ItemGroup>
                  <Import Project="{Path.Combine(PackagedBuild.Repository, "src", "Aioros", "build", "Aioros.targets")}" />
                </Project>
                """, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(work.FullName, "BannedSymbols.txt"), "M:System.Threading.Tasks.Task.Wait ; The project's own reason\n", cancellationToken);
            var source = Path.Combine(work.FullName, "Library.cs");

            await File.WriteAllTextAsync(source, Clean, cancellationToken);
            var clean = await PackagedBuild.DotnetAsync(work.FullName, "build", "-tl:off", "--disable-build-servers");
            await File.WriteAllTextAsync(source, Blocking, cancellationToken);
            var blocking = await PackagedBuild.DotnetAsync(work.FullName, "build", "-tl:off", "--disable-build-servers");

            Assert.True(clean.Exit == 0, clean.Output);
            Assert.DoesNotContain("RS0031", clean.Output, StringComparison.Ordinal);
            Assert.NotEqual(0, blocking.Exit);
            Assert.DoesNotContain("RS0031", blocking.Output, StringComparison.Ordinal);
            Assert.Contains("The project's own reason", blocking.Output, StringComparison.Ordinal);
            Assert.Contains("Thread.Sleep", blocking.Output, StringComparison.Ordinal);
            Assert.Contains("Blocks a thread", blocking.Output, StringComparison.Ordinal);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }
}
