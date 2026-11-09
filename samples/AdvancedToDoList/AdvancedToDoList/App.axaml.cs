using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System.Linq;
using AdvancedToDoList.Services;
using Avalonia.Markup.Xaml;
using AdvancedToDoList.ViewModels;
using AdvancedToDoList.Properties;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedToDoList;

/// <summary>
/// Main application entry point for the Advanced To-Do List app.
/// Handles service registration, settings management, theme updates, and UI initialization.
/// </summary>
/// <remarks>
/// Key responsibilities:
/// - Registers all application services via DI (CategoryService, ToDoItemService, DatabaseService)
/// - Manages settings persistence and live UI updates (e.g., accent color changes)
/// - Handles platform-specific UI initialization (desktop vs single-view)
/// - Configures data validation strategy (disables Avalonia's built-in validation to avoid duplicates)
/// 
/// Service registration pattern:
/// - Services are registered only if not already registered (prevents duplicates)
/// - Settings.Default is loaded after DI container is built
/// - Design mode uses a separate service collection to avoid runtime dependencies
/// 
/// Theme management:
/// - Accent color from settings is applied to all FluentTheme palettes
/// - Changes are reactive — updating settings updates the UI immediately
/// 
/// Data validation:
/// - Both Avalonia and CommunityToolkit.Mvvm have DataAnnotation validators
/// - This app uses CommunityToolkit's [NotifyDataErrorInfo], so we disable Avalonia's to avoid duplicate errors
/// </remarks>
public partial class App : Application
{
    /// <summary>
    /// Global service provider for the application.
    /// This is the primary way to access registered services from anywhere in the app.
    /// </summary>
    /// <remarks>
    /// Why static?
    /// - Allows access to services from ViewModels without constructor injection
    /// - Useful for fire-and-forget scenarios or design-time data
    /// 
    /// Initialization:
    /// - Set during RegisterAppServices() after BuildServiceProvider()
    /// - Never null after first successful initialization (enforced by ! in property declaration)
    /// </remarks>
    public static IServiceProvider Services { get; set; } = null!;

    /// <summary>
    /// Initializes the application resources and services.
    /// Called before OnFrameworkInitializationCompleted().
    /// </summary>
    /// <remarks>
    /// Design mode handling:
    /// - When running in VS Designer or Blend, uses DesignDbService
    /// - This provides sample data without connecting to the real database
    /// - Design mode services are registered in a temporary ServiceCollection
    /// 
    /// Settings initialization:
    /// - Subscribes to property changes for live theme updates
    /// - Loads the XAML view hierarchy (AvaloniaXamlLoader.Load)
    /// - Applies initial accent color from settings
    /// 
    /// Why Initialize() vs OnFrameworkInitializationCompleted():
    /// - Initialize() is for lightweight setup (resources, settings)
    /// - OnFrameworkInitializationCompleted() is for UI creation (MainWindow/MainView)
    /// </remarks>
    public override void Initialize()
    {
        // Subscribe to settings changes so UI updates immediately when accent color changes
        Settings.Default.PropertyChanged += SettingsOnPropertyChanged;
        
        // Load the XAML resources and views
        AvaloniaXamlLoader.Load(this);
        
        // Apply the initial accent color from settings
        UpdateAccentColor(Settings.Default.AccentColor);
    }

    /// <summary>
    /// Handles property changes from Settings to update the UI in real-time.
    /// Currently only handles accent color changes.
    /// </summary>
    /// <param name="sender">The settings object that changed</param>
    /// <param name="e">The property name that changed</param>
    /// <remarks>
    /// Why this pattern?
    /// - Keeps settings and UI in sync without user having to restart
    /// - Only updates the specific property that changed
    /// - Uses a switch statement for easy extensibility (add more properties later)
    /// </remarks>
    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Settings.Default.AccentColor):
                UpdateAccentColor((sender as Settings)?.AccentColor);
                break;
        }
    }

    /// <summary>
    /// Updates the FluentTheme accent color across all palettes.
    /// This makes the app match the user's selected accent color.
    /// </summary>
    /// <param name="accentColor">The new accent color from settings</param>
    /// <remarks>
    /// How it works:
    /// - Finds the FluentTheme in the application styles
    /// - Updates every palette's Accent property (Primary, Secondary, etc.)
    /// - Avalonia automatically re-renders with the new color
    /// 
    /// Why check for null?
    /// - Theme might not exist yet (during startup)
    /// - Accent color might be unset (first run)
    /// - Both are handled gracefully by early returns
    /// </remarks>
    private void UpdateAccentColor(Color? accentColor)
    {
        var fluenttheme = Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluenttheme is null || accentColor is null)
            return;
        foreach (var palette in fluenttheme.Palettes.Values)
        {
            palette.Accent = accentColor.Value;
        }
    }
}
