using LuxMonitor.Models;

namespace LuxMonitor.Sensors;

/// <summary>
/// Fausses mesures réalistes, pour travailler l'interface sans vrai matériel
/// (dans une VM, pour des captures d'écran ou des tests).
/// Lancement : LuxMonitor.exe --demo
/// </summary>
public sealed class DemoSensorProvider : ISensorProvider
{
    private readonly Random _random = new();
    private double _cpu = 18;
    private double _ramUsed = 9.5;
    private double _gpuLoad = 35;
    private double _gpuTemp = 52;

    public SystemSnapshot Read()
    {
        // Marche aléatoire : la valeur bouge un peu à chaque lecture, comme une vraie mesure.
        _cpu = Drift(_cpu, 12, 2, 97);
        _ramUsed = Drift(_ramUsed, 0.4, 6, 20);
        _gpuLoad = Drift(_gpuLoad, 15, 0, 99);
        _gpuTemp = Drift(_gpuTemp, 1.5, 38, 82);

        return new SystemSnapshot(new CpuInfo("Core i7-13620H", CpuVendor.Intel), _cpu, _ramUsed, 24,
        [
            new GpuSnapshot("demo-gpu-0", "NVIDIA GeForce RTX 4060 Laptop GPU", GpuVendor.Nvidia,
                _gpuLoad, _gpuTemp, 2.4, 8),
        ],
        [
            new DiskSnapshot(@"C:\", "Système", 476, 182),
            new DiskSnapshot(@"D:\", "Données", 931, 540),
        ]);
    }

    private double Drift(double value, double step, double min, double max)
        => Math.Clamp(value + (_random.NextDouble() - 0.5) * step, min, max);

    public void Dispose() { /* Rien à libérer : aucune ressource ouverte. */ }
}
