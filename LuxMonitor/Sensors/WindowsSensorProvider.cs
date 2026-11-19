using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using LuxMonitor.Models;

namespace LuxMonitor.Sensors;

/// <summary>
/// Lit les vraies mesures de Windows, sans pilote et sans droits administrateur.
/// CPU et RAM passent par deux fonctions de kernel32.dll (API Windows native),
/// les disques par DriveInfo (.NET), les GPU et la température CPU par LibreHardwareMonitor (voir HardwareReader).
/// </summary>
public sealed class WindowsSensorProvider : ISensorProvider
{
    private const double BytesPerGb = 1024d * 1024d * 1024d;

    // Le % CPU se calcule par différence entre deux mesures : on garde la précédente.
    private long _prevIdle, _prevKernel, _prevUser;
    private bool _hasPrevious;

    private readonly HardwareReader _hardware = new();
    private readonly FpsMeter _fps = new();

    // L'identité du processeur ne change pas : on la lit une seule fois.
    private readonly CpuInfo _cpu = ReadCpuInfo();

    // Les barrettes de RAM ne changent pas pendant que le PC tourne : lecture unique.
    private readonly IReadOnlyList<RamModule> _ramModules = RamModuleReader.Read();

    // Read() tourne en arrière-plan et Dispose() sur le fil de l'interface :
    // ce verrou garantit qu'on ne libère jamais le matériel au milieu d'une lecture.
    private readonly object _sync = new();
    private bool _disposed;

    public SystemSnapshot Read()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return ReadUnsafe();
        }
    }

    private SystemSnapshot ReadUnsafe()
    {
        var (ramUsed, ramTotal) = ReadRam();
        _hardware.Update(); // Une seule mise à jour des capteurs par rafraîchissement.
        WriteDiagnosticOnce();

        return new SystemSnapshot(
            _cpu,
            ReadCpuPercent(),
            _hardware.ReadCpuTemperature(),
            ramUsed,
            ramTotal,
            _ramModules,
            _hardware.ReadGpus(),
            ReadDisks(),
            _fps.Read());
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            _hardware.Dispose();
            _fps.Dispose();
        }
    }

    private bool _diagnosticWritten;

    /// <summary>
    /// En mode Debug uniquement : écrit la liste des capteurs dans "diagnostic-capteurs.txt",
    /// à côté de l'exe, au premier rafraîchissement. Rien n'est écrit dans la version publiée.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    private void WriteDiagnosticOnce()
    {
        if (_diagnosticWritten)
            return;
        _diagnosticWritten = true;

        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "diagnostic-capteurs.txt");
            File.WriteAllText(path, _hardware.BuildDiagnosticReport());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Diagnostic facultatif : on ignore les erreurs d'écriture.
        }
    }

    private double ReadCpuPercent()
    {
        if (!GetSystemTimes(out long idle, out long kernel, out long user))
            return 0;

        if (!_hasPrevious)
        {
            (_prevIdle, _prevKernel, _prevUser, _hasPrevious) = (idle, kernel, user, true);
            return 0; // Première lecture : pas encore de point de comparaison.
        }

        long idleDelta = idle - _prevIdle;
        // Attention : le temps "kernel" inclut déjà le temps "idle".
        long totalDelta = (kernel - _prevKernel) + (user - _prevUser);
        (_prevIdle, _prevKernel, _prevUser) = (idle, kernel, user);

        if (totalDelta <= 0)
            return 0;

        return Math.Clamp((1.0 - (double)idleDelta / totalDelta) * 100.0, 0, 100);
    }

    /// <summary>
    /// Nom commercial et fabricant du processeur, lus dans le registre (là où Windows les stocke au démarrage).
    /// Lecture seule dans HKEY_LOCAL_MACHINE : pas besoin de droits administrateur.
    /// </summary>
    private static CpuInfo ReadCpuInfo()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");

            string name = key?.GetValue("ProcessorNameString") is string rawName ? ShortenCpuName(rawName) : "";

            // Identifiant fabricant inscrit dans le processeur lui-même (instruction CPUID).
            CpuVendor vendor = key?.GetValue("VendorIdentifier") switch
            {
                "GenuineIntel" => CpuVendor.Intel,
                "AuthenticAMD" => CpuVendor.Amd,
                _ => CpuVendor.Unknown,
            };

            return new CpuInfo(name, vendor);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return new CpuInfo("", CpuVendor.Unknown);
        }
    }

    /// <summary>"13th Gen Intel(R) Core(TM) i7-13620H" devient "Core i7-13620H".</summary>
    private static string ShortenCpuName(string name)
    {
        string[] noise =
        [
            "(R)", "(TM)", "(tm)", "Intel ", "AMD ", " Processor", " with Radeon Graphics",
            "13th Gen ", "12th Gen ", "14th Gen ", "11th Gen ",
        ];
        foreach (string word in noise)
            name = name.Replace(word, " ");

        // Coupe ce qui suit le modèle : fréquence ("CPU @ 2.40GHz") ou graphique intégré ("w/ Radeon...").
        foreach (string marker in new[] { " CPU", " w/" })
        {
            int at = name.IndexOf(marker, StringComparison.Ordinal);
            if (at > 0)
                name = name[..at];
        }

        return string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static (double UsedGb, double TotalGb) ReadRam()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
            return (0, 0);

        double total = status.TotalPhys / BytesPerGb;
        double used = (status.TotalPhys - status.AvailPhys) / BytesPerGb;
        return (used, total);
    }

    private static IReadOnlyList<DiskSnapshot> ReadDisks()
    {
        var disks = new List<DiskSnapshot>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            // Seulement les disques internes : pas de lecteur réseau ni de clé USB pour l'instant.
            if (drive.DriveType != DriveType.Fixed)
                continue;

            try
            {
                if (!drive.IsReady)
                    continue;

                string label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Disque local" : drive.VolumeLabel;
                disks.Add(new DiskSnapshot(
                    drive.RootDirectory.FullName,
                    label,
                    drive.TotalSize / BytesPerGb,
                    drive.AvailableFreeSpace / BytesPerGb));
            }
            catch (IOException) { /* Disque retiré pendant la lecture : on l'ignore. */ }
            catch (UnauthorizedAccessException) { /* Volume protégé : on l'ignore. */ }
        }

        return disks;
    }

    // ---- Appels à l'API Windows (P/Invoke) ----

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
