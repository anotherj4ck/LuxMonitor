using System.Drawing;
using System.Windows;
using LuxMonitor.Localization;
using LuxMonitor.Models;
using Forms = System.Windows.Forms;

namespace LuxMonitor.Services;

/// <summary>
/// Icône dans la zone de notification (à côté de l'horloge), avec son menu.
/// WPF n'a pas de composant pour ça : on emprunte le NotifyIcon de Windows Forms,
/// fourni avec .NET. Pas de bibliothèque externe à ajouter.
///
/// Le menu est construit une seule fois ; ses textes sont réécrits quand la langue change (UpdateTexts).
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly bool _adminStartupEnabled;

    // Éléments du menu dont le texte dépend de la langue.
    private readonly Forms.ToolStripMenuItem _showHideItem;
    private readonly Forms.ToolStripMenuItem _lockItem;
    private readonly Forms.ToolStripMenuItem _overlayItem;
    private readonly Forms.ToolStripMenuItem _cornerMenu;
    private readonly Forms.ToolStripMenuItem _moveItem;
    private readonly Dictionary<OverlayCorner, Forms.ToolStripMenuItem> _cornerItems = [];
    private readonly Forms.ToolStripMenuItem _startupItem;
    private readonly Forms.ToolStripMenuItem _languageMenu;
    private readonly Forms.ToolStripMenuItem _languageAutoItem;
    private readonly Forms.ToolStripMenuItem _languageFrenchItem;
    private readonly Forms.ToolStripMenuItem _languageEnglishItem;
    private readonly Forms.ToolStripMenuItem _tempFpsItem;
    private readonly Forms.ToolStripMenuItem _exitItem;

    private static readonly (OverlayCorner Corner, string Key)[] Corners =
    [
        (OverlayCorner.TopLeft, "Corner.TopLeft"),
        (OverlayCorner.TopRight, "Corner.TopRight"),
        (OverlayCorner.BottomLeft, "Corner.BottomLeft"),
        (OverlayCorner.BottomRight, "Corner.BottomRight"),
    ];

    public TrayIconService(
        Action toggleWindow,
        bool windowLocked,
        Action<bool> setWindowLocked,
        AppLanguage? languageChoice,
        Action<AppLanguage?> setLanguage,
        Action toggleOverlay,
        Action moveOverlay,
        Action<OverlayCorner> setOverlayCorner,
        OverlayCorner currentCorner,
        Action openWelcome,
        bool adminStartupEnabled,
        Action exitApplication)
    {
        _adminStartupEnabled = adminStartupEnabled;

        _showHideItem = new Forms.ToolStripMenuItem();
        _showHideItem.Click += (_, _) => toggleWindow();

        // Figer : la fenêtre ne bouge plus et ne se redimensionne plus (évite les déplacements par erreur).
        _lockItem = new Forms.ToolStripMenuItem { CheckOnClick = true, Checked = windowLocked };
        _lockItem.CheckedChanged += (_, _) => setWindowLocked(_lockItem.Checked);

        // Mode jeu + choix du coin (sous-menu avec une coche sur le coin actif).
        _overlayItem = new Forms.ToolStripMenuItem();
        _overlayItem.Click += (_, _) => toggleOverlay();

        _cornerMenu = new Forms.ToolStripMenuItem();
        _moveItem = new Forms.ToolStripMenuItem();
        _moveItem.Click += (_, _) =>
        {
            moveOverlay();
            foreach (var cornerItem in _cornerItems.Values)
                cornerItem.Checked = false; // Position libre : plus aucun coin coché.
        };
        _cornerMenu.DropDownItems.Add(_moveItem);
        _cornerMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
        foreach (var (corner, _) in Corners)
        {
            var item = new Forms.ToolStripMenuItem { Checked = corner == currentCorner };
            item.Click += (_, _) =>
            {
                setOverlayCorner(corner);
                foreach (var (otherCorner, otherItem) in _cornerItems)
                    otherItem.Checked = otherCorner == corner;
            };
            _cornerItems[corner] = item;
            _cornerMenu.DropDownItems.Add(item);
        }

        _startupItem = new Forms.ToolStripMenuItem { CheckOnClick = true, Checked = StartupService.IsEnabled() };

        // Avec le démarrage administrateur, c'est la tâche planifiée qui lance Lux Monitor :
        // la case est cochée et grisée (on la gère depuis "Température et FPS…").
        if (adminStartupEnabled)
        {
            _startupItem.Checked = true;
            _startupItem.Enabled = false;
        }
        _startupItem.CheckedChanged += (_, _) =>
        {
            if (!StartupService.SetEnabled(_startupItem.Checked))
                _startupItem.Checked = StartupService.IsEnabled(); // Échec : on réaffiche l'état réel.
        };

        // Langue : automatique (celle de Windows), ou forcée. Les noms des langues restent
        // écrits dans leur propre langue ("Français", "English") : on les reconnaît toujours.
        _languageMenu = new Forms.ToolStripMenuItem();
        _languageAutoItem = new Forms.ToolStripMenuItem();
        _languageFrenchItem = new Forms.ToolStripMenuItem("Français");
        _languageEnglishItem = new Forms.ToolStripMenuItem("English");
        (Forms.ToolStripMenuItem Item, AppLanguage? Choice)[] languages =
        [
            (_languageAutoItem, null),
            (_languageFrenchItem, AppLanguage.French),
            (_languageEnglishItem, AppLanguage.English),
        ];
        foreach (var (item, choice) in languages)
        {
            item.Checked = choice == languageChoice;
            item.Click += (_, _) =>
            {
                foreach (var (otherItem, otherChoice) in languages)
                    otherItem.Checked = otherChoice == choice;
                setLanguage(choice);
            };
            _languageMenu.DropDownItems.Add(item);
        }
        _languageMenu.DropDownItems.Insert(1, new Forms.ToolStripSeparator());

        _tempFpsItem = new Forms.ToolStripMenuItem();
        _tempFpsItem.Click += (_, _) => openWelcome();

        _exitItem = new Forms.ToolStripMenuItem();
        _exitItem.Click += (_, _) => exitApplication();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_showHideItem);
        menu.Items.Add(_lockItem);
        menu.Items.Add(_overlayItem);
        menu.Items.Add(_cornerMenu);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(_tempFpsItem);
        menu.Items.Add(_languageMenu);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_exitItem);

        UpdateTexts();
        Loc.Instance.LanguageChanged += UpdateTexts;

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

    /// <summary>Réécrit tous les textes du menu dans la langue active.</summary>
    private void UpdateTexts()
    {
        _showHideItem.Text = Loc.T("Tray.ShowHide");
        _lockItem.Text = Loc.T("Tray.Lock");
        _overlayItem.Text = Loc.T("Tray.GameMode");
        _overlayItem.ShortcutKeyDisplayString = Loc.T("Tray.Shortcut");
        _cornerMenu.Text = Loc.T("Tray.OverlayPosition");
        _moveItem.Text = Loc.T("Tray.MoveWithMouse");
        foreach (var (corner, key) in Corners)
            _cornerItems[corner].Text = Loc.T(key);
        _startupItem.Text = Loc.T(_adminStartupEnabled ? "Tray.StartupAdmin" : "Tray.Startup");
        _tempFpsItem.Text = Loc.T("Tray.TempFps");
        _languageMenu.Text = Loc.T("Tray.Language");
        _languageAutoItem.Text = Loc.T("Tray.LanguageAuto");
        _exitItem.Text = Loc.T("Tray.Exit");
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
        Loc.Instance.LanguageChanged -= UpdateTexts;

        // Sans ça, l'icône reste affichée après la fenêtre fermée, jusqu'à ce que la souris passe dessus.
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
