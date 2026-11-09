using System.Reflection;
using System.Threading.Tasks;
using AdvancedToDoList;
using AdvancedToDoList.Desktop.Services;
using AdvancedToDoList.Services;
using Avalonia;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AiorosCapture;

internal static class Target
{
    public static AppBuilder Build(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseService>(new DesktopDbService());
        services.AddSingleton<ISettingsStorageService>(new DefaultSettingsStorageService());
        App.RegisterAppServices(services);
        AddSampleData();
        return AdvancedToDoList.Desktop.Program.BuildAvaloniaApp();
    }

    private static void AddSampleData()
    {
        var helper = typeof(App).Assembly.GetType("AdvancedToDoList.Helper.DatabaseHelper", true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        using var connection = ((Task<SqliteConnection>)helper.GetMethod("GetOpenConnectionAsync", flags)!.Invoke(null, null)!).GetAwaiter().GetResult();
        ((Task)helper.GetMethod("AddSampleDataAsync", flags)!.Invoke(null, [connection])!).GetAwaiter().GetResult();
    }
}
