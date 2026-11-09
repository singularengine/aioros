using System.Threading.Tasks;
using AdvancedToDoList.Models;

namespace AdvancedToDoList.Services;

/// <summary>
/// Service interface for managing ToDoItem persistence operations.
/// Defines contract for ToDoItem CRUD operations to enable dependency injection and testing.
/// Allows swapping implementations for different platforms or testing scenarios.
/// </summary>
public interface IToDoItemService
{   
    /// <summary>Gets the to-do items with their categories.</summary>
    Task<ToDoItem[]> GetToDoItemsAsync(bool loadAlsoCompletedItems);

    /// <summary>Gets one to-do item with its category, or null when it does not exist.</summary>
    Task<ToDoItem?> GetToDoItemAsync(long id);

    /// <summary>
    /// Asynchronously saves a ToDoItem to persistent storage.
    /// Handles both creating new items and updating existing items.
    /// </summary>
    /// <param name="item">The ToDoItem to save</param>
    /// <returns>True if the operation was successful, otherwise false</returns>
    Task<bool> SaveToDoItemAsync(ToDoItem item);
    
    /// <summary>
    /// Asynchronously deletes a ToDoItem from persistent storage.
    /// Permanently removes the item from the data store.
    /// </summary>
    /// <param name="item">The ToDoItem to delete</param>
    /// <returns>True if the operation was successful, otherwise false</returns>
    Task<bool> DeleteToDoItemAsync(ToDoItem item);
}
