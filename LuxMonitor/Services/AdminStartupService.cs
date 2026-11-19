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
/// Tâche visible et supprimable dans : Planificateur de tâches > Bibliothèque > "Lux Monitor".
/// </summary>
public static class AdminStartupService
{
    public const string TaskName = "Lux Monitor";

    // Arguments de ligne de commande utilisés pour se relancer en administrateur.
    public const string EnableArgument = "--admin-startup=on";
    public const string DisableArgument = "--admin-startup=off";

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

    /// <summary>Crée (ou remplace) la tâche planifiée. À appeler depuis un processus administrateur.</summary>
    public static bool Enable()
    {
        if (Environment.ProcessPath is not string exePath)
            return false;

        string xmlPath = Path.Combine(Path.GetTempPath(), $"LuxMonitor-task-{Guid.NewGuid():N}.xml");
        try
        {
            // Format XML : le seul qui permet de régler tous les détails ci-dessous
            // (la commande schtasks seule ne le permet pas).
            File.WriteAllText(xmlPath, BuildTaskXml(exePath), Encoding.Unicode);
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

    /// <summary>Supprime la tâche planifiée. À appeler depuis un processus administrateur.</summary>
    public static bool Disable() => RunSchtasks($"/Delete /TN \"{TaskName}\" /F");

    private static bool RunSchtasks(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("schtasks.exe", arguments)
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
    /// </summary>
    private static string BuildTaskXml(string exePath)
    {
        string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        string command = SecurityElement.Escape(exePath);

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
                </Exec>
              </Actions>
            </Task>
            """;
    }
}
