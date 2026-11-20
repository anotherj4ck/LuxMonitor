using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace LuxMonitor.Services;

/// <summary>
/// Démarrage de Lux Monitor EN ADMINISTRATEUR à l'ouverture de session, sans écran UAC à chaque fois.
///
/// La clé "Run" du registre ne sait pas lancer un programme en administrateur.
/// On passe donc par le Planificateur de tâches, avec une tâche "exécuter avec les privilèges
/// les plus élevés". La créer demande une seule fois les droits admin (un écran UAC) ;
/// ensuite, Windows la lance tout seul à chaque ouverture de session.
///
/// SÉCURITÉ (audit v0.2) — une tâche qui lance un programme en administrateur est une cible :
/// si un programme malveillant, même sans droits, peut modifier ce que la tâche lance,
/// il obtient les droits administrateur à la prochaine ouverture de session (élévation de privilèges).
/// D'où trois protections :
///  1. La tâche ne lance JAMAIS l'exe d'origine (Téléchargements, Bureau… modifiables sans droits),
///     mais une copie dans C:\Program Files\LuxMonitor, dossier que seul un administrateur peut modifier.
///  2. Un exe "fichier unique" extrait ses DLL dans un dossier temporaire de l'utilisateur, lui aussi
///     modifiable sans droits. Pour l'instance admin, on redirige cette extraction dans Program Files
///     (variable DOTNET_BUNDLE_EXTRACT_BASE_DIR), sinon une DLL piégée y serait chargée en admin.
///  3. Les outils système (schtasks, cmd) sont appelés par leur chemin complet dans System32 :
///     un faux "schtasks.exe" posé à côté de Lux Monitor ne peut pas être lancé à sa place.
///
/// Tâche visible et supprimable dans : Planificateur de tâches > Bibliothèque > "Lux Monitor".
/// </summary>
public static class AdminStartupService
{
    public const string TaskName = "Lux Monitor";

    // Arguments de ligne de commande utilisés pour se relancer en administrateur.
    public const string EnableArgument = "--admin-startup=on";
    public const string DisableArgument = "--admin-startup=off";

    /// <summary>Dossier protégé : seuls les administrateurs peuvent y écrire.</summary>
    public static string InstallFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LuxMonitor");

    /// <summary>La copie de Lux Monitor que lance la tâche planifiée.</summary>
    public static string InstalledExePath { get; } = Path.Combine(InstallFolder, "LuxMonitor.exe");

    /// <summary>Où l'instance admin extrait ses DLL (au lieu du dossier temporaire de l'utilisateur).</summary>
    private static string RuntimeFolder => Path.Combine(InstallFolder, "runtime");

    /// <summary>Chemin complet d'un outil de System32 (jamais un simple nom, voir protection n° 3).</summary>
    private static string SystemTool(string fileName) => Path.Combine(Environment.SystemDirectory, fileName);

    /// <summary>
    /// Relance Lux Monitor en administrateur (écran UAC) avec une action à effectuer.
    /// Renvoie false si l'utilisateur a refusé l'écran UAC.
    /// </summary>
    public static bool RestartElevated(string argument)
    {
        if (Environment.ProcessPath is not string exePath)
            return false;

        try
        {
            // "runas" : demande à Windows de lancer le programme en administrateur.
            Process.Start(new ProcessStartInfo(exePath, argument) { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch (Win32Exception)
        {
            return false; // UAC refusé.
        }
    }

    /// <summary>
    /// Installe la copie protégée et crée (ou remplace) la tâche planifiée.
    /// À appeler depuis un processus administrateur.
    /// </summary>
    public static bool Enable()
    {
        if (!SystemInfo.IsAdministrator() || Environment.ProcessPath is not string exePath)
            return false;

        // Le XML de la tâche est écrit DANS le dossier protégé : dans %TEMP%, un autre programme
        // pourrait le modifier entre son écriture et sa lecture par schtasks.
        string xmlPath = Path.Combine(InstallFolder, "task.xml");
        try
        {
            Directory.CreateDirectory(InstallFolder);

            // Copie de l'exe actuel (sauf si c'est déjà la copie installée qui tourne).
            // Une mise à jour = relancer "Activer" depuis le nouvel exe : la copie est remplacée.
            if (!string.Equals(Path.GetFullPath(exePath), Path.GetFullPath(InstalledExePath), StringComparison.OrdinalIgnoreCase))
                File.Copy(exePath, InstalledExePath, overwrite: true);

            File.WriteAllText(xmlPath, BuildTaskXml(), Encoding.Unicode);
            return RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Add("Démarrage admin", ex);
            return false;
        }
        finally
        {
            try { File.Delete(xmlPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Lance la tâche planifiée maintenant. Possible SANS être administrateur : la tâche appartient
    /// à l'utilisateur, et c'est elle qui porte les droits. Sert quand on double-clique sur l'exe
    /// alors que le démarrage admin est activé : on obtient l'instance admin, sans écran UAC.
    /// </summary>
    public static bool RunTask() => RunSchtasks($"/Run /TN \"{TaskName}\"");

    /// <summary>
    /// Supprime la tâche planifiée, puis la copie protégée (au mieux : si cette copie est
    /// en cours d'exécution, Windows refuse de l'effacer ; elle reste alors, sans danger).
    /// À appeler depuis un processus administrateur.
    /// </summary>
    public static bool Disable()
    {
        bool removed = RunSchtasks($"/Delete /TN \"{TaskName}\" /F");

        try
        {
            if (Directory.Exists(InstallFolder))
                Directory.Delete(InstallFolder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Add("Démarrage admin", $"Dossier {InstallFolder} non supprimé : {ex.Message}");
        }

        return removed;
    }

    private static bool RunSchtasks(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(SystemTool("schtasks.exe"), arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true, // Pas de fenêtre noire qui s'ouvre et se ferme.
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            using Process? process = Process.Start(startInfo);
            if (process is null)
                return false;

            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(10_000);

            if (process.ExitCode != 0)
                DiagnosticLog.Add("Démarrage admin", $"schtasks {arguments} → code {process.ExitCode} : {error.Trim()}");

            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            DiagnosticLog.Add("Démarrage admin", ex);
            return false;
        }
    }

    /// <summary>
    /// Définition de la tâche. Les réglages importants (et leurs pièges) :
    /// - RunLevel HighestAvailable : lancement en administrateur, sans UAC.
    /// - LogonType InteractiveToken : uniquement quand l'utilisateur est connecté, sur son bureau
    ///   (une tâche "en arrière-plan" ne pourrait pas afficher de fenêtre).
    /// - DisallowStartIfOnBatteries / StopIfGoingOnBatteries à false : par défaut, Windows
    ///   n'exécute PAS les tâches sur batterie. Sur un portable, Lux Monitor ne démarrerait pas !
    /// - ExecutionTimeLimit PT0S : par défaut, Windows arrête une tâche au bout de 3 jours.
    /// - Delay 10 s : laisse le temps au bureau et à la zone de notification d'être prêts.
    ///
    /// L'action passe par cmd.exe, seul moyen de définir une variable d'environnement avant
    /// le lancement (protection n° 2). "/d" empêche cmd d'exécuter les commandes "AutoRun"
    /// du registre de l'utilisateur, qui seraient sinon lancées… en administrateur.
    /// Contrepartie : une console peut apparaître une fraction de seconde à l'ouverture de session.
    /// </summary>
    private static string BuildTaskXml()
    {
        string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        string command = SecurityElement.Escape(SystemTool("cmd.exe"));
        string arguments = SecurityElement.Escape(
            $"/d /c set \"DOTNET_BUNDLE_EXTRACT_BASE_DIR={RuntimeFolder}\" && start \"\" \"{InstalledExePath}\"");

        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Lance Lux Monitor avec les droits administrateur à l'ouverture de session (température du processeur et FPS).</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                  <Delay>PT10S</Delay>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
                <Enabled>true</Enabled>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{command}</Command>
                  <Arguments>{arguments}</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;
    }
}
