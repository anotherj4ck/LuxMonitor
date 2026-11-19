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
    private double _ramPercent;
    private string _ramText = "—";
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

        RamPercent = snapshot.RamPercent;
        RamText = $"{snapshot.RamUsedGb:0.0} / {snapshot.RamTotalGb:0} Go";

        Sync(Gpus, snapshot.Gpus,
            snap => snap.Id, vm => vm.Id,
            snap => new GpuViewModel(snap.Id, snap.Vendor),
            (vm, snap) => vm.Update(snap));

        Sync(Disks, snapshot.Disks,
            snap => snap.RootPath, vm => vm.RootPath,
            snap => new DiskViewModel(snap.RootPath),
            (vm, snap) => vm.Update(snap));
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
