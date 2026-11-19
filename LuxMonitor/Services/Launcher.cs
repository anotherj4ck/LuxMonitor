using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using LuxMonitor.Models;

namespace LuxMonitor.Services;

/// <summary>
/// Ouvre l'outil Windows qui correspond à l'élément cliqué.
/// C'est le cœur de l'idée de Lux Monitor : chaque mesure mène quelque part.
/// </summary>
public static class Launcher
{
    /// <summary>Ouvre un disque dans l'Explorateur de fichiers.</summary>
    public static void OpenFolder(string path)
    {
        // Le chemin vient de Windows (DriveInfo), jamais d'une saisie utilisateur :
        // pas de risque d'injection de commande ici.
        Start("explorer.exe", path);
    }

    /// <summary>Ouvre le Gestionnaire des tâches (onglet Performances en V2).</summary>
    public static void OpenTaskManager() => Start("taskmgr.exe");

    /// <summary>
    /// Ouvre le logiciel du constructeur du GPU s'il est installé,
    /// sinon le Gestionnaire des tâches (qui affiche aussi les GPU).
    /// </summary>
    public static void OpenGpuTool(GpuVendor vendor)
    {
        string? tool = FindGpuTool(vendor);
        if (tool is not null)
            Start(tool);
        else
            OpenTaskManager();
    }

    private static string? FindGpuTool(GpuVendor vendor)
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        // Emplacements d'installation par défaut des logiciels constructeurs.
        string[] candidates = vendor switch
        {
            GpuVendor.Nvidia =>
            [
                Path.Combine(programFiles, "NVIDIA Corporation", "NVIDIA App", "CEF", "NVIDIA App.exe"),
                Path.Combine(programFiles, "NVIDIA Corporation", "NVIDIA GeForce Experience", "NVIDIA GeForce Experience.exe"),
            ],
            GpuVendor.Amd =>
            [
                Path.Combine(programFiles, "AMD", "CNext", "CNext", "RadeonSoftware.exe"),
            ],
            _ => [], // Intel : pas d'emplacement fiable connu pour l'instant, repli sur le Gestionnaire des tâches.
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void Start(string fileName, string arguments = "")
    {
        try
        {
            Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            // Programme introuvable, ou demande d'élévation (UAC) refusée.
            MessageBox.Show($"Impossible d'ouvrir {Path.GetFileName(fileName)}.\n\n{ex.Message}",
                "Lux Monitor", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
