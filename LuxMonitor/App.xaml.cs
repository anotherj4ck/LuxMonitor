using System.Threading;
using System.Windows;

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

        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            // Déjà lancé : on réveille l'instance existante, et on s'arrête sans rien afficher.
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out EventWaitHandle? existing))
            {
                existing.Set();
                existing.Dispose();
            }

            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();

        ListenForShowRequests(mainWindow);
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
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
