using System.Collections.ObjectModel;
using LuxMonitor.Models;
using LuxMonitor.Sensors;
using LuxMonitor.Services;

namespace LuxMonitor.ViewModels;

/// <summary>
/// Fait le lien entre les capteurs et la fenêtre.
/// Il lit une photo de la machine et la transforme en valeurs prêtes à afficher.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly ISensorProvider _provider;

    private string _cpuName = string.Empty;
    private double _cpuPercent;
    private string _cpuText = "—";
    private string _cpuTemperatureText = string.Empty;
    private double _ramPercent;
    private string _ramText = "—";
    private string _ramModulesText = string.Empty;
    private bool _isThemePanelOpen;
    private bool _isCompact;

    public MainViewModel(ISensorProvider provider, bool isDemo)
    {
        _provider = provider;
        IsDemo = isDemo;
    }

    public bool IsDemo { get; }

    /// <summary>Pastilles du panneau des thèmes.</summary>
    public IReadOnlyList<ThemeOptionViewModel> ThemeOptions { get; } =
        ThemeManager.Themes.Select(theme => new ThemeOptionViewModel(theme)).ToList();

    public bool IsThemePanelOpen
    {
        get => _isThemePanelOpen;
        set => SetProperty(ref _isThemePanelOpen, value);
    }

    /// <summary>Détail des barrettes : "16 Go Samsung · 8 Go SK hynix" (vide si inconnu).</summary>
    public string RamModulesText
    {
        get => _ramModulesText;
        private set
        {
            if (SetProperty(ref _ramModulesText, value))
                OnPropertyChanged(nameof(HasRamModules));
        }
    }

    public bool HasRamModules => RamModulesText.Length > 0;

    /// <summary>Disposition miniature (widget étroit) : une ligne par élément.</summary>
    public bool IsCompact
    {
        get => _isCompact;
        set
        {
            if (SetProperty(ref _isCompact, value))
                OnPropertyChanged(nameof(IsNormalLayout));
        }
    }

    /// <summary>Inverse de IsCompact, pour l'affichage (WPF n'a pas de convertisseur "inverse" intégré).</summary>
    public bool IsNormalLayout => !IsCompact;

    /// <summary>Place la coche sur le thème actif.</summary>
    public void SetActiveTheme(string themeId)
    {
        foreach (ThemeOptionViewModel option in ThemeOptions)
            option.IsActive = option.Theme.Id == themeId;
    }

    public ObservableCollection<GpuViewModel> Gpus { get; } = [];

    public ObservableCollection<DiskViewModel> Disks { get; } = [];

    /// <summary>Fabricant du processeur : sert à choisir quel outil ouvrir au clic.</summary>
    public CpuVendor CpuVendor { get; private set; }

    public string CpuName
    {
        get => _cpuName;
        private set => SetProperty(ref _cpuName, value);
    }

    public double CpuPercent
    {
        get => _cpuPercent;
        private set => SetProperty(ref _cpuPercent, value);
    }

    public string CpuText
    {
        get => _cpuText;
        private set => SetProperty(ref _cpuText, value);
    }

    /// <summary>Température du processeur ("62 °C"), vide si indisponible (pas de pilote ou pas de droits).</summary>
    public string CpuTemperatureText
    {
        get => _cpuTemperatureText;
        private set
        {
            if (SetProperty(ref _cpuTemperatureText, value))
                OnPropertyChanged(nameof(HasCpuTemperature));
        }
    }

    public bool HasCpuTemperature => CpuTemperatureText.Length > 0;

    public double RamPercent
    {
        get => _ramPercent;
        private set => SetProperty(ref _ramPercent, value);
    }

    public string RamText
    {
        get => _ramText;
        private set => SetProperty(ref _ramText, value);
    }

    public void Refresh()
    {
        SystemSnapshot snapshot = _provider.Read();

        CpuName = snapshot.Cpu.Name;
        CpuVendor = snapshot.Cpu.Vendor;
        CpuPercent = snapshot.CpuPercent;
        CpuText = $"{snapshot.CpuPercent:0} %";
        CpuTemperatureText = snapshot.CpuTemperatureC is double temp ? $"{temp:0} °C" : string.Empty;

        RamPercent = snapshot.RamPercent;
        RamText = $"{snapshot.RamUsedGb:0.0} / {snapshot.RamTotalGb:0} Go";
        RamModulesText = string.Join("  ·  ", snapshot.RamModules.Select(m =>
            string.IsNullOrEmpty(m.Manufacturer) ? $"{m.CapacityGb:0} Go" : $"{m.CapacityGb:0} Go {m.Manufacturer}"));

        Sync(Gpus, FilterIdleIntegratedGpus(snapshot.Gpus),
            snap => snap.Id, vm => vm.Id,
            snap => new GpuViewModel(snap.Id, snap.Vendor),
            (vm, snap) => vm.Update(snap));

        Sync(Disks, snapshot.Disks,
            snap => snap.RootPath, vm => vm.RootPath,
            snap => new DiskViewModel(snap.RootPath),
            (vm, snap) => vm.Update(snap));
    }

    // ---- GPU intégré : masqué quand il ne sert pas ----

    // Charge au-dessus de laquelle le GPU intégré est considéré comme "utilisé".
    private const double IntegratedGpuActiveLoad = 5;

    // Délai avant de le masquer à nouveau : évite qu'il clignote à chaque petit pic d'activité.
    private static readonly TimeSpan IntegratedGpuHideDelay = TimeSpan.FromSeconds(10);

    private readonly Dictionary<string, DateTime> _integratedGpuLastActive = [];

    /// <summary>
    /// Sur un portable à deux GPU, le GPU intégré (Intel) est souvent au repos.
    /// On ne l'affiche que s'il a été utilisé dans les 10 dernières secondes.
    /// S'il n'y a pas de carte dédiée, il est le seul GPU : on l'affiche toujours.
    /// Limite connue : les cartes dédiées Intel Arc sont vues comme "Intel" et suivent la même règle.
    /// </summary>
    private IReadOnlyList<GpuSnapshot> FilterIdleIntegratedGpus(IReadOnlyList<GpuSnapshot> gpus)
    {
        bool hasDedicatedGpu = gpus.Any(g => g.Vendor != GpuVendor.Intel);
        if (!hasDedicatedGpu)
            return gpus;

        DateTime now = DateTime.UtcNow;

        return gpus.Where(gpu =>
        {
            if (gpu.Vendor != GpuVendor.Intel)
                return true;

            if (gpu.LoadPercent >= IntegratedGpuActiveLoad)
                _integratedGpuLastActive[gpu.Id] = now;

            return _integratedGpuLastActive.TryGetValue(gpu.Id, out DateTime lastActive)
                   && now - lastActive < IntegratedGpuHideDelay;
        }).ToList();
    }

    /// <summary>
    /// Met à jour une liste affichée sans la reconstruire (recréer les tuiles
    /// chaque seconde ferait clignoter l'interface). Générique : sert pour les GPU
    /// comme pour les disques, et servira pour la suite (réseau, ventilateurs...).
    /// </summary>
    private static void Sync<TViewModel, TSnapshot>(
        ObservableCollection<TViewModel> items,
        IReadOnlyList<TSnapshot> snapshots,
        Func<TSnapshot, string> snapshotKey,
        Func<TViewModel, string> viewModelKey,
        Func<TSnapshot, TViewModel> create,
        Action<TViewModel, TSnapshot> update)
        where TViewModel : class
    {
        // 1. Retirer ce qui a disparu (clé USB débranchée, GPU désactivé...).
        var currentKeys = snapshots.Select(snapshotKey).ToHashSet();
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (!currentKeys.Contains(viewModelKey(items[i])))
                items.RemoveAt(i);
        }

        // 2. Mettre à jour l'existant, ajouter le nouveau.
        foreach (TSnapshot snapshot in snapshots)
        {
            string key = snapshotKey(snapshot);
            TViewModel? viewModel = items.FirstOrDefault(vm => viewModelKey(vm) == key);
            if (viewModel is null)
            {
                viewModel = create(snapshot);
                items.Add(viewModel);
            }

            update(viewModel, snapshot);
        }
    }
}
