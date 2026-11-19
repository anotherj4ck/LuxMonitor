using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LuxMonitor.Models;
using LuxMonitor.Sensors;
using LuxMonitor.Services;
using LuxMonitor.ViewModels;

namespace LuxMonitor;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 24;

    private readonly ISensorProvider _provider;
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _timer;
    private readonly AppSettings _settings = SettingsService.Load();
    private readonly TrayIconService _tray;

    // Vrai seulement quand on quitte pour de bon (menu "Quitter").
    // Sinon, fermer la fenêtre la cache simplement dans la zone de notification.
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();

        // "--demo" sur la ligne de commande = fausses mesures (utile dans une VM).
        bool isDemo = Environment.GetCommandLineArgs()
            .Contains("--demo", StringComparer.OrdinalIgnoreCase);

        _provider = isDemo ? new DemoSensorProvider() : new WindowsSensorProvider();
        _viewModel = new MainViewModel(_provider, isDemo);
        DataContext = _viewModel;

        RestorePosition();

        // Rafraîchissement toutes les secondes, uniquement quand la fenêtre est visible :
        // cachée, Lux Monitor n'interroge plus le matériel (économie de batterie, GPU qui peut dormir).
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _viewModel.Refresh();
        IsVisibleChanged += (_, _) => UpdateRefreshState();

        _tray = new TrayIconService(ToggleWidget, ExitApplication);
        StartupService.RefreshPathIfEnabled();

        Closing += OnClosing;
        Closed += OnClosed;
    }

    // ---- Afficher, masquer, quitter ----

    /// <summary>Affiche la fenêtre et la met au premier plan (appelé aussi par une 2e instance).</summary>
    public void ShowWidget()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void HideWidget()
    {
        _viewModel.IsThemePanelOpen = false;
        Hide();
        SavePosition();

        // La première fois seulement, on explique où est passée la fenêtre.
        if (!_settings.HasShownTrayHint)
        {
            _tray.ShowHint("Lux Monitor continue de tourner",
                "Il est dans la zone de notification. Clic gauche pour le rouvrir, clic droit pour le menu.");
            _settings.HasShownTrayHint = true;
            SettingsService.Save(_settings);
        }
    }

    private void ToggleWidget()
    {
        if (IsVisible)
            HideWidget();
        else
            ShowWidget();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    private void UpdateRefreshState()
    {
        if (IsVisible)
        {
            _viewModel.Refresh(); // Valeurs à jour tout de suite, sans attendre une seconde.
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Alt+F4 ou bouton ✕ : on cache au lieu de quitter.
        if (!_isExiting)
        {
            e.Cancel = true;
            HideWidget();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SavePosition();
        _timer.Stop();
        _provider.Dispose(); // Libère l'accès au matériel.
        _tray.Dispose();     // Retire l'icône de la zone de notification.
    }

    // ---- Position de la fenêtre ----

    private void RestorePosition()
    {
        Rect screen = new(
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

        // On ne réutilise la position enregistrée que si elle est encore visible
        // (un écran secondaire a pu être débranché depuis).
        if (_settings.WindowLeft is double left && _settings.WindowTop is double top
            && left >= screen.Left && left <= screen.Right - 100
            && top >= screen.Top && top <= screen.Bottom - 100)
        {
            Left = left;
            Top = top;
            return;
        }

        // Par défaut : en haut à droite de l'écran principal, comme un widget.
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - ScreenMargin;
        Top = workArea.Top + ScreenMargin;
    }

    private void SavePosition()
    {
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        SettingsService.Save(_settings);
    }

    // ---- Thèmes ----

    // Appelé quand Windows a créé la fenêtre, juste avant le premier affichage :
    // on applique le thème enregistré ici pour éviter un flash de l'ancien thème.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        WindowEffects.Prepare(this);
        ApplyTheme(ThemeManager.FindOrDefault(_settings.ThemeId));
    }

    private void ApplyTheme(ThemeDefinition theme)
    {
        ThemeManager.Apply(this, theme);
        _viewModel.SetActiveTheme(theme.Id);
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
        => _viewModel.IsThemePanelOpen = !_viewModel.IsThemePanelOpen;

    private void ThemeOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ThemeOptionViewModel option })
            return;

        ApplyTheme(option.Theme);
        _viewModel.IsThemePanelOpen = false;

        _settings.ThemeId = option.Theme.Id;
        SettingsService.Save(_settings);
    }

    // ---- Interactions ----

    // La fenêtre n'a pas de barre de titre : on la déplace en cliquant n'importe où.
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
            return;

        DragMove();       // Rend la main quand on relâche le bouton de la souris...
        SavePosition();   // ...on enregistre donc la nouvelle position juste après.
    }

    private void Close_Click(object sender, RoutedEventArgs e) => HideWidget();

    private void Cpu_Click(object sender, RoutedEventArgs e) => Launcher.OpenCpuTool(_viewModel.CpuVendor);

    private void Gpu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GpuViewModel gpu })
            Launcher.OpenGpuTool(gpu.Vendor);
    }

    private void Ram_Click(object sender, RoutedEventArgs e) => Launcher.OpenTaskManager();

    private void Disk_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DiskViewModel disk })
            Launcher.OpenFolder(disk.RootPath);
    }
}
