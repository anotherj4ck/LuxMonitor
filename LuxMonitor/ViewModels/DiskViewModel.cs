using LuxMonitor.Localization;
using LuxMonitor.Models;

namespace LuxMonitor.ViewModels;

/// <summary>Ce que l'interface affiche pour un disque.</summary>
public sealed class DiskViewModel : ObservableObject
{
    private string _label = string.Empty;
    private string _detail = string.Empty;
    private double _usedPercent;

    public DiskViewModel(string rootPath) => RootPath = rootPath;

    /// <summary>Chemin réel du disque (ex. "C:\"), utilisé pour l'ouvrir dans l'Explorateur.</summary>
    public string RootPath { get; }

    /// <summary>Nom court affiché (ex. "C:").</summary>
    public string Title => RootPath.TrimEnd('\\');

    public string Label
    {
        get => _label;
        private set => SetProperty(ref _label, value);
    }

    public string Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    public double UsedPercent
    {
        get => _usedPercent;
        private set => SetProperty(ref _usedPercent, value);
    }

    public void Update(DiskSnapshot snapshot)
    {
        Label = snapshot.Label;
        UsedPercent = snapshot.UsedPercent;
        Detail = Loc.F("Fmt.DiskFree", snapshot.FreeGb, snapshot.TotalGb);
    }
}
