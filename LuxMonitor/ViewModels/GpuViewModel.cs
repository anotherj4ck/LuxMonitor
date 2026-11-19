using System.Globalization;
using LuxMonitor.Models;

namespace LuxMonitor.ViewModels;

/// <summary>Ce que l'interface affiche pour une carte graphique.</summary>
public sealed class GpuViewModel : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private string _name = string.Empty;
    private string _loadText = "—";
    private double _loadPercent;
    private string _detail = string.Empty;
    private string _temperatureText = string.Empty;

    public GpuViewModel(string id, GpuVendor vendor)
    {
        Id = id;
        Vendor = vendor;
    }

    public string Id { get; }

    /// <summary>Constructeur : sert à choisir quel outil ouvrir au clic.</summary>
    public GpuVendor Vendor { get; }

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public string LoadText
    {
        get => _loadText;
        private set => SetProperty(ref _loadText, value);
    }

    public double LoadPercent
    {
        get => _loadPercent;
        private set => SetProperty(ref _loadPercent, value);
    }

    public string Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    /// <summary>Température seule, pour la disposition miniature (vide si inconnue).</summary>
    public string TemperatureText
    {
        get => _temperatureText;
        private set => SetProperty(ref _temperatureText, value);
    }

    public void Update(GpuSnapshot snapshot)
    {
        Name = ShortenName(snapshot.Name);
        LoadPercent = snapshot.LoadPercent ?? 0;
        LoadText = snapshot.LoadPercent is double load ? $"{load:0} %" : "—";

        TemperatureText = snapshot.TemperatureC is double t ? $"{t:0} °C  " : string.Empty;

        // On n'affiche que ce qui est connu : pas de "0 °C" inventé.
        var parts = new List<string>();
        if (snapshot.TemperatureC is double temp)
            parts.Add($"{temp:0} °C");
        if (snapshot.MemoryUsedGb is double used && snapshot.MemoryTotalGb is double total && total > 0)
            parts.Add(string.Format(French, "{0:0.0} / {1:0} Go VRAM", used, total));

        Detail = parts.Count > 0 ? string.Join("  ·  ", parts) : "Capteurs non disponibles";
    }

    /// <summary>"NVIDIA GeForce RTX 4060 Laptop GPU" devient "RTX 4060 Laptop" : la tuile reste lisible.</summary>
    private static string ShortenName(string name)
        => name.Replace("NVIDIA GeForce ", "")
               .Replace("NVIDIA ", "")
               .Replace("AMD Radeon ", "Radeon ")
               .Replace(" GPU", "")
               .Trim();
}
