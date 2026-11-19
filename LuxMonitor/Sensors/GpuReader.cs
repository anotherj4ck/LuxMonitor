using LibreHardwareMonitor.Hardware;
using LuxMonitor.Models;

namespace LuxMonitor.Sensors;

/// <summary>
/// Lit les cartes graphiques via la bibliothèque LibreHardwareMonitor.
/// Elle passe par les API des constructeurs (NVAPI pour NVIDIA, ADL pour AMD...),
/// donc pas besoin de droits administrateur pour le GPU.
/// </summary>
public sealed class GpuReader : IDisposable
{
    private readonly Computer _computer = new() { IsGpuEnabled = true };
    private readonly bool _isOpen;

    public GpuReader()
    {
        try
        {
            // Ouverture coûteuse (quelques centaines de ms) : on la fait une seule fois.
            _computer.Open();
            _isOpen = true;
        }
        catch (Exception)
        {
            // Si la bibliothèque échoue, Lux Monitor continue sans GPU plutôt que de planter.
            _isOpen = false;
        }
    }

    public IReadOnlyList<GpuSnapshot> Read()
    {
        if (!_isOpen)
            return [];

        var gpus = new List<GpuSnapshot>();

        foreach (IHardware hardware in _computer.Hardware)
        {
            GpuVendor? vendor = hardware.HardwareType switch
            {
                HardwareType.GpuNvidia => GpuVendor.Nvidia,
                HardwareType.GpuAmd => GpuVendor.Amd,
                HardwareType.GpuIntel => GpuVendor.Intel,
                _ => null,
            };

            if (vendor is null)
                continue;

            try
            {
                hardware.Update(); // Rafraîchit les valeurs des capteurs de ce GPU.

                gpus.Add(new GpuSnapshot(
                    hardware.Identifier.ToString(),
                    hardware.Name,
                    vendor.Value,
                    LoadPercent: Find(hardware, SensorType.Load, "GPU Core")
                                 ?? Find(hardware, SensorType.Load, "D3D 3D"), // nom utilisé par les GPU Intel
                    TemperatureC: Find(hardware, SensorType.Temperature, "GPU Core"),
                    MemoryUsedGb: Find(hardware, SensorType.SmallData, "GPU Memory Used") / 1024,
                    MemoryTotalGb: Find(hardware, SensorType.SmallData, "GPU Memory Total") / 1024));
            }
            catch (Exception)
            {
                // Un GPU qui répond mal (pilote en cours de mise à jour...) ne doit pas bloquer les autres.
            }
        }

        // Carte dédiée en premier : c'est elle qui intéresse l'utilisateur.
        return gpus.OrderBy(g => g.Vendor == GpuVendor.Intel).ToList();
    }

    /// <summary>Cherche un capteur par type et par nom. Renvoie null s'il n'existe pas.</summary>
    private static double? Find(IHardware hardware, SensorType type, string name)
        => hardware.Sensors.FirstOrDefault(s => s.SensorType == type && s.Name == name)?.Value;

    public void Dispose()
    {
        if (_isOpen)
            _computer.Close();
    }
}
