using LuxMonitor.Models;

namespace LuxMonitor.ViewModels;

/// <summary>Une pastille dans le panneau des thèmes.</summary>
public sealed class ThemeOptionViewModel : ObservableObject
{
    private bool _isActive;

    public ThemeOptionViewModel(ThemeDefinition theme) => Theme = theme;

    public ThemeDefinition Theme { get; }

    public string Name => Theme.DisplayName;

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
