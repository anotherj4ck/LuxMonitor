using System.IO;
using System.Security;
using Microsoft.Win32;

namespace LuxMonitor.Services;

/// <summary>
/// Lancement automatique à l'ouverture de session Windows.
/// On utilise la clé "Run" de l'utilisateur courant (HKCU) : c'est le mécanisme standard,
/// visible dans Gestionnaire des tâches > Démarrage, et il ne demande pas de droits administrateur.
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LuxMonitor";

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Active ou désactive le lancement au démarrage. Renvoie false en cas d'échec.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null)
                return false;

            if (enabled)
            {
                // Chemin de l'exécutable en cours, entre guillemets (il peut contenir des espaces).
                if (Environment.ProcessPath is not string exePath)
                    return false;

                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Si le lancement au démarrage est actif mais pointe vers un ancien emplacement
    /// (l'exe a été déplacé ou mis à jour ailleurs), on corrige le chemin.
    /// </summary>
    public static void RefreshPathIfEnabled()
    {
        if (IsEnabled())
            SetEnabled(true);
    }
}
