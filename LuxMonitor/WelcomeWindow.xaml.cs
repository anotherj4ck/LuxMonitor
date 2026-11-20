using System.Windows;
using System.Windows.Input;
using LuxMonitor.Localization;
using LuxMonitor.Services;

namespace LuxMonitor;

/// <summary>
/// Fenêtre d'accueil : explique et propose d'activer la température du processeur et les FPS.
/// L'état (PawnIO installé ? démarrage admin actif ?) est revérifié chaque fois que la fenêtre
/// reprend le focus : par exemple au retour du navigateur, après avoir installé PawnIO.
/// </summary>
public partial class WelcomeWindow : Window
{
    private readonly Func<bool> _isAdminStartupEnabled;
    private readonly Action _exitApplication;

    /// <param name="isAdminStartupEnabled">Lit l'état actuel du démarrage administrateur.</param>
    /// <param name="exitApplication">Ferme Lux Monitor (l'instance administrateur prend le relais).</param>
    public WelcomeWindow(Func<bool> isAdminStartupEnabled, Action exitApplication)
    {
        InitializeComponent();
        _isAdminStartupEnabled = isAdminStartupEnabled;
        _exitApplication = exitApplication;

        Activated += (_, _) => RefreshState();
        Loc.Instance.LanguageChanged += RefreshState;
        Closed += (_, _) => Loc.Instance.LanguageChanged -= RefreshState;
        RefreshState();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Même apparence que le widget (Acrylic ou fond opaque selon le thème actif).
        WindowEffects.Prepare(this);
        ThemeManager.ApplyBackdrop(this);
    }

    private void RefreshState()
    {
        bool pawnIoInstalled = SystemInfo.IsPawnIoInstalled();
        PawnIoStatus.Text = Loc.T(pawnIoInstalled ? "Welcome.Installed" : "Welcome.NotInstalled");
        PawnIoButton.Visibility = pawnIoInstalled ? Visibility.Collapsed : Visibility.Visible;

        bool adminEnabled = _isAdminStartupEnabled();
        AdminStatus.Text = Loc.T(adminEnabled ? "Welcome.Enabled" : "Welcome.NotEnabled");
        AdminButton.Content = Loc.T(adminEnabled ? "Welcome.Disable" : "Welcome.Enable");
    }

    private void PawnIo_Click(object sender, RoutedEventArgs e)
    {
        // Adresse officielle écrite en dur : on n'ouvre jamais une adresse venue d'ailleurs.
        Launcher.OpenUrl(SystemInfo.PawnIoWebsite);
        ShowResult(Loc.T("Welcome.PawnIoHint"));
    }

    private void Admin_Click(object sender, RoutedEventArgs e)
    {
        bool enable = !_isAdminStartupEnabled();
        string argument = enable ? AdminStartupService.EnableArgument : AdminStartupService.DisableArgument;

        // Lux Monitor se relance en administrateur (écran UAC) pour créer ou supprimer la tâche planifiée.
        // Si l'utilisateur accepte, la nouvelle instance prend le relais et celle-ci se ferme.
        if (AdminStartupService.RestartElevated(argument))
        {
            Close();
            _exitApplication();
        }
        else
        {
            ShowResult(Loc.T("Welcome.AdminCancelled"));
        }
    }

    private void ShowResult(string message)
    {
        ResultMessage.Text = message;
        ResultMessage.Visibility = Visibility.Visible;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
