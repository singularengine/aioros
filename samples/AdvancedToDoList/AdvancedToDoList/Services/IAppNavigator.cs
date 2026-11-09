namespace AdvancedToDoList.Services;

/// <summary>Where the view models go next: the routes that replace the hamburger menu and the dialogs.</summary>
public interface IAppNavigator
{
    Task GoToAsync(string path);
}
