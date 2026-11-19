using System.Management;
using System.Runtime.InteropServices;
using LuxMonitor.Models;

namespace LuxMonitor.Sensors;

/// <summary>
/// Lit les barrettes de RAM installées (fabricant et capacité) via WMI,
/// la base d'informations matérielles de Windows. C'est la même source que la commande
/// PowerShell : Get-CimInstance Win32_PhysicalMemory. Pas de droits administrateur nécessaires.
/// </summary>
public static class RamModuleReader
{
    private const double BytesPerGb = 1024d * 1024d * 1024d;

    // Certains PC (surtout les portables) renvoient un code fabricant JEDEC au lieu du nom.
    // On traduit les plus courants. Les deux ordres d'octets existent selon les BIOS.
    private static readonly (string[] Codes, string Name)[] JedecManufacturers =
    [
        (new[] { "80CE", "CE00" }, "Samsung"),
        (new[] { "80AD", "AD00" }, "SK hynix"),
        (new[] { "802C", "2C00" }, "Micron"),
        (new[] { "859B", "9B85" }, "Crucial"),
        (new[] { "0198", "9801" }, "Kingston"),
        (new[] { "04CB", "CB04" }, "ADATA"),
        (new[] { "029E", "9E02" }, "Corsair"),
        (new[] { "04CD", "CD04" }, "G.Skill"),
    ];

    /// <summary>Barrettes installées, de la plus grande à la plus petite. Liste vide si WMI ne répond pas.</summary>
    public static IReadOnlyList<RamModule> Read()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Capacity FROM Win32_PhysicalMemory");
            var modules = new List<RamModule>();

            using ManagementObjectCollection results = searcher.Get();

            foreach (ManagementBaseObject module in results)
            {
                using (module)
                {
                    ulong bytes = Convert.ToUInt64(module["Capacity"] ?? 0UL);
                    if (bytes == 0)
                        continue;

                    modules.Add(new RamModule(NormalizeManufacturer(module["Manufacturer"] as string), bytes / BytesPerGb));
                }
            }

            return modules.OrderByDescending(m => m.CapacityGb).ToList();
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            // Service WMI indisponible ou bloqué : on n'affiche simplement pas le détail des barrettes.
            return [];
        }
    }

    /// <summary>Transforme ce que renvoie le BIOS ("80AD000080AD", "Hynix", "Unknown"...) en nom lisible.</summary>
    private static string NormalizeManufacturer(string? raw)
    {
        string value = raw?.Trim() ?? "";

        if (value.Length == 0 || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Undefined", StringComparison.OrdinalIgnoreCase))
            return "";

        foreach (var (codes, name) in JedecManufacturers)
        {
            if (codes.Any(code => value.Contains(code, StringComparison.OrdinalIgnoreCase)))
                return name;
        }

        if (value.Contains("hynix", StringComparison.OrdinalIgnoreCase))
            return "SK hynix";
        if (value.StartsWith("Micron", StringComparison.OrdinalIgnoreCase))
            return "Micron";

        return value;
    }
}
