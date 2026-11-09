using AdvancedToDoList.Models;
using AdvancedToDoList.Services;
using Aioros.Pages;

namespace AdvancedToDoList.Pages.Items;

/// <summary>The to-do item to edit, or a new one for <c>/items/new</c>, with the categories it can take.</summary>
public sealed class EditToDoItemLoader(IToDoItemService toDoItemService, ICategoryService categoryService)
{
    public async Task<LoaderResult<EditToDoItemData>> LoadAsync(string id)
    {
        var item = id == "new"
            ? new ToDoItem { Title = "To-Do Item" }
            : long.TryParse(id, out var key) ? await toDoItemService.GetToDoItemAsync(key) : null;
        return item is null
            ? LoaderResult.NotFound()
            : new EditToDoItemData(item, await categoryService.GetCategoriesAsync());
    }
}
