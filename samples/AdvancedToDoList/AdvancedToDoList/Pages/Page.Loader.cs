using AdvancedToDoList.Models;
using AdvancedToDoList.Services;
using Aioros.Pages;

namespace AdvancedToDoList.Pages;

public sealed class ToDoItemsLoader(IToDoItemService toDoItemService)
{
    public async Task<LoaderResult<ToDoItem[]>> LoadAsync() => await toDoItemService.GetToDoItemsAsync(true);
}
