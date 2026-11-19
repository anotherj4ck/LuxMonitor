using System.IO;
using System.Runtime.InteropServices;
using LuxMonitor.Models;

namespace LuxMonitor.Sensors;

/// <summary>
/// Lit les vraies mesures de Windows, sans pilote et sans droits administrateur.
/// CPU et RAM passent par deux fonctions de kernel32.dll (API Windows native),
/// les disques par DriveInfo (.NET), les GPU par LibreHardwareMonitor (voir GpuReader).
/// </summary>
public sealed class WindowsSensorProvider : ISensorProvider
{
    private const double BytesPerGb = 1024d * 1024d * 1024d;

    // Le % CPU se calcule par différence entre deux mesures : on garde la précédente.
    private long _prevIdle, _prevKernel, _prevUser;
    private bool _hasPrevious;

    private readonly GpuReader _gpuReader = new();

    public SystemSnapshot Read()
    {
        var (ramUsed, ramTotal) = ReadRam();
        return new SystemSnapshot(ReadCpuPercent(), ramUsed, ramTotal, _gpuReader.Read(), ReadDisks());
    }

    public void Dispose() => _gpuReader.Dispose();

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
