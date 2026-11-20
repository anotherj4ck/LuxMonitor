using LuxMonitor.Models;

namespace LuxMonitor.ViewModels;

/// <summary>Type d'élément affiché dans la disposition bandeau (sert à savoir quoi ouvrir au clic).</summary>
public enum StripItemKind
{
    Fps,
    Cpu,
    Gpu,
    Ram,
    Disk,
}

/// <summary>
/// Une case de la disposition « bandeau ». Contrairement aux autres dispositions,
/// le bandeau affiche TOUS les éléments (FPS, CPU, GPU, RAM, disques) dans une seule liste :
/// c'est ce qui permet de les répartir en cases de largeur égale sur une ligne.
/// </summary>
public sealed class StripItemViewModel : ObservableObject
{
    private string _label = string.Empty;
    private string _value = string.Empty;
    private string _secondary = string.Empty;
    private string _toolTip = string.Empty;
    private double _percent;

    public StripItemViewModel(string key, StripItemKind kind, GpuVendor gpuVendor = default, string rootPath = "")
    {
        Key = key;
        Kind = kind;
        GpuVendor = gpuVendor;
        RootPath = rootPath;
    }

    /// <summary>Identifiant stable ("cpu", "gpu:…", "disk:C:\") : permet de mettre à jour sans recréer.</summary>
    public string Key { get; }

    public StripItemKind Kind { get; }

    /// <summary>Pour un GPU : quel logiciel ouvrir au clic.</summary>
    public GpuVendor GpuVendor { get; }

    /// <summary>Pour un disque : quel dossier ouvrir au clic.</summary>
    public string RootPath { get; }

    /// <summary>Les FPS ne s'ouvrent pas au clic et n'ont pas de jauge.</summary>
    public bool IsClickable => Kind != StripItemKind.Fps;

    public bool HasGauge => Kind != StripItemKind.Fps;

    public string Label
    {
        get => _label;
        private set => SetProperty(ref _label, value);
    }

    public string Value
    {
        get => _value;
        private set => SetProperty(ref _value, value);
    }

    /// <summary>Information secondaire, en plus discret (température).</summary>
    public string Secondary
    {
        get => _secondary;
        private set => SetProperty(ref _secondary, value);
    }

    public string ToolTip
    {
        get => _toolTip;
        private set => SetProperty(ref _toolTip, value);
    }

    public double Percent
    {
        get => _percent;
        private set => SetProperty(ref _percent, value);
    }

    public void Update(string label, string value, string secondary, double percent, string toolTip)
    {
        Label = label;
        Value = value;
        Secondary = secondary;
        Percent = percent;
        ToolTip = toolTip;
    }
}
