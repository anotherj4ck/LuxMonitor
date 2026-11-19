using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LuxMonitor.Models;
using LuxMonitor.Services;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace LuxMonitor.Sensors;

/// <summary>
/// Mesure les FPS de l'application au premier plan, sans rien injecter dans le jeu.
///
/// Principe (le même que l'outil PresentMon de Microsoft/Intel) : à chaque image affichée,
/// DirectX signale un événement "Present" dans ETW (Event Tracing for Windows), le système
/// de traçage intégré à Windows. On compte ces événements par processus : images par seconde = FPS.
///
/// C'est une écoute passive : aucun risque avec les anti-triche.
/// Contrainte : ETW en temps réel exige les droits administrateur. Sans eux, FPS indisponibles (null).
/// </summary>
public sealed class FpsMeter : IDisposable
{
    private const string SessionName = "LuxMonitor-FPS";

    // Fournisseurs ETW de DirectX (identifiants publics, utilisés aussi par PresentMon).
    private static readonly Guid DxgiProvider = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9"); // DirectX 10/11/12
    private static readonly Guid D3D9Provider = new("783ACA0A-790E-4D7F-8451-AA850511C6B9"); // DirectX 9

    private const int DxgiPresentStart = 42;
    private const int DxgiPresentMultiplaneOverlayStart = 55;
    private const int D3D9PresentStart = 1;

    // Processus qui affichent des images mais ne sont pas des jeux : on n'affiche pas leurs FPS.
    private static readonly HashSet<string> IgnoredProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "dwm", "LuxMonitor", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost",
    };

    // Fenêtre de calcul : on compte les images des 1 dernières secondes (horodatage ETW).
    private const double MeasureWindowMs = 1000;
    // On garde un peu plus d'historique que la fenêtre, pour absorber les retards de livraison.
    private const double HistoryMs = 2500;
    // Sans aucune image reçue depuis ce délai, l'application ne dessine plus : on masque les FPS.
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(3);

    private readonly TraceEventSession? _session;

    /// <summary>État de la mesure, pour le rapport de diagnostic.</summary>
    public string Status { get; } = "non démarrés";
    private readonly Dictionary<int, string> _processNames = [];
    private readonly int _ownProcessId = Environment.ProcessId;

    // Historique des images par processus. Rempli par le fil ETW, lu par Read() : dictionnaire "concurrent".
    private readonly ConcurrentDictionary<int, FrameHistory> _frames = new();
    private int _lastTargetProcessId;
    private long _eventsReceived;

    /// <summary>Nombre d'événements DirectX reçus depuis le lancement (diagnostic).</summary>
    public long EventsReceived => Interlocked.Read(ref _eventsReceived);

    /// <summary>
    /// Horodatages des dernières images d'un processus.
    /// Pourquoi des horodatages plutôt qu'un simple compteur par seconde : ETW livre les événements
    /// par paquets (ses tampons sont vidés environ une fois par seconde). Un compteur "par seconde"
    /// tombait donc parfois à 0, puis doublait : d'où les FPS qui disparaissaient. Avec l'heure exacte
    /// de chaque image, le calcul reste juste même si les événements arrivent en retard.
    /// </summary>
    private sealed class FrameHistory
    {
        private readonly Queue<double> _timestampsMs = new();
        private readonly object _sync = new();

        /// <summary>Heure (horloge du PC) de réception du dernier paquet d'images.</summary>
        public DateTime LastReceivedUtc { get; private set; }

        public void Add(double timestampMs)
        {
            lock (_sync)
            {
                _timestampsMs.Enqueue(timestampMs);
                while (_timestampsMs.Count > 0 && timestampMs - _timestampsMs.Peek() > HistoryMs)
                    _timestampsMs.Dequeue();
                LastReceivedUtc = DateTime.UtcNow;
            }
        }

        /// <summary>Images par seconde sur la dernière seconde d'horodatages connus.</summary>
        public double? ComputeFps()
        {
            lock (_sync)
            {
                if (_timestampsMs.Count < 2)
                    return null;

                double newest = _timestampsMs.Last();
                double[] recent = _timestampsMs.Where(t => newest - t <= MeasureWindowMs).ToArray();
                if (recent.Length < 2)
                    return null;

                double spanSeconds = (recent[^1] - recent[0]) / 1000.0;
                return spanSeconds > 0 ? (recent.Length - 1) / spanSeconds : null;
            }
        }
    }

    public FpsMeter()
    {
        if (TraceEventSession.IsElevated() != true)
        {
            Status = "désactivés (pas administrateur)";
            return; // Pas administrateur : pas de FPS, sans erreur.
        }

        try
        {
            // Une session au même nom peut traîner si Lux Monitor a planté : on la ferme d'abord.
            TraceEventSession.GetActiveSession(SessionName)?.Stop();

            var session = new TraceEventSession(SessionName) { StopOnDispose = true };
            session.EnableProvider(DxgiProvider, TraceEventLevel.Verbose);
            session.EnableProvider(D3D9Provider, TraceEventLevel.Verbose);
            session.Source.AllEvents += OnEvent;

            // Process() bloque tant que la session tourne : on lui donne son propre fil.
            var listener = new Thread(() =>
            {
                try
                {
                    session.Source.Process();
                }
                catch (Exception ex)
                {
                    // Session arrêtée (fermeture de l'appli) : fin normale de l'écoute.
                    DiagnosticLog.Add("FPS/écoute", ex);
                }
            })
            {
                IsBackground = true,
                Name = "LuxMonitor.Fps",
            };
            listener.Start();

            _session = session;
            Status = "actifs";
        }
        catch (Exception ex)
        {
            // Fonction facultative : quelle que soit l'erreur, Lux Monitor continue sans FPS.
            DiagnosticLog.Add("FPS", ex);
            Status = $"erreur au démarrage ({ex.GetType().Name})";
            _session = null;
        }
    }

    private void OnEvent(TraceEvent data)
    {
        _eventsReceived++;
        int id = (int)data.ID;
        bool isPresent =
            (data.ProviderGuid == DxgiProvider && (id == DxgiPresentStart || id == DxgiPresentMultiplaneOverlayStart))
            || (data.ProviderGuid == D3D9Provider && id == D3D9PresentStart);

        if (isPresent)
            _frames.GetOrAdd(data.ProcessID, _ => new FrameHistory()).Add(data.TimeStampRelativeMSec);
    }

    /// <summary>FPS de l'application au premier plan, ou null si rien à afficher.</summary>
    public FpsInfo? Read()
    {
        if (_session is null)
            return null;

        RemoveOldProcesses();

        // Si on clique sur Lux Monitor, on garde les FPS du jeu qui était au premier plan juste avant.
        int processId = GetForegroundProcessId();
        if (processId == 0 || processId == _ownProcessId)
            processId = _lastTargetProcessId;
        else
            _lastTargetProcessId = processId;

        if (processId == 0 || !_frames.TryGetValue(processId, out FrameHistory? history))
            return null;

        // Plus d'images depuis 3 secondes (jeu en pause, réduit...) : on masque.
        if (DateTime.UtcNow - history.LastReceivedUtc > StaleAfter)
            return null;

        if (history.ComputeFps() is not double fps)
            return null;

        string name = GetProcessName(processId);
        if (name.Length == 0 || IgnoredProcesses.Contains(name))
            return null;

        return new FpsInfo(fps, name);
    }

    /// <summary>Oublie les processus qui n'affichent plus rien depuis une minute (fermés, en pause...).</summary>
    private void RemoveOldProcesses()
    {
        DateTime limit = DateTime.UtcNow - TimeSpan.FromMinutes(1);
        foreach (var (processId, history) in _frames)
        {
            if (history.LastReceivedUtc < limit)
                _frames.TryRemove(processId, out _);
        }
    }

    private string GetProcessName(int processId)
    {
        if (_processNames.TryGetValue(processId, out string? cached))
            return cached;

        // Windows réutilise les numéros de processus : on vide le cache de temps en temps.
        if (_processNames.Count > 200)
            _processNames.Clear();

        string name;
        try
        {
            using var process = Process.GetProcessById(processId);
            name = process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            name = ""; // Processus déjà terminé.
        }

        _processNames[processId] = name;
        return name;
    }

    private static int GetForegroundProcessId()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero)
            return 0;

        GetWindowThreadProcessId(window, out uint processId);
        return (int)processId;
    }

    public void Dispose() => _session?.Dispose(); // Arrête la session ETW (StopOnDispose).

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
