using Aioros.Functions;
using Aioros.Pages;
using Lumen.Shell;

namespace Lumen.Pages;

[Cached(3600)]
public static class ShellLoader
{
    private static readonly (string Prefix, string Section, string Icon)[] Sections =
    [
        ("/inbox", "Inbox", "inbox"),
        ("/issues", "Issues", "issues"),
        ("/projects", "Projects", "projects"),
        ("/dashboard", "Dashboard", "dashboard"),
        ("/settings", "Settings", "settings"),
    ];

    public static ShellData Load(PageRequest request)
    {
        var match = Sections.FirstOrDefault(section => request.Path.StartsWith(section.Prefix, StringComparison.OrdinalIgnoreCase));
        return match.Prefix is null ? new ShellData("Lumen", "issues") : new ShellData(match.Section, match.Icon);
    }
}
