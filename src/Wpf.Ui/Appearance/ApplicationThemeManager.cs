// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using Wpf.Ui.Controls;

namespace Wpf.Ui.Appearance;

/// <summary>
/// Allows to manage the application theme by swapping resource dictionaries containing dynamic resources with color information.
/// </summary>
/// <example>
/// <code lang="csharp">
/// ApplicationThemeManager.Apply(
///     ApplicationTheme.Light
/// );
/// </code>
/// <code lang="csharp">
/// if (ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark)
/// {
///     ApplicationThemeManager.Apply(
///         ApplicationTheme.Light
///     );
/// }
/// </code>
/// <code>
/// ApplicationThemeManager.Changed += (theme, accent) =>
/// {
///     Debug.WriteLine($"Application theme changed to {theme.ToString()}");
/// };
/// </code>
/// </example>
public static class ApplicationThemeManager
{
    private static ApplicationTheme _cachedApplicationTheme = ApplicationTheme.Unknown;

    internal const string LibraryNamespace = "wpf.ui;";

    internal const string ThemesDictionaryPath = "pack://application:,,,/Wpf.Ui;component/Resources/Theme/";

    /// <summary>
    /// Event triggered when the application's theme is changed.
    /// </summary>
    public static event ThemeChangedEvent? Changed;

    /// <summary>
    /// Gets a value that indicates whether the application is currently using the high contrast theme.
    /// </summary>
    /// <returns><see langword="true"/> if application uses high contrast theme.</returns>
    public static bool IsHighContrast() => _cachedApplicationTheme == ApplicationTheme.HighContrast;

    /// <summary>
    /// Gets a value that indicates whether the Windows is currently using the high contrast theme.
    /// </summary>
    /// <returns><see langword="true"/> if system uses high contrast theme.</returns>
    public static bool IsSystemHighContrast() => SystemThemeManager.HighContrast;

    /// <summary>
    /// Changes the current application theme.
    /// </summary>
    /// <param name="applicationTheme">Theme to set.</param>
    /// <param name="backgroundEffect">Whether the custom background effect should be applied.</param>
    /// <param name="updateAccent">Whether the color accents should be changed.</param>
    /// <param name="forceBackground">If <see langword="true"/>, bypasses the app's theme compatibility check and tries to force the change of a background effect.</param>
    public static void Apply(
        ApplicationTheme applicationTheme,
        WindowBackdropType backgroundEffect = WindowBackdropType.Mica,
        bool updateAccent = true,
        bool forceBackground = false
    )
    {
        if (updateAccent)
        {
            ApplicationAccentColorManager.Apply(
                ApplicationAccentColorManager.GetColorizationColor(),
                applicationTheme,
                false
            );
        }

        if (applicationTheme == ApplicationTheme.Unknown)
        {
            return;
        }

        var appDictionaries = new ResourceDictionaryManager(LibraryNamespace);

        var themeDictionaryName = "Light";

        switch (applicationTheme)
        {
            case ApplicationTheme.Dark:
                themeDictionaryName = "Dark";
                break;
            case ApplicationTheme.HighContrast:
                switch (ApplicationThemeManager.GetSystemTheme())
                {
                    case SystemTheme.HC1:
                        themeDictionaryName = "HC1";
                        break;
                    case SystemTheme.HC2:
                        themeDictionaryName = "HC2";
                        break;
                    case SystemTheme.HCBlack:
                        themeDictionaryName = "HCBlack";
                        break;
                    case SystemTheme.HCWhite:
                    default:
                        themeDictionaryName = "HCWhite";
                        break;
                }

                break;
        }

        var isUpdated = appDictionaries.UpdateDictionary(
            "theme",
            new Uri(ThemesDictionaryPath + themeDictionaryName + ".xaml", UriKind.Absolute)
        );

        //var wpfUiDictionary = appDictionaries.GetDictionary("wpf.ui");

        // Force reloading ALL dictionaries
        // Works but is terrible
        //var isCoreUpdated = appDictionaries.UpdateDictionary(
        //    "wpf.ui",
        //    new Uri(
        //        AppearanceData.LibraryDictionariesUri + "Wpf.Ui.xaml",
        //        UriKind.Absolute
        //    )
        //);

        //var isBrushesUpdated = appDictionaries.UpdateDictionary(
        //        "assets/brushes",
        //        new Uri(
        //            AppearanceData.LibraryDictionariesUri + "Assets/Brushes.xaml",
        //            UriKind.Absolute
        //        )
        //    );

#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"INFO | {typeof(ApplicationThemeManager)} tries to update theme to {themeDictionaryName} ({applicationTheme}): {isUpdated}",
            nameof(ApplicationThemeManager)
        );
#endif
        if (!isUpdated)
        {
            return;
        }

        SystemThemeManager.UpdateSystemThemeCache();

        _cachedApplicationTheme = applicationTheme;

        Changed?.Invoke(applicationTheme, ApplicationAccentColorManager.SystemAccent);

        if (UiApplication.Current.MainWindow is Window mainWindow)
        {
            WindowBackgroundManager.UpdateBackground(
                mainWindow,
                applicationTheme,
                backgroundEffect,
                forceBackground
            );
        }
    }

    /// <summary>
    /// Applies Resources in the <paramref name="frameworkElement"/>.
    /// </summary>
    public static void Apply(FrameworkElement frameworkElement)
    {
        if (frameworkElement is null)
            return;

        var resourcesRemove = frameworkElement
            .Resources.MergedDictionaries.Where(e => e.Source is not null)
            .Where(e => e.Source.ToString().ToLower().Contains(LibraryNamespace))
            .ToArray();

        foreach (var resource in UiApplication.Current.Resources.MergedDictionaries)
        {
            System.Diagnostics.Debug.WriteLine(
                $"INFO | {typeof(ApplicationThemeManager)} Add {resource.Source}",
                "Wpf.Ui.Appearance"
            );
            frameworkElement.Resources.MergedDictionaries.Add(resource);
        }

        foreach (var resource in resourcesRemove)
        {
            System.Diagnostics.Debug.WriteLine(
                $"INFO | {typeof(ApplicationThemeManager)} Remove {resource.Source}",
                "Wpf.Ui.Appearance"
            );
            frameworkElement.Resources.MergedDictionaries.Remove(resource);
        }

        foreach (System.Collections.DictionaryEntry resource in UiApplication.Current.Resources)
        {
            System.Diagnostics.Debug.WriteLine(
                $"INFO | {typeof(ApplicationThemeManager)} Copy Resource {resource.Key} - {resource.Value}",
                "Wpf.Ui.Appearance"
            );
            frameworkElement.Resources[resource.Key] = resource.Value;
        }
    }

    public static void ApplySystemTheme()
    {
        ApplySystemTheme(true);
    }

    public static void ApplySystemTheme(bool updateAccent)
    {
        SystemThemeManager.UpdateSystemThemeCache();

        SystemTheme systemTheme = GetSystemTheme();

        ApplicationTheme themeToSet = ApplicationTheme.Light;

        if (systemTheme is SystemTheme.Dark or SystemTheme.CapturedMotion or SystemTheme.Glow)
        {
            themeToSet = ApplicationTheme.Dark;
        }
        else if (
            systemTheme is SystemTheme.HC1 or SystemTheme.HC2 or SystemTheme.HCBlack or SystemTheme.HCWhite
        )
        {
            themeToSet = ApplicationTheme.HighContrast;
        }

        Apply(themeToSet, updateAccent: updateAccent);
    }

    /// <summary>
    /// Gets currently set application theme.
    /// </summary>
    /// <returns><see cref="ApplicationTheme.Unknown"/> if something goes wrong.</returns>
    public static ApplicationTheme GetAppTheme()
    {
        if (_cachedApplicationTheme == ApplicationTheme.Unknown)
        {
            FetchApplicationTheme();
        }

        return _cachedApplicationTheme;
    }

    /// <summary>
    /// Gets currently set system theme.
    /// </summary>
    /// <returns><see cref="SystemTheme.Unknown"/> if something goes wrong.</returns>
    public static SystemTheme GetSystemTheme()
    {
        return SystemThemeManager.GetCachedSystemTheme();
    }

    /// <summary>
    /// Gets a value that indicates whether the application is matching the system theme.
    /// </summary>
    /// <returns><see langword="true"/> if the application has the same theme as the system.</returns>
    public static bool IsAppMatchesSystem()
    {
        ApplicationTheme appApplicationTheme = GetAppTheme();
        SystemTheme sysTheme = GetSystemTheme();

        return appApplicationTheme switch
        {
            ApplicationTheme.Dark
                => sysTheme is SystemTheme.Dark or SystemTheme.CapturedMotion or SystemTheme.Glow,
            ApplicationTheme.Light
                => sysTheme is SystemTheme.Light or SystemTheme.Flow or SystemTheme.Sunrise,
            _ => appApplicationTheme == ApplicationTheme.HighContrast && SystemThemeManager.HighContrast
        };
    }

    /// <summary>
    /// Checks if the application and the operating system are currently working in a dark theme.
    /// </summary>
    public static bool IsMatchedDark()
    {
        ApplicationTheme appApplicationTheme = GetAppTheme();
        SystemTheme sysTheme = GetSystemTheme();

        if (appApplicationTheme != ApplicationTheme.Dark)
        {
            return false;
        }

        return sysTheme is SystemTheme.Dark or SystemTheme.CapturedMotion or SystemTheme.Glow;
    }

    /// <summary>
    /// Checks if the application and the operating system are currently working in a light theme.
    /// </summary>
    public static bool IsMatchedLight()
    {
        ApplicationTheme appApplicationTheme = GetAppTheme();
        SystemTheme sysTheme = GetSystemTheme();

        if (appApplicationTheme != ApplicationTheme.Light)
        {
            return false;
        }

        return sysTheme is SystemTheme.Light or SystemTheme.Flow or SystemTheme.Sunrise;
    }

    /// <summary>
    /// Tries to guess the currently set application theme.
    /// </summary>
    private static void FetchApplicationTheme()
    {
        ResourceDictionaryManager appDictionaries = new(LibraryNamespace);
        ResourceDictionary? themeDictionary = appDictionaries.GetDictionary("theme");

        if (themeDictionary == null)
        {
            return;
        }

        var themeUri = themeDictionary.Source.ToString().Trim().ToLower();

        if (themeUri.Contains("light"))
        {
            _cachedApplicationTheme = ApplicationTheme.Light;
        }

        if (themeUri.Contains("dark"))
        {
            _cachedApplicationTheme = ApplicationTheme.Dark;
        }

        if (themeUri.Contains("highcontrast"))
        {
            _cachedApplicationTheme = ApplicationTheme.HighContrast;
        }
    }
}
