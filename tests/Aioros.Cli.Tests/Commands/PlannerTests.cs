using Aioros.Cli.Commands;

namespace Aioros.Cli.Tests.Commands;

public sealed class PlannerTests
{
    private static readonly AppFolder Empty = new(null, []);

    [Fact]
    [Trait("AC", "CLI-01")]
    public void UsageIsPrinted()
    {
        var help = Planner.Plan([], Empty, "1.0.0");
        var unknown = Planner.Plan(["deploy-to-mars"], Empty, "1.0.0");

        Assert.Equal(0, help.ExitCode);
        Assert.All(["new", "dev", "build", "start", "routes"], command => Assert.Contains($"  {command}", help.Output, StringComparison.Ordinal));
        Assert.Equal(1, unknown.ExitCode);
        Assert.Empty(unknown.Steps);
    }

    [Fact]
    [Trait("AC", "CLI-02")]
    public void NewInstallsTheTemplateAndCreatesTheApp()
    {
        var plan = Planner.Plan(["new", "shop"], Empty, "1.2.3");

        Assert.Equal(
            [["new", "install", "Aioros.Templates::1.2.3"], ["new", "aioros", "-n", "shop"]],
            plan.Steps.Select(step => step.Arguments.ToArray()));
    }

    [Fact]
    [Trait("AC", "CLI-03")]
    public void DevBuildAndStartAreDotnetCommands()
    {
        var project = Path.Combine("Shop", "Shop.csproj");
        var publish = Path.Combine(".aioros", "publish");
        var folder = new AppFolder(project, []);

        var dev = Assert.Single(Planner.Plan(["dev"], folder, "1.0.0").Steps);
        var build = Planner.Plan(["build"], folder, "1.0.0").Steps;
        var start = Assert.Single(Planner.Plan(["start"], folder, "1.0.0").Steps);

        Assert.Equal(["watch", "--project", project, "--non-interactive", "--", "--urls=http://localhost:3000"], dev.Arguments);
        Assert.Equal("Development", dev.Environment["ASPNETCORE_ENVIRONMENT"]);
        Assert.Null(dev.WorkingDirectory);
        Assert.Equal(["publish", project, "-c", "Release", "-o", publish], build[0].Arguments);
        Assert.Equal(["Shop.dll", "--precompile=aioros.plans.json"], build[1].Arguments);
        Assert.Equal(publish, build[1].WorkingDirectory);
        Assert.Equal("Production", build[1].Environment["ASPNETCORE_ENVIRONMENT"]);
        Assert.Equal(["Shop.dll"], start.Arguments);
        Assert.Equal(publish, start.WorkingDirectory);
        Assert.Equal("Production", start.Environment["ASPNETCORE_ENVIRONMENT"]);
    }

    [Fact]
    [Trait("AC", "CLI-10")]
    public void DevListensOnAFreePortWithoutOpeningABrowser()
    {
        var folder = new AppFolder(Path.Combine("Shop", "Shop.csproj"), []);
        var taken = new HashSet<int> { 3000, 3001, 5000 };
        taken.UnionWith(Enumerable.Range(7000, 11));
        bool Free(int port) => !taken.Contains(port);

        var dev = Planner.Plan(["dev"], folder, "1.0.0", free: Free, token: "c0ffee");
        var chosen = Planner.Plan(["dev", "--port=4000"], folder, "1.0.0", free: Free);
        var busy = Planner.Plan(["dev", "--port=5000"], folder, "1.0.0", free: Free);
        var full = Planner.Plan(["dev", "--port=7000"], folder, "1.0.0", free: Free);
        var invalid = Planner.Plan(["dev", "--port=70000"], folder, "1.0.0", free: Free);
        var words = Planner.Plan(["dev", "--port=web"], folder, "1.0.0", free: Free);
        var profiled = Planner.Plan(["dev"], folder with { Port = 5318 }, "1.0.0", free: Free);
        var desktop = Planner.Plan(["dev", "--desktop"], folder, "1.0.0", free: Free, token: "c0ffee");
        const string Launch = """
            { "profiles": {
              "Desktop": { "commandName": "Executable", "applicationUrl": "http://localhost:9000" },
              "Web": { "commandName": "Project", "applicationUrl": "https://localhost:7001;http://localhost:5318" },
              "Other": { "commandName": "Project", "applicationUrl": "http://localhost:6000" } } }
            """;

        var step = Assert.Single(dev.Steps);
        Assert.Equal("--urls=http://localhost:3002", step.Arguments[^1]);
        Assert.Equal("Port 3000 is in use, using 3002 instead.\n", dev.Output);
        Assert.Equal("1", step.Environment["DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER"]);
        Assert.Equal("1", step.Environment["DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH"]);
        Assert.Equal("c0ffee", step.Environment["Aioros__DevelopmentToken"]);
        Assert.False(Assert.Single(chosen.Steps).Environment.ContainsKey("Aioros__DevelopmentToken"));
        Assert.Equal(("--urls=http://localhost:4000", string.Empty), (Assert.Single(chosen.Steps).Arguments[^1], chosen.Output));
        Assert.Equal("--urls=http://localhost:5001", Assert.Single(busy.Steps).Arguments[^1]);
        Assert.Equal((1, "Ports 7000 to 7010 are all in use; choose another with --port.\n"), (full.ExitCode, full.Output));
        Assert.Empty(full.Steps);
        Assert.Equal((1, "--port must be a number from 1 to 65535, not '70000'.\n"), (invalid.ExitCode, invalid.Output));
        Assert.Equal((1, "--port must be a number from 1 to 65535, not 'web'.\n"), (words.ExitCode, words.Output));
        Assert.Equal("--urls=http://localhost:5318", Assert.Single(profiled.Steps).Arguments[^1]);
        Assert.Equal(["watch", "--project", folder.Project!, "--non-interactive", "--", "--desktop"], Assert.Single(desktop.Steps).Arguments);
        Assert.Empty(desktop.Output);
        Assert.Equal("Development", desktop.Steps[0].Environment["ASPNETCORE_ENVIRONMENT"]);
        Assert.Equal(5318, AppFolder.LaunchPort(Launch));
        Assert.Null(AppFolder.LaunchPort("""{ "profiles": { "Web": { "commandName": "Project" } } }"""));
        Assert.Null(AppFolder.LaunchPort("not json"));
    }

    [Fact]
    [Trait("AC", "CLI-07")]
    public void BuildBakesInTheServerOfTheInstalledApp()
    {
        var project = Path.Combine("Shop", "Shop.csproj");
        var publish = Path.Combine(".aioros", "publish");
        var folder = new AppFolder(project, []);

        var baked = Planner.Plan(["build", "--server=https://shop.example/"], folder, "1.0.0");
        var plain = Planner.Plan(["build"], folder, "1.0.0");
        var relative = Planner.Plan(["build", "--server=/api"], folder, "1.0.0");
        var other = Planner.Plan(["build", "--server=ftp://shop.example"], folder, "1.0.0");

        Assert.Equal(["publish", project, "-c", "Release", "-o", publish, "-p:AiorosServer=https://shop.example"], baked.Steps[0].Arguments);
        Assert.Equal(["Shop.dll", "--precompile=aioros.plans.json"], baked.Steps[1].Arguments);
        Assert.Equal(["publish", project, "-c", "Release", "-o", publish], plain.Steps[0].Arguments);
        Assert.All([relative, other], refused =>
        {
            Assert.Equal(1, refused.ExitCode);
            Assert.Empty(refused.Steps);
            Assert.Contains("--server", refused.Output, StringComparison.Ordinal);
        });
    }

    [Fact]
    [Trait("AC", "CLI-08")]
    public async Task BuildInstallerPublishesAndPacksWithVelopackAsync()
    {
        var project = Path.Combine("Shop", "Shop.csproj");
        var publish = Path.Combine(".aioros", "installer", "publish");
        var releases = Path.Combine(".aioros", "installer", "releases");
        var folder = new AppFolder(project, []);
        var packages = await File.ReadAllTextAsync(Path.Combine(Root(AppContext.BaseDirectory), "Directory.Packages.props"), TestContext.Current.CancellationToken);

        var linux = Planner.Plan(["build", "--installer", "--version=1.2.0"], folder, "1.0.0", "linux-x64");
        var windows = Planner.Plan(["build", "--installer", "--version=1.2.0", "--server=https://shop.example"], folder, "1.0.0", "win-x64");
        var mac = Planner.Plan(["build", "--installer", "--version=1.2.0-beta.1"], folder, "1.0.0", "osx-arm64");
        var unversioned = Planner.Plan(["build", "--installer"], folder, "1.0.0", "linux-x64");
        var malformed = Planner.Plan(["build", "--installer", "--version=1.2"], folder, "1.0.0", "linux-x64");

        string[] Pack(string version, string executable) =>
            ["dnx", "vpk@" + Planner.VelopackVersion, "--yes", "pack", "--packId", "Shop", "--packVersion", version, "--packDir", publish, "--mainExe", executable, "--outputDir", releases];
        Assert.Contains($"<PackageVersion Include=\"Velopack\" Version=\"{Planner.VelopackVersion}\" />", packages, StringComparison.Ordinal);
        Assert.Equal(
            [
                ["publish", project, "-c", "Release", "-r", "linux-x64", "--self-contained", "-o", publish, "-p:AiorosInstalled=true", "-p:Version=1.2.0"],
                Pack("1.2.0", "Shop"),
            ],
            linux.Steps.Select(step => step.Arguments.ToArray()));
        Assert.Equal(
            [
                ["publish", project, "-c", "Release", "-r", "win-x64", "--self-contained", "-o", publish, "-p:AiorosInstalled=true", "-p:Version=1.2.0", "-p:AiorosServer=https://shop.example"],
                Pack("1.2.0", "Shop.exe"),
            ],
            windows.Steps.Select(step => step.Arguments.ToArray()));
        Assert.Equal([.. Pack("1.2.0-beta.1", "Shop"), "--plist", Path.Combine(publish, "Info.plist")], mac.Steps[1].Arguments);
        Assert.All([unversioned, malformed], refused =>
        {
            Assert.Equal(1, refused.ExitCode);
            Assert.Empty(refused.Steps);
            Assert.Contains("--version", refused.Output, StringComparison.Ordinal);
        });
    }

    private static string Root(string directory) =>
        File.Exists(Path.Combine(directory, "Aioros.slnx")) ? directory : Root(Path.GetDirectoryName(directory) ?? throw new InvalidOperationException("The repository root was not found."));

    [Fact]
    [Trait("AC", "CLI-04")]
    public void RoutesAreListedAndConflictsFail()
    {
        var plan = Planner.Plan(["routes"], new AppFolder("Shop", ["Pages/issues/[id]/Page.axaml", "Pages/Page.axaml", "Pages/Layout.axaml"]), "1.0.0");
        var conflict = Planner.Plan(["routes"], new AppFolder("Shop", ["Pages/(a)/x/Page.axaml", "Pages/(b)/x/Page.axaml"]), "1.0.0");

        Assert.Equal(0, plan.ExitCode);
        Assert.Equal("/  Pages/Page.axaml  < Pages/Layout.axaml\n/issues/{id}  Pages/issues/[id]/Page.axaml  < Pages/Layout.axaml\n", plan.Output);
        Assert.Equal(1, conflict.ExitCode);
        Assert.Contains("conflict: /x", conflict.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "CLI-05")]
    public void TheAppIsTheProjectWithPages()
    {
        var shop = Path.Combine("Shop", "Shop.csproj");
        var host = Path.Combine("Shop.AppHost", "Shop.AppHost.csproj");
        var admin = Path.Combine("Admin", "Admin.csproj");
        var pages = new HashSet<string>([shop, "Root.csproj", admin], StringComparer.Ordinal);

        var solution = AppFolder.Find([host, shop], pages.Contains);
        var own = AppFolder.Find(["Root.csproj"], pages.Contains);
        var none = AppFolder.Find([host], pages.Contains);
        var several = AppFolder.Find([shop, admin], pages.Contains);

        Assert.Equal(shop, solution.Project);
        Assert.Equal("Root.csproj", own.Project);
        Assert.All(["dev", "build", "start", "routes"], command =>
        {
            var plan = Planner.Plan([command], none, "1.0.0");
            Assert.Equal(1, plan.ExitCode);
            Assert.Empty(plan.Steps);
            Assert.Contains("No Aioros app", plan.Output, StringComparison.Ordinal);
        });
        var ambiguous = Planner.Plan(["dev"], several, "1.0.0");
        Assert.Equal(1, ambiguous.ExitCode);
        Assert.Contains(shop, ambiguous.Output, StringComparison.Ordinal);
        Assert.Contains(admin, ambiguous.Output, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("AC", "CLI-42")]
    [InlineData("new", "<name>")]
    [InlineData("dev", "--port=<n>")]
    [InlineData("dev", "--desktop")]
    [InlineData("build", "--server=<url>")]
    [InlineData("build", "--installer")]
    [InlineData("build", "--version=<semver>")]
    [InlineData("start", "aioros start")]
    [InlineData("routes", "aioros routes")]
    public void ACommandFollowedByHelpPrintsItsUsageAndRunsNothing(string command, string shown)
    {
        var app = new AppFolder(Path.Combine("Shop", "Shop.csproj"), []);
        var none = new AppFolder(null, []) { Problem = "No Aioros app found.\n" };

        foreach (var plan in new[] { Planner.Plan([command, "--help"], app, "1.0.0"), Planner.Plan([command, "-h"], none, "1.0.0") })
        {
            Assert.Equal(0, plan.ExitCode);
            Assert.Empty(plan.Steps);
            Assert.Contains(shown, plan.Output, StringComparison.Ordinal);
        }
    }
}
