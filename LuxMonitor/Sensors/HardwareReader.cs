using System.Security.Principal;
using System.Text;
using LibreHardwareMonitor.Hardware;
using LuxMonitor.Models;
using LuxMonitor.Services;
using Microsoft.Win32;

namespace LuxMonitor.Sensors;

/// <summary>
/// Lit les capteurs matériels via la bibliothèque LibreHardwareMonitor.
///
/// - Cartes graphiques : passent par les API des constructeurs (NVAPI pour NVIDIA, ADL pour AMD...),
///   donc sans droits administrateur.
/// - Température du processeur : nécessite le pilote PawnIO ET des droits administrateur.
///   Si l'un des deux manque, la valeur reste simplement inconnue (null) : rien ne plante,
///   et l'interface n'affiche pas de température.
/// </summary>
public sealed class HardwareReader : IDisposable
{
    // Noms des capteurs de température CPU selon le fabricant, du plus pertinent au moins pertinent.
    private static readonly string[] CpuTemperatureSensorNames =
    [
        "CPU Package",        // Intel : température globale du processeur
        "Core (Tctl/Tdie)",   // AMD Ryzen
        "Core (Tctl)",        // AMD (variante)
        "Core (Tdie)",        // AMD (variante)
        "Core Max",           // Repli : cœur le plus chaud
        "Core Average",       // Repli : moyenne des cœurs
    ];

    private readonly Computer _computer = new() { IsGpuEnabled = true, IsCpuEnabled = true };
    private readonly bool _isOpen;

    public HardwareReader()
    {
        try
        {
            // Ouverture coûteuse (quelques centaines de ms) : on la fait une seule fois.
            _computer.Open();
            _isOpen = true;
        }
        catch (Exception ex)
        {
            // Si la bibliothèque échoue, Lux Monitor continue sans ces capteurs plutôt que de planter.
            DiagnosticLog.Add("Capteurs", ex);
            _isOpen = false;
        }
    }

    /// <summary>Met à jour tous les capteurs. À appeler une fois par rafraîchissement, avant les lectures.</summary>
    public void Update()
    {
        if (!_isOpen)
            return;

        foreach (IHardware hardware in _computer.Hardware)
        {
            try
            {
                hardware.Update();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Add($"Capteurs/{hardware.HardwareType}", ex);
                // Un composant qui répond mal (pilote en cours de mise à jour...) ne doit pas bloquer les autres.
            }
        }
    }

    /// <summary>Température du processeur en °C, ou null si elle n'est pas disponible.</summary>
    public double? ReadCpuTemperature()
    {
        if (!_isOpen)
            return null;

        IHardware? cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu is null)
            return null;

        foreach (string name in CpuTemperatureSensorNames)
        {
            double? value = Find(cpu, SensorType.Temperature, name);

            // Sans pilote, certains capteurs existent mais valent 0 : on les ignore.
            if (value is > 0)
                return value;
        }

        return null;
    }

    public IReadOnlyList<GpuSnapshot> ReadGpus()
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

        // Carte dédiée en premier : c'est elle qui intéresse l'utilisateur.
        return gpus.OrderBy(g => g.Vendor == GpuVendor.Intel).ToList();
    }

    /// <summary>
    /// Rapport de diagnostic : droits, présence de PawnIO, et liste de tous les capteurs vus.
    /// Sert à comprendre pourquoi une valeur manque sur une machine donnée.
    /// </summary>
    public string BuildDiagnosticReport()
    {
        var report = new StringBuilder();
        bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        report.AppendLine($"Date            : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"Administrateur  : {(isAdmin ? "oui" : "non")}");
        report.AppendLine($"PawnIO installé : {(IsPawnIoInstalled() ? "oui" : "non")}");
        report.AppendLine($"Bibliothèque    : {(_isOpen ? "ouverte" : "échec à l'ouverture")}");
        report.AppendLine();

        if (!_isOpen)
            return report.ToString();

        foreach (IHardware hardware in _computer.Hardware)
        {
            report.AppendLine($"[{hardware.HardwareType}] {hardware.Name}");
            foreach (ISensor sensor in hardware.Sensors.OrderBy(s => s.SensorType).ThenBy(s => s.Name))
                report.AppendLine($"    {sensor.SensorType,-12} {sensor.Name,-28} {sensor.Value?.ToString("0.0") ?? "(vide)"}");
            report.AppendLine();
        }

        return report.ToString();
    }

    /// <summary>PawnIO s'enregistre comme programme installé : on vérifie sa présence dans le registre.</summary>
    private static bool IsPawnIoInstalled()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            return key is not null;
        }
        catch (Exception)
        {
            return false;
        }
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
