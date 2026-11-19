using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace LuxMonitor.Services;

/// <summary>
/// Icône dans la zone de notification (à côté de l'horloge), avec son menu.
/// WPF n'a pas de composant pour ça : on emprunte le NotifyIcon de Windows Forms,
/// fourni avec .NET. Pas de bibliothèque externe à ajouter.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIconService(Action toggleWindow, Action exitApplication)
    {
        var startupItem = new Forms.ToolStripMenuItem("Lancer au démarrage de Windows")
        {
            CheckOnClick = true,
            Checked = StartupService.IsEnabled(),
        };
        startupItem.CheckedChanged += (_, _) =>
        {
            if (!StartupService.SetEnabled(startupItem.Checked))
                startupItem.Checked = StartupService.IsEnabled(); // Échec : on réaffiche l'état réel.
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Afficher / masquer", null, (_, _) => toggleWindow());
        menu.Items.Add(startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quitter Lux Monitor", null, (_, _) => exitApplication());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Lux Monitor",
            ContextMenuStrip = menu,
            Visible = true,
        };

        // Clic gauche : afficher / masquer. Le clic droit ouvre le menu tout seul.
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                toggleWindow();
        };
    }

    /// <summary>Petite bulle d'information affichée par Windows près de l'icône.</summary>
    public void ShowHint(string title, string message)
        => _notifyIcon.ShowBalloonTip(4000, title, message, Forms.ToolTipIcon.None);

    /// <summary>L'icône est intégrée dans l'exe (ressource WPF), on la relit depuis là.</summary>
    private static Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/LuxMonitor.ico"));
        return resource is null ? SystemIcons.Application : new Icon(resource.Stream);
    }

    public void Dispose()
    {
        // Sans ça, l'icône reste affichée après la fermeture jusqu'à ce que la souris passe dessus.
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
