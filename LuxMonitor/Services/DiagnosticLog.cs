using System.Collections.Concurrent;

namespace LuxMonitor.Services;

/// <summary>
/// Petit journal des erreurs "silencieuses" : celles qu'on attrape pour ne pas planter
/// (capteur indisponible, pilote absent...), mais qu'on veut pouvoir retrouver en cas de problème.
/// Son contenu est écrit dans le rapport de diagnostic (voir WindowsSensorProvider).
/// </summary>
public static class DiagnosticLog
{
    private const int MaxEntries = 50; // Évite qu'une erreur répétée chaque seconde remplisse la mémoire.

    private static readonly ConcurrentQueue<string> Entries = new();

    public static void Add(string source, Exception exception)
        => Add(source, $"{exception.GetType().Name} : {exception.Message}");

    public static void Add(string source, string message)
    {
        if (Entries.Count >= MaxEntries)
            return;

        Entries.Enqueue($"{DateTime.Now:HH:mm:ss}  [{source}] {message}");
    }

    public static IReadOnlyList<string> GetEntries() => Entries.ToArray();
}
