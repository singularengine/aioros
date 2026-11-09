using System.Threading.Tasks;
using AdvancedToDoList.Helper;
using Dapper;

namespace AdvancedToDoList.Services;

public class ToDoListDataService(DatabaseHelper database) : IToDoListDataService
{
    public Task<DatabaseDto> ExportAsync() => database.ExportAsync();

    public async Task ImportAsync(DatabaseDto dto)
    {
        foreach (var category in dto.Categories ?? [])
        {
            await category.SaveAsync(database);
        }

        foreach (var toDoItem in dto.ToDoItems ?? [])
        {
            await toDoItem.SaveAsync(database);
        }
    }

    public async Task ClearAsync()
    {
        await using var connection = await database.GetOpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            DROP TABLE IF EXISTS Category;
            DROP TABLE IF EXISTS ToDoItem;
            VACUUM;
            """);
        await database.EnsureInitializedAsync(connection, true);
    }
}
