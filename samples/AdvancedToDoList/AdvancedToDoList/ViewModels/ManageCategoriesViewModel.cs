using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AdvancedToDoList.Messages;
using AdvancedToDoList.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;
using DynamicData.Binding;
using AdvancedToDoList.Services;

namespace AdvancedToDoList.ViewModels;

/// <summary>
/// This ViewModel will manage all of our categories.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2112", Justification = "We have all needed members added via DynamicallyAccessedMembers-Attribute")]
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We have all needed members added via DynamicallyAccessedMembers-Attribute")]
public partial class ManageCategoriesViewModel
    : ViewModelBase, IRecipient<UpdateDataMessage<Category>>
{
    /// <summary>
    /// Creates a new instance of this ViewModel.
    /// </summary>
    private readonly ICategoryService _categoryService;
    private readonly IAppNavigator _navigator;

    public ManageCategoriesViewModel(Category[] categories, ICategoryService categoryService, IAppNavigator navigator, IMessenger messenger)
    {
        _categoryService = categoryService;
        _navigator = navigator;

        // Registers ourselves to the scoped messenger, which is needed to receive the UpdateDataRequest-messaged.
        messenger.Register(this);

        // This sets up the source cache. Please check https://github.com/reactivemarbles/DynamicData for full documentation.
        _categoriesSourceCache.Connect()
            // We sort by categories name. If two items have the same name, we then sort by its ID.
            .SortAndBind(out _categories,
                SortExpressionComparer<CategoryViewModel>
                    .Ascending(x => x.Name ?? string.Empty)
                    .ThenByAscending(x => x.Id ?? -1))
            // Remember to subscribe to the changes, otherwise the UI will never update. 
            .Subscribe();

        Receive(categories);
    }

    /// <summary>Takes the categories the page's loader read, first and after every change.</summary>
    public void Receive(Category[] categories)
    {
        _categoriesSourceCache.Edit(cache =>
        {
            cache.Clear();
            cache.AddOrUpdate(categories.Select(x => new CategoryViewModel(x)));
        });
    }

    /// <summary>
    /// This task will load the data async.
    /// </summary>
    private async Task LoadDataAsync()
    {
        // fetch all categories
        var categories = await _categoryService.GetCategoriesAsync();

        // Use AddOrUpdate, which will update existing items and add new items.
        _categoriesSourceCache.AddOrUpdate(categories.Select(x => new CategoryViewModel(x)));
    }

    // This is the SourceCache, which stores all of our items.
    private readonly SourceCache<CategoryViewModel, long> _categoriesSourceCache =
        new SourceCache<CategoryViewModel, long>(c => c.Id ?? -1);

    /// <summary>
    /// This is the backing field for the <see cref="Categories"/>-collection.
    /// </summary>
    private readonly ReadOnlyObservableCollection<CategoryViewModel> _categories;

    /// <summary>
    /// Gets a read-only collection of all loaded categories.
    /// </summary>
    public ReadOnlyObservableCollection<CategoryViewModel> Categories => _categories;

    /// <summary>
    /// Gets or sets the selected category. 
    /// </summary>
    /// <remarks>When this changes, also the edit and delete command should be notified.</remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCategoryCommand), nameof(EditCategoryCommand))]
    public partial CategoryViewModel? SelectedCategory { get; set; }

    /// <summary>
    /// Gets a command that adds a new Category.
    /// </summary>
    [RelayCommand]
    private async Task AddNewCategoryAsync()
    {
        // Open the edit page for a new category.
        await _navigator.GoToAsync("/categories/new");
    }

    /// <summary>
    /// This method checks if the given category can be edited or deleted.
    /// </summary>
    /// <param name="category">the item that should be edited</param>
    /// <returns>true if the item is not null</returns>
    private static bool CanEditOrDeleteCategory(CategoryViewModel? category) => category != null;

    /// <summary>
    /// Gets a command that deletes the provided category.
    /// </summary>
    /// <param name="category">the category to remove</param>
    [RelayCommand(CanExecute = nameof(CanEditOrDeleteCategory))]
    private async Task DeleteCategoryAsync(CategoryViewModel? category)
    {
        if (category == null)
        {
            return;
        }

        // Ask on the confirmation page, which deletes the category.
        await _navigator.GoToAsync($"/confirm/delete-category/{category.Id}");
    }

    /// <summary>
    /// Gets a command that edits the provided category.
    /// </summary>
    /// <param name="category">The category to edit.</param>
    [RelayCommand(CanExecute = nameof(CanEditOrDeleteCategory))]
    private async Task EditCategoryAsync(CategoryViewModel? category)
    {
        // In theory this should never be null but better to safeguard it here. 
        if (category == null)
        {
            return;
        }

        // Open the edit page, which loads the category.
        await _navigator.GoToAsync($"/categories/{category.Id}");
    }

    /// <summary>
    /// Gets a command that refreshes the entire list.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        // remember the ID that was selected before.
        var previousSelectedItemId = SelectedCategory?.Id ?? -1;

        // clear the source cache, then load the data again.
        _categoriesSourceCache.Clear();
        await LoadDataAsync();

        // try to find the previous selected item. and select it again.
        var lookUpResult = _categoriesSourceCache.Lookup(previousSelectedItemId);

        SelectedCategory = lookUpResult.HasValue
            ? lookUpResult.Value
            : null;
    }

    // IRecipient-Impl

    /// <inheritdoc />
    public void Receive(UpdateDataMessage<Category> message)
    {
        // Get the updated items
        var categoriesToUpdate =
            message.ItemsAffected.Select(x => new CategoryViewModel(x));

        switch (message.Action)
        {
            case UpdateAction.Added:
            case UpdateAction.Updated:
                // Update our collection. 
                _categoriesSourceCache.AddOrUpdate(categoriesToUpdate);
                break;

            case UpdateAction.Removed:
                // Remove the items from our collection
                _categoriesSourceCache.Remove(categoriesToUpdate);
                break;
            
            case UpdateAction.Reset:
                _ = RefreshAsync();
                break;
            
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}