using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdvancedToDoList.Messages;
using AdvancedToDoList.Models;
using AdvancedToDoList.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AdvancedToDoList.ViewModels;

/// <summary>
/// ViewModel for editing ToDoItems in a dialog context.
/// Provides UI interactions for modifying ToDoItem properties, managing categories,
/// and validating input before saving changes to the database.
/// </summary>
public partial class EditToDoItemViewModel : ViewModelBase
{    
    /// <summary>
    /// Dialog service for showing dialogs and managing user interactions.
    /// Used for confirmation dialogs, error messages, and category creation.
    /// </summary>
    private readonly IAppNavigator _navigator;
    
    /// <summary>
    /// Service for saving and managing ToDoItem data.
    /// Handles persistence of ToDoItems to the database.
    /// </summary>
    private readonly IToDoItemService _toDoService;
    
    /// <summary>
    /// Creates a new instance of this ViewModel to edit the given ToDoItem.
    /// Resolves services from the dependency injection container or creates default instances.
    /// </summary>
    /// <param name="toDoItem">The ToDoItem to edit.</param>
    /// <param name="availableCategories">A list of available categories to select from.</param>
    public EditToDoItemViewModel(EditToDoItemData data, IToDoItemService toDoService, IAppNavigator navigator)
        : this(new ToDoItemViewModel(data.Item), data.Categories.Select(x => new CategoryViewModel(x)).ToList(),
            toDoService, navigator)
    {
    }

    /// <summary>
    /// Creates a new instance of this ViewModel to edit the given to-do-item with the provided services.
    /// This constructor is primarily used for unit testing with mocked services.
    /// </summary>
    /// <param name="toDoItem">The ToDoItem to edit.</param>
    /// <param name="availableCategories">A list of available categories to select from.</param>
    /// <param name="toDoService">The service for managing ToDoItem persistence.</param>
    /// <param name="dialogService">The service for managing dialog interactions.</param>
    private EditToDoItemViewModel(ToDoItemViewModel toDoItem, IList<CategoryViewModel> availableCategories,
        IToDoItemService toDoService, IAppNavigator navigator)
    {
        Item = toDoItem;
        _toDoService = toDoService;
        _navigator = navigator;
        
        //Add the empty category to the list as the first option (represents "uncategorized")
        availableCategories.Insert(0, CategoryViewModel.Empty);
        
        AvailableCategories = availableCategories;
    }

    /// <summary>
    /// Gets the Item that should be edited.
    /// </summary>
    public ToDoItemViewModel Item { get; }

    /// <summary>
    /// Gets a list of available categories to choose from.
    /// </summary>
    public IList<CategoryViewModel> AvailableCategories { get; }

    /// <summary>Keeps the draft and its errors when the page's data is read again after a command.</summary>
    public void Receive(EditToDoItemData data)
    {
    }

    /// <summary>The error the last save ended with, shown on the page instead of a dialog.</summary>
    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>
    /// Command that opens a dialog to create a new category.
    /// The newly created category is added to the available categories list
    /// and automatically selected for the current ToDoItem.
    /// </summary>
    [RelayCommand]
    private async Task AddNewCategoryAsync()
    {
        // Open the page that creates a new category
        await _navigator.GoToAsync("/categories/new");
    }

    /// <summary>
    /// Command that resets the ToDoItem's category to the default "uncategorized" option.
    /// Uses the static Empty category to represent items without a specific category.
    /// </summary>
    [RelayCommand]
    private void SetCategoryToEmpty()
    {
        Item.Category = CategoryViewModel.Empty;
    }
    
    /// <summary>
    /// Gets a command that saves the changes to the item and returns the item to the caller.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        // Check if the item is valid. If the item has errors, notify the user and exit this method.
        Item.Validate();
        if (Item.HasErrors)
        {
            Error = "Please correct the errors in the form.";
            return;
        }
        
        // try to save the item.
        var toDoItem = Item.ToToDoItem();
        var success = await _toDoService.SaveToDoItemAsync(toDoItem);
        
        // If saving was successful, return the changed item, otherwise show an error dialog.
        if (success)
        {
            await _navigator.GoToAsync("/");
        }
        else
        {
            // Show an error message if something went wrong.
            Error = "An error occurred while saving the to-do item.";
        }
    }

    /// <summary>
    /// Gets a command that cancels the edit. 
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">This exception should never occur in theory.</exception>
    [RelayCommand]
    private async Task CancelAsync()
    {
        // The "Save changes?" question is the Cancel button's flyout: Yes runs Save, No runs this.
        await _navigator.GoToAsync("/");
    }
}