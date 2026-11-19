using System.Windows;
using LuxMonitor.Models;

namespace LuxMonitor.Services;

/// <summary>
/// Liste des thèmes disponibles, et changement de thème en direct.
/// </summary>
public static class ThemeManager
{
    /// <summary>Catalogue des thèmes. Le premier est celui par défaut.</summary>
    public static IReadOnlyList<ThemeDefinition> Themes { get; } =
    [
        new ThemeDefinition("liquid", "Liquide", "/Themes/Liquid.xaml", UseAcrylic: true, IsDark: true, "#66788899", "#F2FFFFFF"),
        new ThemeDefinition("dark", "Sombre", "/Themes/Dark.xaml", UseAcrylic: false, IsDark: true, "#FF141B2B", "#FF6EA8FE"),
        new ThemeDefinition("light", "Clair", "/Themes/Light.xaml", UseAcrylic: true, IsDark: false, "#FFF7F3EA", "#FFE0B43A"),
    ];

    public static ThemeDefinition Default => Themes[0];

    /// <summary>Retrouve un thème par son identifiant, ou le thème par défaut s'il est inconnu.</summary>
    public static ThemeDefinition FindOrDefault(string? id)
        => Themes.FirstOrDefault(t => t.Id == id) ?? Default;

    /// <summary>
    /// Applique un thème : remplace les couleurs de l'application et règle l'effet Windows.
    /// Grâce aux DynamicResource, tout se redessine instantanément, sans redémarrer.
    /// </summary>
    public static void Apply(Window window, ThemeDefinition theme)
    {
        // Le dictionnaire du thème est toujours en première position (voir App.xaml).
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries[0] = new ResourceDictionary { Source = new Uri(theme.ResourcePath, UriKind.Relative) };

        bool acrylicApplied = WindowEffects.SetBackdrop(window, theme.UseAcrylic, theme.IsDark);

        // Si le thème voulait l'effet Acrylic mais que Windows ne le permet pas (Windows 10...),
        // on utilise le fond opaque de secours défini dans le thème.
        string backgroundKey = theme.UseAcrylic && !acrylicApplied ? "WindowFallbackBrush" : "WindowBackgroundBrush";
        window.SetResourceReference(Window.BackgroundProperty, backgroundKey);
    }
}
