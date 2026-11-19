namespace LuxMonitor.Models;

/// <summary>
/// Photo instantanée d'un disque à un moment donné.
/// Un record est immuable : une mesure ne change pas après avoir été prise.
/// </summary>
public sealed record DiskSnapshot(string RootPath, string Label, double TotalGb, double FreeGb)
{
    public double UsedPercent => TotalGb <= 0 ? 0 : (TotalGb - FreeGb) / TotalGb * 100.0;
}

public enum GpuVendor
{
    Nvidia,
    Amd,
    Intel,
}

/// <summary>
/// Photo instantanée d'une carte graphique.
/// Les valeurs sont nullables (double?) : tous les GPU n'exposent pas tous les capteurs
/// (un GPU Intel intégré n'a souvent ni température ni VRAM dédiée).
/// "Inconnu" n'est pas la même chose que "zéro".
/// </summary>
public sealed record GpuSnapshot(
    string Id,
    string Name,
    GpuVendor Vendor,
    double? LoadPercent,
    double? TemperatureC,
    double? MemoryUsedGb,
    double? MemoryTotalGb);

/// <summary>
/// Photo instantanée de toute la machine. C'est ce que renvoie un capteur,
/// et c'est tout ce que l'interface a besoin de connaître.
/// </summary>
public sealed record SystemSnapshot(
    double CpuPercent,
    double RamUsedGb,
    double RamTotalGb,
    IReadOnlyList<GpuSnapshot> Gpus,
    IReadOnlyList<DiskSnapshot> Disks)
{
    public double RamPercent => RamTotalGb <= 0 ? 0 : RamUsedGb / RamTotalGb * 100.0;
}
