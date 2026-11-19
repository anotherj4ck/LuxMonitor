using System.IO;
using System.Text.Json;

namespace LuxMonitor.Services;

/// <summary>Réglages enregistrés entre deux lancements.</summary>
public sealed class AppSettings
{
    public string ThemeId { get; set; } = "liquid";

    /// <summary>Dernière position de la fenêtre (null = jamais déplacée).</summary>
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    /// <summary>Largeur choisie par l'utilisateur (la hauteur s'adapte toute seule).</summary>
    public double? WindowWidth { get; set; }

    /// <summary>La bulle "Lux Monitor continue dans la zone de notification" a déjà été montrée.</summary>
    public bool HasShownTrayHint { get; set; }
}

/// <summary>
/// Lit et écrit les réglages dans %AppData%\LuxMonitor\settings.json.
/// AppData (et pas le dossier du programme) : c'est l'emplacement prévu par Windows
/// pour les réglages d'un utilisateur, accessible sans droits administrateur.
/// </summary>
public static class SettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LuxMonitor",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Fichier illisible ou abîmé : on repart des réglages par défaut plutôt que de planter.
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Impossible d'enregistrer : le thème marche quand même, il ne sera juste pas mémorisé.
        }
    }
}
