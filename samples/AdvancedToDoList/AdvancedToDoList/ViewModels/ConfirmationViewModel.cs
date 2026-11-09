using System.Threading.Tasks;
using AdvancedToDoList.Models;
using AdvancedToDoList.Services;
using CommunityToolkit.Mvvm.Input;

namespace AdvancedToDoList.ViewModels;

/// <summary>What a confirmation page asks and does.</summary>
public enum ConfirmationAction
{
    DeleteToDoItem,
    DeleteCategory,
    ClearDatabase
}

/// <summary>A question the app asked in an overlay dialog, now asked on a page of its own.</summary>
public sealed record Confirmation(string Title, string Message, ConfirmationAction Action, long Id, string Back);

/// <summary>
/// The Yes / No overlay dialogs of the original app as a page: Yes does what was asked and goes back, No goes back.
/// </summary>
public partial class ConfirmationViewModel(
    Confirmation confirmation,
    IToDoItemService toDoItemService,
    ICategoryService categoryService,
    IToDoListDataService dataService,
    IAppNavigator navigator) : ViewModelBase
{
    public string Title => confirmation.Title;

    public string Message => confirmation.Message;

    [RelayCommand]
    private async Task YesAsync()
    {
        switch (confirmation.Action)
        {
            case ConfirmationAction.DeleteToDoItem:
                await toDoItemService.DeleteToDoItemAsync(new ToDoItem { Id = confirmation.Id });
                break;
            case ConfirmationAction.DeleteCategory:
                await categoryService.DeleteCategoryAsync(new Category { Id = confirmation.Id });
                break;
            case ConfirmationAction.ClearDatabase:
                await dataService.ClearAsync();
                break;
        }

        await navigator.GoToAsync(confirmation.Back);
    }

    [RelayCommand]
    private Task NoAsync() => navigator.GoToAsync(confirmation.Back);
}
