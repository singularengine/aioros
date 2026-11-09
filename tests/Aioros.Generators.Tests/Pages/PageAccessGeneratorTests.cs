namespace Aioros.Generators.Tests.Pages;

public sealed class PageAccessGeneratorTests
{
    private const string Models = """
        namespace Microsoft.AspNetCore.Authorization
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class AuthorizeAttribute : System.Attribute
            {
                public AuthorizeAttribute() { }
                public AuthorizeAttribute(string policy) => Policy = policy;
                public string? Policy { get; set; }
                public string? Roles { get; set; }
            }

            public sealed class AllowAnonymousAttribute : System.Attribute { }
        }

        namespace App.Models
        {
            [Microsoft.AspNetCore.Authorization.Authorize]
            public sealed class ShellModel { }

            [Microsoft.AspNetCore.Authorization.Authorize("admins", Roles = "owner")]
            public sealed class AdminModel { }

            [Microsoft.AspNetCore.Authorization.AllowAnonymous]
            public sealed class LoginModel { }

            public sealed class OpenModel { }
        }
        """;

    [Fact]
    [Trait("AC", "ACCESS-01")]
    public void AccessRulesComeFromTheLayoutAndPageModels()
    {
        var pages = new GeneratorHarness()
            .Source(Models)
            .Xaml("Pages/(app)/Layout.axaml", "<ContentControl />", "ShellModel")
            .Xaml("Pages/(app)/admin/Page.axaml", "<TextBlock />", "AdminModel")
            .Xaml("Pages/(app)/login/Page.axaml", "<TextBlock />", "LoginModel")
            .Xaml("Pages/open/Page.axaml", "<TextBlock />", "OpenModel")
            .Run()
            .Pages(out _)
            .ToDictionary(page => page.Route);

        Assert.Equal([new Aioros.Pages.PageAccess(null, null), new Aioros.Pages.PageAccess("admins", "owner")], pages["/admin"].Access);
        Assert.Empty(pages["/login"].Access);
        Assert.Empty(pages["/open"].Access);
    }

    [Fact]
    [Trait("AC", "ACCESS-04")]
    public void AccessRulesComeFromTheLoaders()
    {
        var result = new GeneratorHarness()
            .Source(Models)
            .Source("""
                namespace App.Models
                {
                    public sealed record Shell(string User);
                    public sealed record Report(string Name);
                    public sealed class LoadedShellModel(Shell shell) { public Shell Shell { get; } = shell; }
                    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "staff")]
                    public sealed class ReportModel(Report report) { public Report Report { get; } = report; }
                    public sealed class WelcomeModel(Report report) { public Report Report { get; } = report; }
                }
                """)
            .Source(Loader("ShellLoader", "Shell", "[Microsoft.AspNetCore.Authorization.Authorize]"), "Pages/(app)/Layout.Loader.cs")
            .Source(Loader("ReportLoader", "Report", "[Microsoft.AspNetCore.Authorization.Authorize(\"admins\", Roles = \"owner\")]"), "Pages/(app)/reports/Page.Loader.cs")
            .Source(Loader("WelcomeLoader", "Report", "[Microsoft.AspNetCore.Authorization.AllowAnonymous]"), "Pages/(app)/welcome/Page.Loader.cs")
            .Xaml("Pages/(app)/Layout.axaml", "<ContentControl />", "LoadedShellModel")
            .Xaml("Pages/(app)/reports/Page.axaml", "<TextBlock />", "ReportModel")
            .Xaml("Pages/(app)/welcome/Page.axaml", "<TextBlock />", "WelcomeModel")
            .Xaml("Pages/(app)/plain/Page.axaml", "<TextBlock />", "OpenModel")
            .Run();
        var pages = result.Pages(out _).ToDictionary(page => page.Route);

        Assert.Empty(result.Diagnostics);
        Assert.Equal([new Aioros.Pages.PageAccess(null, null), new Aioros.Pages.PageAccess("admins", "owner"), new Aioros.Pages.PageAccess(null, "staff")], pages["/reports"].Access);
        Assert.Equal([new Aioros.Pages.PageAccess(null, null)], pages["/plain"].Access);
        Assert.Empty(pages["/welcome"].Access);
        Assert.True(pages["/welcome"].AllowAnonymous);
        Assert.False(pages["/reports"].AllowAnonymous);
    }

    private static string Loader(string name, string data, string attribute) => $$"""
        using System.Threading;
        using System.Threading.Tasks;
        using Aioros.Pages;
        namespace App.Loaders;
        {{attribute}}
        public sealed class {{name}} : ILoader<App.Models.{{data}}>
        {
            public ValueTask<LoaderResult<App.Models.{{data}}>> LoadAsync(PageRequest request, CancellationToken cancellationToken) =>
                ValueTask.FromResult<LoaderResult<App.Models.{{data}}>>(new App.Models.{{data}}("x"));
        }
        """;
}
