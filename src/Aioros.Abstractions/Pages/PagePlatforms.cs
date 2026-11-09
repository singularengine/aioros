namespace Aioros.Pages;

/// <summary>
/// Where a page runs, declared on its XAML root (<c>a:Page.Platforms="Desktop"</c>). A page that leaves out the web is
/// native-only: its route still exists on the web and answers with a stand-in that invites to open or install the app
/// (Expo Router allows a platform-only route only beside a universal fallback).
/// </summary>
[Flags]
public enum PagePlatforms
{
    None = 0,
    Web = 1,
    Desktop = 2,
    Mobile = 4,
    All = Web | Desktop | Mobile,
}
