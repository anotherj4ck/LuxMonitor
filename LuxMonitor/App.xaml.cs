using System.Threading;
using System.Windows;
using LuxMonitor.Services;

namespace LuxMonitor;

/// <summary>
/// Point d'entrée de l'application.
/// Garantit qu'un seul Lux Monitor tourne à la fois : si on le relance alors qu'il est
/// déjà actif (par exemple caché dans la zone de notification), la nouvelle instance
/// demande simplement à l'ancienne de se montrer, puis s'arrête.
/// </summary>
public partial class App : Application
{
    // "Local\" : propre à la session Windows de l'utilisateur.
    private const string MutexName = @"Local\LuxMonitor.SingleInstance";
    private const string ShowEventName = @"Local\LuxMonitor.Show";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool isAdminStartupChange = e.Args.Contains(AdminStartupService.EnableArgument)
                                    || e.Args.Contains(AdminStartupService.DisableArgument);

        // Après une relance en administrateur, l'ancienne instance est en train de se fermer :
        // on lui laisse quelques secondes au lieu de conclure qu'un Lux Monitor tourne déjà.
        if (!TryAcquireSingleInstance(waitForPrevious: isAdminStartupChange))
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        // Seulement maintenant que l'ancienne instance est fermée (elle enregistre ses réglages
        // en quittant, ce qui écraserait les nôtres) : on crée ou supprime la tâche planifiée.
        HandleAdminStartupArgument(e.Args);

        var mainWindow = new MainWindow();

        // L'application s'arrête quand la fenêtre principale se ferme,
        // même si la fenêtre du mode jeu existe encore (cachée).
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        mainWindow.Show();

        ListenForShowRequests(mainWindow);
    }

    private bool TryAcquireSingleInstance(bool waitForPrevious)
    {
        try
        {
            _instanceMutex = new Mutex(initiallyOwned: false, MutexName);
            bool acquired;
            try
            {
                acquired = _instanceMutex.WaitOne(waitForPrevious ? TimeSpan.FromSeconds(10) : TimeSpan.Zero);
            }
            catch (AbandonedMutexException)
            {
                acquired = true; // L'instance précédente a planté sans libérer le verrou : on le récupère.
            }

            if (!acquired)
            {
                _instanceMutex.Dispose();
                _instanceMutex = null;
            }

            return acquired;
        }
        catch (UnauthorizedAccessException)
        {
            // Le verrou appartient à une instance lancée en administrateur : on ne peut même pas l'ouvrir.
            // Cela suffit à savoir qu'un Lux Monitor tourne déjà.
            return false;
        }
    }

    /// <summary>Demande à l'instance déjà lancée de s'afficher (si on en a le droit).</summary>
    private static void SignalExistingInstance()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out EventWaitHandle? existing))
            {
                existing.Set();
                existing.Dispose();
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Instance administrateur : une instance normale n'a pas le droit de la réveiller.
        }
    }

    /// <summary>
    /// Traite "--admin-startup=on/off" (Lux Monitor a été relancé en administrateur pour ça).
    /// Renvoie true si l'argument était présent.
    /// </summary>
    private static bool HandleAdminStartupArgument(string[] args)
    {
        bool enable = args.Contains(AdminStartupService.EnableArgument);
        bool disable = args.Contains(AdminStartupService.DisableArgument);
        if (!enable && !disable)
            return false;

        AppSettings settings = SettingsService.Load();
        settings.HasShownWelcome = true;

        if (enable)
        {
            if (AdminStartupService.Enable())
            {
                // La tâche planifiée remplace la clé "Run" : sinon Lux Monitor serait lancé deux fois.
                StartupService.SetEnabled(false);
                settings.AdminStartupEnabled = true;
            }
            else
            {
                MessageBox.Show("Impossible de créer la tâche planifiée. Lux Monitor continue sans démarrage administrateur.",
                    "Lux Monitor", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else if (AdminStartupService.Disable())
        {
            // On garde un démarrage automatique, mais normal (sans droits admin).
            StartupService.SetEnabled(true);
            settings.AdminStartupEnabled = false;
        }

        SettingsService.Save(settings);
        return true;
    }

    /// <summary>
    /// Attend, sur un fil d'exécution séparé, qu'une autre instance demande à afficher la fenêtre.
    /// </summary>
    private void ListenForShowRequests(MainWindow mainWindow)
    {
        var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showEvent = showEvent; // Gardé en champ pour qu'il vive aussi longtemps que l'application.

        var listener = new Thread(() =>
        {
            try
            {
                while (showEvent.WaitOne())
                {
                    // L'interface ne peut être modifiée que depuis son propre fil : on passe par le Dispatcher.
                    Dispatcher.Invoke(mainWindow.ShowWidget);
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException or TaskCanceledException)
            {
                // L'application se ferme : on arrête d'écouter.
            }
        })
        {
            IsBackground = true, // Ne bloque pas la fermeture de l'application.
            Name = "LuxMonitor.ShowListener",
        };
        listener.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _instanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Verrou déjà libéré : rien à faire.
        }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
