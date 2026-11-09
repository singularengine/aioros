using AdvancedToDoList.Services;
using AdvancedToDoList.ViewModels;
using Aioros.Pages;

namespace AdvancedToDoList.Pages.Confirm;

/// <summary>The questions the overlay dialogs asked: <c>/confirm/delete-item/{id}</c>, <c>/confirm/delete-category/{id}</c> and <c>/confirm/clear/0</c>.</summary>
public sealed class ConfirmationLoader(IToDoItemService toDoItemService, ICategoryService categoryService)
{
    public async Task<LoaderResult<Confirmation>> LoadAsync(string action, long id) => action switch
    {
        "delete-item" when await toDoItemService.GetToDoItemAsync(id) is { } toDoItem =>
            new Confirmation("Delete To Do Item", $"Are you sure you want to delete the todo item '{toDoItem.Title}'?",
                ConfirmationAction.DeleteToDoItem, id, "/"),
        "delete-category" when await categoryService.GetCategoryAsync(id) is { } category =>
            new Confirmation("Delete category", $"Are you sure you want to delete the category '{category.Name}'?",
                ConfirmationAction.DeleteCategory, id, "/categories"),
        "clear" => new Confirmation("Clear Database",
            """
            Are you sure you want to clear the database? This cannot be undone.
            TIP: Consider to export the data before you continue.

            Press "Yes" to continue.
            """, ConfirmationAction.ClearDatabase, 0, "/settings"),
        _ => LoaderResult.NotFound(),
    };
}
