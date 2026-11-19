namespace LuxMonitor.Models;

/// <summary>
/// Photo instantanée d'un disque à un moment donné.
/// Un record est immuable : une mesure ne change pas après avoir été prise.
/// </summary>
public sealed record DiskSnapshot(string RootPath, string Label, double TotalGb, double FreeGb)
{
    public double UsedPercent => TotalGb <= 0 ? 0 : (TotalGb - FreeGb) / TotalGb * 100.0;
}

/// <summary>Coin de l'écran où s'affiche le mode jeu.</summary>
public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public enum CpuVendor
{
    Unknown,
    Intel,
    Amd,
}

/// <summary>Identité du processeur : ne change pas pendant l'exécution.</summary>
public sealed record CpuInfo(string Name, CpuVendor Vendor);

/// <summary>Images par seconde de l'application au premier plan.</summary>
public sealed record FpsInfo(double Value, string ProcessName);

/// <summary>Une barrette de RAM : fabricant (vide si inconnu) et capacité.</summary>
public sealed record RamModule(string Manufacturer, double CapacityGb);

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
    CpuInfo Cpu,
    double CpuPercent,
    double? CpuTemperatureC,
    double RamUsedGb,
    double RamTotalGb,
    IReadOnlyList<RamModule> RamModules,
    IReadOnlyList<GpuSnapshot> Gpus,
    IReadOnlyList<DiskSnapshot> Disks,
    FpsInfo? Fps)
{
    public double RamPercent => RamTotalGb <= 0 ? 0 : RamUsedGb / RamTotalGb * 100.0;
}
