using AdvancedToDoList.Models;
using AdvancedToDoList.Services;
using Aioros.Pages;

namespace AdvancedToDoList.Pages.Categories;

/// <summary>The category to edit, or a new one for <c>/categories/new</c>.</summary>
public sealed class EditCategoryLoader(ICategoryService categoryService)
{
    public async Task<LoaderResult<Category>> LoadAsync(string id) =>
        id == "new"
            ? new Category { Name = "New Category" }
            : long.TryParse(id, out var key) && await categoryService.GetCategoryAsync(key) is { } category
                ? category
                : LoaderResult.NotFound();
}
