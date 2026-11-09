using AdvancedToDoList.Models;
using AdvancedToDoList.Services;
using Aioros.Pages;

namespace AdvancedToDoList.Pages.Categories;

public sealed class CategoriesLoader(ICategoryService categoryService)
{
    public async Task<LoaderResult<Category[]>> LoadAsync() => await categoryService.GetCategoriesAsync();
}
