using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AdvancedToDoList.Helper;
using AdvancedToDoList.Messages;
using AdvancedToDoList.Models;
using AdvancedToDoList.Properties;
using AdvancedToDoList.Services;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SharedControls.Services;

namespace AdvancedToDoList.ViewModels;

/// <summary>
/// ViewModel responsible for managing application settings and data operations.
/// Handles theme switching, data import/export, and database management.
/// </summary>
public partial class SettingsViewModel(IToDoListDataService data, IAppNavigator navigator, IMessenger messenger) : ViewModelBase, IDialogParticipant
{
    /// <summary>What the last export or import did, shown on the page instead of a dialog.</summary>
    [ObservableProperty]
    public partial string? Message { get; set; }

    /// <summary>
    /// Gets the application settings instance for binding to UI controls.
    /// </summary>
    public Settings Settings => Settings.Default;

    /// <summary>
    /// Array of available theme variants that users can select from.
    /// Includes Default, Dark, and Light theme options.
    /// </summary>
    public string[] AvailableThemeVariants { get; } =
    [
        ThemeVariant.Default.ToString(),
        ThemeVariant.Dark.ToString(),
        ThemeVariant.Light.ToString()
    ];

    /// <summary>
    /// A command that will export the entire database as Json-File
    /// </summary>
    [RelayCommand]
    private async Task ExportDataAsync()
    {
        try
        {
            // Show the file save dialog for the user to choose an export location
            var safeFilePickerResult = await this.SafeFileDialogAsync(
                "Export Data", 
                [FileHelper.JsonFileType]);

            if (safeFilePickerResult?.File is {  } storageFile)
            {
                // Open file stream for writing
                await using var fs = await storageFile.OpenWriteAsync();

                try
                {
                    // Export all database data to JSON format
                    await JsonSerializer.SerializeAsync(fs, await data.ExportAsync(), JsonContextHelper.Default.DatabaseDto);
                }
                catch (Exception e)
                {
                    // Show an error if export fails
                    Message = "An error occured during exporting data. " + e.Message;
                    return;
                }

                // Show a success message with the filename
                Message = $"Successfully exported data to '{storageFile.Name}'.";
            }
        }
        catch (Exception e)
        {
            Message = "Could not export the data: " + e.Message;
        }
    }
    
    /// <summary>
    /// A command that will import the selected JSON file into the current database.
    /// Existing items will be updated. 
    /// </summary>
    [RelayCommand]
    private async Task ImportDataAsync()
    {
        try
        {
            // NOTE: Existing items will be updated / overridden. You may want to let the user choose 
            // how to handle it. 
            
            // Show file open dialog for user to select JSON import file
            var openFilePickerResult = await this.OpenFileDialogAsync(
                "Import Data",
                [FileHelper.JsonFileType]);

            if (openFilePickerResult?.FirstOrDefault() is { } storageFile)
            {
                // Open a file stream for reading
                await using var fs = await storageFile.OpenReadAsync();
            
                // Deserialize JSON into database DTO structure
                var dto = await JsonSerializer.DeserializeAsync<DatabaseDto>(fs, 
                    JsonContextHelper.Default.DatabaseDto);

                if (dto is null)
                {
                    throw new FileLoadException("Could not load data");
                }
            
                // Save all categories and ToDoItems from imported data (updates existing ones)
                await data.ImportAsync(dto);

                // Notify other ViewModels about updated DB to refresh their views
                messenger.Send(new UpdateDataMessage<ToDoItem>(UpdateAction.Reset));
                messenger.Send(new UpdateDataMessage<Category>(UpdateAction.Reset));
                Message = $"Imported '{storageFile.Name}'.";
            }
        }
        catch (Exception e)
        {
            Message = "Error importing JSON file: " + e.Message;
        }
    }

    /// <summary>
    /// A command that clears all data from the database.
    /// Shows a confirmation dialog first, then drops, and recreates all tables.
    /// Notifies other ViewModels to refresh their data after completion.
    /// </summary>
    [RelayCommand]
    private async Task ClearDatabaseAsync()
    {
        // Ask on the confirmation page, which clears the database
        await navigator.GoToAsync("/confirm/clear/0");
    }
}