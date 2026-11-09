using System.Threading.Tasks;
using AdvancedToDoList.Helper;

namespace AdvancedToDoList.Services;

/// <summary>
/// The whole to-do list at once: the export, the import and the reset that the settings page offers. They run where
/// the database is, on the server.
/// </summary>
public interface IToDoListDataService
{
    Task<DatabaseDto> ExportAsync();

    Task ImportAsync(DatabaseDto dto);

    Task ClearAsync();
}
