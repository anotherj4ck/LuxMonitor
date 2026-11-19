using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
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
        PawnIoStatus.Text = pawnIoInstalled ? "· installé ✓" : "· non installé";
        PawnIoButton.Visibility = pawnIoInstalled ? Visibility.Collapsed : Visibility.Visible;

        bool adminEnabled = _isAdminStartupEnabled();
        AdminStatus.Text = adminEnabled ? "· activés ✓" : "· non activés";
        AdminButton.Content = adminEnabled ? "Désactiver" : "Activer";
    }

    private void PawnIo_Click(object sender, RoutedEventArgs e)
    {
        // Adresse officielle écrite en dur : on n'ouvre jamais une adresse venue d'ailleurs.
        Process.Start(new ProcessStartInfo(SystemInfo.PawnIoWebsite) { UseShellExecute = true });
        ShowResult("Installe PawnIO depuis le site, puis reviens ici : cette fenêtre se mettra à jour toute seule.");
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
            ShowResult("Activation annulée : l'écran de confirmation administrateur a été refusé. Rien n'a été modifié.");
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
