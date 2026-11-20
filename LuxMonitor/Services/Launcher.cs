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

    /// <summary>
    /// Ouvre l'outil de mise à jour du fabricant du processeur :
    /// le logiciel s'il est installé, sinon la page officielle de détection des pilotes.
    /// </summary>
    public static void OpenCpuTool(CpuVendor vendor)
    {
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        switch (vendor)
        {
            case CpuVendor.Intel:
                // Intel Driver & Support Assistant : au lancement, il ouvre son tableau de bord dans le navigateur.
                StartFirstExisting(
                    [Path.Combine(programFilesX86, "Intel", "Driver and Support Assistant", "DSATray.exe")],
                    fallbackUrl: "https://www.intel.com/content/www/us/en/support/detect.html");
                break;

            case CpuVendor.Amd:
                // AMD Software (Adrenalin) gère aussi les pilotes de chipset Ryzen.
                StartFirstExisting(
                    [Path.Combine(programFiles, "AMD", "CNext", "CNext", "RadeonSoftware.exe")],
                    fallbackUrl: "https://www.amd.com/en/support/download/drivers.html");
                break;

            default:
                OpenTaskManager();
                break;
        }
    }

    /// <summary>Lance le premier programme trouvé ; sinon ouvre la page web officielle dans le navigateur.</summary>
    private static void StartFirstExisting(string[] candidates, string fallbackUrl)
    {
        string? program = candidates.FirstOrDefault(File.Exists);

        // Adresse écrite en dur dans le code, en https : on ne lance jamais une URL venue de l'extérieur.
        Start(program ?? fallbackUrl);
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
            MessageBox.Show(Localization.Loc.F("Error.CannotOpen", Path.GetFileName(fileName), ex.Message),
                "Lux Monitor", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
