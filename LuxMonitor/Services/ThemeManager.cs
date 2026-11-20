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
        new ThemeDefinition("tactical", "Tactical Radar", "/Themes/TacticalRadar.xaml", UseAcrylic: true, IsDark: true, "#FF0B1F12", "#FF4DFF73"),
        new ThemeDefinition("cyber", "Cyber HUD", "/Themes/CyberHud.xaml", UseAcrylic: true, IsDark: true, "#FF06161E", "#FF2EE6FF"),
        new ThemeDefinition("kawaii", "Kawaii Neko", "/Themes/KawaiiNeko.xaml", UseAcrylic: false, IsDark: false, "#FFFBDDE6", "#FFEE7A9E"),
        // Nom provisoire : Gaël cherche encore le nom définitif. Ne pas changer l'identifiant "crimson".
        new ThemeDefinition("crimson", "Lune Rouge", "/Themes/LuneRouge.xaml", UseAcrylic: false, IsDark: true, "#FF140B0D", "#FFE0283C"),
        new ThemeDefinition("sakura", "Sakura Kintsugi", "/Themes/SakuraKintsugi.xaml", UseAcrylic: false, IsDark: true, "#FF2A1216", "#FFE2B66A"),
        new ThemeDefinition("sunset", "Sunset Minimal", "/Themes/SunsetMinimal.xaml", UseAcrylic: false, IsDark: false, "#FFF6CDBA", "#FFE8604A"),
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
        Current = theme;

        // Le dictionnaire du thème est toujours en première position (voir App.xaml).
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries[0] = new ResourceDictionary { Source = new Uri(theme.ResourcePath, UriKind.Relative) };

        ApplyBackdrop(window);
    }

    /// <summary>Thème actuellement appliqué.</summary>
    public static ThemeDefinition Current { get; private set; } = Themes[0];

    /// <summary>
    /// Applique l'effet Windows du thème actif (Acrylic ou fond opaque) à une fenêtre.
    /// Sert aussi pour les fenêtres secondaires (accueil...), pour qu'elles ressemblent au widget.
    /// </summary>
    public static void ApplyBackdrop(Window window)
    {
        ThemeDefinition theme = Current;
        bool acrylicApplied = WindowEffects.SetBackdrop(window, theme.UseAcrylic, theme.IsDark);

        // Si le thème voulait l'effet Acrylic mais que Windows ne le permet pas (Windows 10...),
        // on utilise le fond opaque de secours défini dans le thème.
        string backgroundKey = theme.UseAcrylic && !acrylicApplied ? "WindowFallbackBrush" : "WindowBackgroundBrush";
        window.SetResourceReference(Window.BackgroundProperty, backgroundKey);
    }
}
