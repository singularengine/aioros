namespace Showcase;

/// <summary>Where the Showcase's view models go next; Aioros implements it from its registration in <c>Program.cs</c>.</summary>
public interface IAppNavigator
{
    /// <summary>Goes to a path, relative to the current page as a link is.</summary>
    Task GoToAsync(string path);
}
