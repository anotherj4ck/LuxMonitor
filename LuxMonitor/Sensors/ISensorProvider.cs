using LuxMonitor.Models;

namespace LuxMonitor.Sensors;

/// <summary>
/// Contrat commun à toutes les sources de mesures.
/// L'interface graphique ne sait pas si les chiffres viennent du vrai matériel
/// ou du mode démo : c'est ce qui permet de travailler l'interface dans une VM.
/// IDisposable : certaines sources ouvrent des accès matériel qu'il faut refermer.
/// </summary>
public interface ISensorProvider : IDisposable
{
    SystemSnapshot Read();
}
