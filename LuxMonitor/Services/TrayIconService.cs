using System.Drawing;
using System.Windows;
using LuxMonitor.Models;
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
    private readonly Forms.ToolStripMenuItem _overlayItem;
    private readonly Dictionary<OverlayCorner, Forms.ToolStripMenuItem> _cornerItems = [];

    public TrayIconService(
        Action toggleWindow,
        Action toggleOverlay,
        Action moveOverlay,
        Action<OverlayCorner> setOverlayCorner,
        OverlayCorner currentCorner,
        Action openWelcome,
        bool adminStartupEnabled,
        Action exitApplication)
    {
        // Mode jeu + choix du coin (sous-menu avec une coche sur le coin actif).
        _overlayItem = new Forms.ToolStripMenuItem("Mode jeu") { ShortcutKeyDisplayString = "Ctrl+Maj+L" };
        _overlayItem.Click += (_, _) => toggleOverlay();

        var cornerMenu = new Forms.ToolStripMenuItem("Position du mode jeu");

        var moveItem = new Forms.ToolStripMenuItem("Déplacer à la souris…");
        moveItem.Click += (_, _) =>
        {
            moveOverlay();
            foreach (var cornerItem in _cornerItems.Values)
                cornerItem.Checked = false; // Position libre : plus aucun coin coché.
        };
        cornerMenu.DropDownItems.Add(moveItem);
        cornerMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
        (OverlayCorner Corner, string Label)[] corners =
        [
            (OverlayCorner.TopLeft, "En haut à gauche"),
            (OverlayCorner.TopRight, "En haut à droite"),
            (OverlayCorner.BottomLeft, "En bas à gauche"),
            (OverlayCorner.BottomRight, "En bas à droite"),
        ];
        foreach (var (corner, label) in corners)
        {
            var item = new Forms.ToolStripMenuItem(label) { Checked = corner == currentCorner };
            item.Click += (_, _) =>
            {
                setOverlayCorner(corner);
                foreach (var (otherCorner, otherItem) in _cornerItems)
                    otherItem.Checked = otherCorner == corner;
            };
            _cornerItems[corner] = item;
            cornerMenu.DropDownItems.Add(item);
        }

        var startupItem = new Forms.ToolStripMenuItem("Lancer au démarrage de Windows")
        {
            CheckOnClick = true,
            Checked = StartupService.IsEnabled(),
        };

        // Avec le démarrage administrateur, c'est la tâche planifiée qui lance Lux Monitor :
        // la case est cochée et grisée (on la gère depuis "Température et FPS…").
        if (adminStartupEnabled)
        {
            startupItem.Checked = true;
            startupItem.Enabled = false;
            startupItem.Text = "Lancer au démarrage de Windows (en administrateur)";
        }
        startupItem.CheckedChanged += (_, _) =>
        {
            if (!StartupService.SetEnabled(startupItem.Checked))
                startupItem.Checked = StartupService.IsEnabled(); // Échec : on réaffiche l'état réel.
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Afficher / masquer", null, (_, _) => toggleWindow());
        menu.Items.Add(_overlayItem);
        menu.Items.Add(cornerMenu);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(startupItem);
        menu.Items.Add("Température et FPS…", null, (_, _) => openWelcome());
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

    /// <summary>Coche "Mode jeu" dans le menu quand il est actif.</summary>
    public void SetOverlayActive(bool isActive) => _overlayItem.Checked = isActive;

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
