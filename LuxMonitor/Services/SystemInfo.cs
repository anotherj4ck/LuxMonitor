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
