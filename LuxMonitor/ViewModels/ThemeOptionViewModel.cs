using LuxMonitor.Localization;
using LuxMonitor.Models;

namespace LuxMonitor.ViewModels;

/// <summary>Une pastille dans le panneau des thèmes.</summary>
public sealed class ThemeOptionViewModel : ObservableObject
{
    private bool _isActive;

    public ThemeOptionViewModel(ThemeDefinition theme)
    {
        Theme = theme;
        Loc.Instance.LanguageChanged += () => OnPropertyChanged(nameof(Name));
    }

    public ThemeDefinition Theme { get; }

    /// <summary>
    /// Nom affiché : traduit pour les thèmes aux noms français (Liquide, Sombre, Clair),
    /// tel quel pour les autres (Tactical Radar, Sakura Kintsugi…).
    /// </summary>
    public string Name => Loc.Has($"Theme.{Theme.Id}") ? Loc.T($"Theme.{Theme.Id}") : Theme.DisplayName;

    // Des chaînes "#AARRGGBB" : WPF les convertit tout seul en couleurs lors du binding.
    public string PreviewBackground => Theme.PreviewBackground;

    public string PreviewAccent => Theme.PreviewAccent;

    /// <summary>Thème actuellement appliqué (affiche une coche).</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
