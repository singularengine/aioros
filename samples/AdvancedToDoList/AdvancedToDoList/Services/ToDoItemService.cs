using System.Threading.Tasks;
using AdvancedToDoList.Helper;
using AdvancedToDoList.Models;

namespace AdvancedToDoList.Services;

/// <summary>
/// Service implementation for managing ToDoItem persistence operations.
/// Provides abstraction layer between ViewModels and the ToDoItem model's database operations.
/// Implements repository pattern for better testability and separation of concerns.
/// </summary>
public class ToDoItemService(DatabaseHelper database) : IToDoItemService
{    
    public async Task<ToDoItem[]> GetToDoItemsAsync(bool loadAlsoCompletedItems) => (await database.GetToDoItemsAsync(loadAlsoCompletedItems)).ToArray();

    public async Task<ToDoItem?> GetToDoItemAsync(long id) => (await database.GetToDoItemsAsync(true)).FirstOrDefault(x => x.Id == id);

    /// <summary>
    /// Asynchronously saves a ToDoItem to the database.
    /// Delegates to the ToDoItem model's SaveAsync method for actual persistence logic.
    /// Handles both new item creation and existing item updates.
    /// </summary>
    /// <param name="item">The ToDoItem to save (insert or update)</param>
    /// <returns>True if save was successful, false otherwise</returns>
    public async Task<bool> SaveToDoItemAsync(ToDoItem item)
    {
        return await item.SaveAsync(database);
    }
    
    /// <summary>
    /// Asynchronously deletes a ToDoItem from the database.
    /// Delegates to the ToDoItem model's DeleteAsync method for actual deletion logic.
    /// Permanently removes the ToDoItem from the data store.
    /// </summary>
    /// <param name="item">The ToDoItem to delete</param>
    /// <returns>True if deletion was successful, false otherwise</returns>
    public async Task<bool> DeleteToDoItemAsync(ToDoItem item)
    {
        return await item.DeleteAsync(database);
    }
}
