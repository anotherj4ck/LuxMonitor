using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace LuxMonitor.Localization;

/// <summary>Langues disponibles.</summary>
public enum AppLanguage
{
    French,
    English,
}

/// <summary>
/// Traductions de l'interface, avec changement de langue EN DIRECT.
///
/// Dans le XAML : {loc:Tr Widget.Processor} (voir TrExtension).
/// Dans le code : Loc.T("Clé") pour un texte, Loc.F("Clé", valeurs...) pour un texte avec des nombres.
///
/// Comment le direct fonctionne : le XAML se lie à l'indexeur Loc.Instance["Clé"].
/// Quand la langue change, on prévient WPF que "Item[]" (tous les indexeurs) a changé :
/// chaque texte relit sa traduction, sans rien recréer.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private IReadOnlyDictionary<string, string> _table = Strings.French;

    private Loc()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Prévient le code (textes calculés, menu de l'icône) que la langue a changé.</summary>
    public event Action? LanguageChanged;

    public AppLanguage Language { get; private set; } = AppLanguage.French;

    /// <summary>Format des nombres de la langue active (virgule en français, point en anglais).</summary>
    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Texte traduit. Clé inconnue : on retombe sur le français, puis sur la clé elle-même (visible, donc facile à repérer).</summary>
    public string this[string key]
        => _table.TryGetValue(key, out string? text) ? text
            : Strings.French.TryGetValue(key, out string? french) ? french
            : key;

    public static string T(string key) => Instance[key];

    public static string F(string key, params object?[] args) => string.Format(Instance.Culture, Instance[key], args);

    /// <summary>Vrai si la clé existe (sert pour les noms de thèmes : seuls certains sont traduits).</summary>
    public static bool Has(string key) => Strings.French.ContainsKey(key);

    /// <summary>Français si Windows est en français, anglais pour toutes les autres langues.</summary>
    public static AppLanguage DetectFromWindows()
        => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? AppLanguage.French : AppLanguage.English;

    /// <summary>Code enregistré dans les réglages : "fr", "en", ou null pour "automatique".</summary>
    public static AppLanguage? FromCode(string? code) => code switch
    {
        "fr" => AppLanguage.French,
        "en" => AppLanguage.English,
        _ => null,
    };

    public static string? ToCode(AppLanguage? language) => language switch
    {
        AppLanguage.French => "fr",
        AppLanguage.English => "en",
        _ => null,
    };

    /// <summary>Applique le réglage enregistré (null = langue de Windows).</summary>
    public void Apply(AppLanguage? choice) => SetLanguage(choice ?? DetectFromWindows());

    private void SetLanguage(AppLanguage language)
    {
        Language = language;
        _table = language == AppLanguage.English ? Strings.English : Strings.French;
        Culture = CultureInfo.GetCultureInfo(language == AppLanguage.English ? "en-US" : "fr-FR");

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        LanguageChanged?.Invoke();
    }
}
