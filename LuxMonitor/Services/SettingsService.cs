using System.IO;
using System.Text.Json;
using LuxMonitor.Models;

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

    /// <summary>Fenêtre figée : ni déplacement, ni redimensionnement (les clics sur les tuiles marchent toujours).</summary>
    public bool IsWindowLocked { get; set; }

    /// <summary>Langue choisie : "fr", "en", ou null pour suivre la langue de Windows.</summary>
    public string? Language { get; set; }

    /// <summary>Coin de l'écran du mode jeu.</summary>
    public OverlayCorner OverlayCorner { get; set; } = OverlayCorner.TopLeft;

    /// <summary>Position libre du mode jeu, si l'utilisateur l'a déplacé (prioritaire sur le coin).</summary>
    public double? OverlayLeft { get; set; }
    public double? OverlayTop { get; set; }

    /// <summary>La bulle d'explication du mode jeu a déjà été montrée.</summary>
    public bool HasShownOverlayHint { get; set; }

    /// <summary>La fenêtre d'accueil a déjà été montrée (premier lancement).</summary>
    public bool HasShownWelcome { get; set; }

    /// <summary>Démarrage en administrateur via la tâche planifiée (température CPU et FPS).</summary>
    public bool AdminStartupEnabled { get; set; }

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

            // En administrateur, on refuse d'écrire à travers un lien posé par un autre programme.
            if (SystemInfo.IsUnsafeToWrite(FilePath))
                return;

            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Impossible d'enregistrer : le thème marche quand même, il ne sera juste pas mémorisé.
        }
    }
}
