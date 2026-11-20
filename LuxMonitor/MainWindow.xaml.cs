using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using LuxMonitor.Localization;
using LuxMonitor.Models;
using LuxMonitor.Sensors;
using LuxMonitor.Services;
using LuxMonitor.ViewModels;

namespace LuxMonitor;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 24;

    // Largeurs possibles du widget, et seuils de changement de disposition :
    // étroit = miniature, moyen = normal, large = bandeau horizontal.
    private const double MinWidgetWidth = 150;
    private const double CompactThreshold = 230;
    private const double StripThreshold = 560;
    private const double MaxWidgetWidth = 1800;

    private readonly ISensorProvider _provider;
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _timer;
    private readonly AppSettings _settings = SettingsService.Load();
    private readonly TrayIconService _tray;

    // Mode jeu : créé à la première utilisation.
    private OverlayWindow? _overlay;
    private HotkeyService? _hotkey;
    private bool _widgetWasVisibleBeforeOverlay;
    private WelcomeWindow? _welcome;

    // Choix du thème : fenêtre séparée, ouverte à côté du widget.
    private ThemePickerWindow? _themePicker;
    private DateTime _themePickerClosedAt;

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

        ApplyWidth(_settings.WindowWidth ?? Width);
        RestorePosition();

        // Rafraîchissement toutes les secondes, uniquement quand la fenêtre est visible :
        // cachée, Lux Monitor n'interroge plus le matériel (économie de batterie, GPU qui peut dormir).
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await _viewModel.RefreshAsync();
        IsVisibleChanged += (_, _) => UpdateRefreshState();

        ApplyWindowLock(_settings.IsWindowLocked);

        _tray = new TrayIconService(ToggleWidget, _settings.IsWindowLocked, SetWindowLocked, Loc.FromCode(_settings.Language), SetLanguage, ToggleOverlay, MoveOverlay, SetOverlayCorner, _settings.OverlayCorner,
            ShowWelcome, _settings.AdminStartupEnabled, ExitApplication);

        // Premier lancement : on présente la température et les FPS (une seule fois).
        if (!_settings.HasShownWelcome)
            Loaded += (_, _) => ShowWelcome();
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

    private void HideWidget() => HideWidget(showHint: true);

    private void HideWidget(bool showHint)
    {
        _themePicker?.Close();
        Hide();
        SavePosition();

        // La première fois seulement, on explique où est passée la fenêtre.
        if (showHint && !_settings.HasShownTrayHint)
        {
            _tray.ShowHint(Loc.T("Hint.TrayTitle"), Loc.T("Hint.TrayText"));
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

    // ---- Accueil (température et FPS) ----

    private void ShowWelcome()
    {
        if (_welcome is not null)
        {
            _welcome.Activate();
            return;
        }

        _welcome = new WelcomeWindow(() => _settings.AdminStartupEnabled, ExitApplication);
        _welcome.Closed += (_, _) =>
        {
            _welcome = null;
            _settings.HasShownWelcome = true;
            SettingsService.Save(_settings);
        };
        _welcome.Show();
    }

    // ---- Mode jeu ----

    /// <summary>
    /// Bascule entre le widget et le mode jeu. En sortant du mode jeu, on retrouve
    /// le widget seulement s'il était affiché avant (sinon on reste discret dans la zone de notification).
    /// </summary>
    private void ToggleOverlay()
    {
        _overlay ??= CreateOverlay();

        if (_overlay.IsVisible)
        {
            _overlay.Hide();
            if (_widgetWasVisibleBeforeOverlay)
                ShowWidget();
        }
        else
        {
            _widgetWasVisibleBeforeOverlay = IsVisible;
            if (IsVisible)
                HideWidget(showHint: false);

            _overlay.Show();
            ShowOverlayHintOnce();
        }

        _tray.SetOverlayActive(_overlay.IsVisible);
        UpdateRefreshState();
    }

    private OverlayWindow CreateOverlay()
    {
        Point? customPosition = _settings.OverlayLeft is double x && _settings.OverlayTop is double y
            ? new Point(x, y)
            : null;

        var overlay = new OverlayWindow(_viewModel, _settings.OverlayCorner, customPosition, SaveOverlayPosition, ToggleOverlay);
        overlay.IsVisibleChanged += (_, _) => UpdateRefreshState();
        return overlay;
    }

    private void SaveOverlayPosition(double left, double top)
    {
        _settings.OverlayLeft = left;
        _settings.OverlayTop = top;
        SettingsService.Save(_settings);
    }

    /// <summary>Active le mode jeu si besoin, puis permet de déplacer la barre à la souris.</summary>
    private void MoveOverlay()
    {
        if (_overlay?.IsVisible != true)
            ToggleOverlay();

        _overlay!.BeginMove();
    }

    private void SetOverlayCorner(OverlayCorner corner)
    {
        // Choisir un coin remplace la position libre.
        _settings.OverlayCorner = corner;
        _settings.OverlayLeft = null;
        _settings.OverlayTop = null;
        SettingsService.Save(_settings);
        _overlay?.SetCorner(corner);
    }

    /// <summary>Le mode jeu n'est pas cliquable : la première fois, on explique comment en sortir.</summary>
    private void ShowOverlayHintOnce()
    {
        if (_settings.HasShownOverlayHint)
            return;

        string howToExit = Loc.T(_hotkey?.IsRegistered == true ? "Hint.OverlayExitShortcut" : "Hint.OverlayExitMenu");
        _tray.ShowHint(Loc.T("Hint.OverlayTitle"), howToExit);

        _settings.HasShownOverlayHint = true;
        SettingsService.Save(_settings);
    }

    private void OverlayButton_Click(object sender, RoutedEventArgs e) => ToggleOverlay();

    private void UpdateRefreshState()
    {
        // Les mesures tournent tant que le widget OU le mode jeu est affiché.
        if (IsVisible || _overlay?.IsVisible == true)
        {
            _ = _viewModel.RefreshAsync(); // Valeurs à jour tout de suite, sans attendre une seconde.
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
        _hotkey?.Dispose();  // Libère le raccourci clavier pour les autres applications.
        _overlay?.Close();
        _welcome?.Close();
        _themePicker?.Close();
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

    // ---- Langue ----

    /// <summary>Choix dans le menu de l'icône : null = suivre la langue de Windows.</summary>
    private void SetLanguage(AppLanguage? language)
    {
        _settings.Language = Loc.ToCode(language);
        SettingsService.Save(_settings);
        Loc.Instance.Apply(language);
    }

    // ---- Fenêtre figée ----

    private void SetWindowLocked(bool isLocked)
    {
        ApplyWindowLock(isLocked);
        _settings.IsWindowLocked = isLocked;
        SettingsService.Save(_settings);
    }

    /// <summary>Figée : on cache la poignée de redimensionnement ; le déplacement est bloqué au clic.</summary>
    private void ApplyWindowLock(bool isLocked)
        => ResizeGrip.Visibility = isLocked ? Visibility.Collapsed : Visibility.Visible;

    // ---- Taille et disposition ----

    /// <summary>
    /// Applique une largeur (bornée) et choisit la disposition qui va avec.
    /// La hauteur, elle, suit le contenu (SizeToContent="Height") : en bandeau,
    /// le widget devient donc tout naturellement un rectangle horizontal.
    /// </summary>
    private void ApplyWidth(double width)
    {
        // Jamais plus large que l'ensemble des écrans.
        double maxWidth = Math.Max(MinWidgetWidth, Math.Min(MaxWidgetWidth, SystemParameters.VirtualScreenWidth));
        Width = Math.Clamp(width, MinWidgetWidth, maxWidth);

        _viewModel.Layout = Width < CompactThreshold ? WidgetLayout.Compact
            : Width >= StripThreshold ? WidgetLayout.Strip
            : WidgetLayout.Normal;
    }

    // On gère le redimensionnement nous-mêmes (poignée en bas à droite) plutôt que de laisser
    // Windows le faire : sinon WPF désactive SizeToContent et la hauteur ne suit plus le contenu.
    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
        => ApplyWidth(Width + e.HorizontalChange);

    private void ResizeGrip_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _settings.WindowWidth = Width;
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

        // Le raccourci global a besoin de la fenêtre Windows réelle : on l'enregistre ici.
        _hotkey = new HotkeyService(this, ToggleOverlay);
    }

    private void ApplyTheme(ThemeDefinition theme)
    {
        ThemeManager.Apply(this, theme);
        _viewModel.SetActiveTheme(theme.Id);
    }

    /// <summary>Le bouton palette ouvre ou ferme la fenêtre de choix du thème.</summary>
    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_themePicker is not null)
        {
            _themePicker.Close();
            return;
        }

        // Si la fenêtre était ouverte, cliquer sur ce bouton lui a fait perdre le focus : elle vient
        // de se fermer toute seule. Sans ce garde-fou, le même clic la rouvrirait aussitôt.
        if (DateTime.UtcNow - _themePickerClosedAt < TimeSpan.FromMilliseconds(300))
            return;

        _themePicker = new ThemePickerWindow(this, _viewModel, SelectTheme);
        _themePicker.Closed += (_, _) =>
        {
            _themePicker = null;
            _themePickerClosedAt = DateTime.UtcNow;
        };
        _themePicker.Show();
    }

    private void SelectTheme(ThemeDefinition theme)
    {
        ApplyTheme(theme);
        _settings.ThemeId = theme.Id;
        SettingsService.Save(_settings);
    }

    // ---- Interactions ----

    // La fenêtre n'a pas de barre de titre : on la déplace en cliquant n'importe où.
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || _settings.IsWindowLocked)
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

    // Bandeau : une seule fonction pour toutes les cases, selon leur type.
    private void StripItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: StripItemViewModel item })
            return;

        switch (item.Kind)
        {
            case StripItemKind.Cpu:
                Launcher.OpenCpuTool(_viewModel.CpuVendor);
                break;
            case StripItemKind.Gpu:
                Launcher.OpenGpuTool(item.GpuVendor);
                break;
            case StripItemKind.Ram:
                Launcher.OpenTaskManager();
                break;
            case StripItemKind.Disk:
                Launcher.OpenFolder(item.RootPath);
                break;
        }
    }
}
