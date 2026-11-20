using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace LuxMonitor.Services;

/// <summary>Petites vérifications sur l'état du système, utilisées par l'accueil et le diagnostic.</summary>
public static class SystemInfo
{
    /// <summary>Page officielle de téléchargement de PawnIO.</summary>
    public const string PawnIoWebsite = "https://pawnio.eu";

    /// <summary>Lux Monitor tourne-t-il avec les droits administrateur ?</summary>
    public static bool IsAdministrator()
        => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>
    /// Vrai si écrire à cet emplacement serait risqué : quand Lux Monitor tourne en administrateur,
    /// un dossier ou fichier "point d'analyse" (lien symbolique, jonction) posé par un autre programme
    /// pourrait détourner l'écriture vers un fichier système. Dans ce cas, on n'écrit simplement pas.
    /// </summary>
    public static bool IsUnsafeToWrite(string path)
    {
        if (!IsAdministrator())
            return false; // Sans droits admin, un détournement ne donnerait rien de plus à l'attaquant.

        try
        {
            string? folder = Path.GetDirectoryName(path);
            return (folder is not null && IsReparsePoint(folder)) || (File.Exists(path) && IsReparsePoint(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true; // Dans le doute, on n'écrit pas.
        }
    }

    private static bool IsReparsePoint(string path)
        => File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);

    /// <summary>PawnIO s'enregistre comme programme installé : on vérifie sa présence dans le registre.</summary>
    public static bool IsPawnIoInstalled()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            return key is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
