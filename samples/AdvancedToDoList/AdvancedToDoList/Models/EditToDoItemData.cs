namespace AdvancedToDoList.Models;

/// <summary>What the edit page of a to-do item loads: the item and the categories it can take.</summary>
public sealed record EditToDoItemData(ToDoItem Item, Category[] Categories);
