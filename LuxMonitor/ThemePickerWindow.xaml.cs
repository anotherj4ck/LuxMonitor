using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LuxMonitor.Models;
using LuxMonitor.Services;
using LuxMonitor.ViewModels;

namespace LuxMonitor;

/// <summary>
/// Fenêtre de choix du thème, collée au widget.
///
/// Placement : on mesure la place libre à droite et à gauche du widget, SUR SON ÉCRAN
/// (zone de travail, donc sans la barre des tâches), et on s'ouvre du côté qui a le plus de place.
/// </summary>
public partial class ThemePickerWindow : Window
{
    private const double Gap = 8; // Espace entre le widget et cette fenêtre.

    private readonly Window _anchor;
    private readonly Action<ThemeDefinition> _themeSelected;
    private bool _isClosing;

    /// <param name="anchor">Le widget, à côté duquel on s'ouvre.</param>
    /// <param name="dataContext">Le MainViewModel (liste des thèmes, coche du thème actif).</param>
    /// <param name="themeSelected">Appelé quand l'utilisateur choisit un thème.</param>
    public ThemePickerWindow(Window anchor, object dataContext, Action<ThemeDefinition> themeSelected)
    {
        InitializeComponent();
        _anchor = anchor;
        _themeSelected = themeSelected;
        DataContext = dataContext;

        // Fenêtre "possédée" par le widget : elle reste au-dessus de lui et disparaît avec lui.
        Owner = anchor;

        // Un clic ailleurs (bureau, autre application, le widget lui-même) : on ferme.
        Deactivated += (_, _) => CloseOnce();

        // Première position avant l'affichage (évite un saut), puis ajustement avec la vraie hauteur.
        PlaceNextToAnchor();
        Loaded += (_, _) => PlaceNextToAnchor();
        SizeChanged += (_, _) => PlaceNextToAnchor();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Même apparence que le widget (Acrylic ou fond opaque selon le thème actif).
        WindowEffects.Prepare(this);
        ThemeManager.ApplyBackdrop(this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _isClosing = true;
        base.OnClosing(e);
    }

    // Fermer déclenche lui-même "Deactivated" : ce garde-fou évite de fermer deux fois.
    private void CloseOnce()
    {
        if (_isClosing)
            return;

        _isClosing = true;
        Close();
    }

    private void ThemeOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ThemeOptionViewModel option })
            return;

        _themeSelected(option.Theme);

        // On reste ouvert pour pouvoir comparer plusieurs thèmes : on adopte juste le nouveau fond.
        ThemeManager.ApplyBackdrop(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseOnce();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            CloseOnce();
    }

    /// <summary>Place la fenêtre à droite ou à gauche du widget, sans jamais sortir de l'écran.</summary>
    private void PlaceNextToAnchor()
    {
        Rect work = GetAnchorWorkArea();
        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight;

        double anchorRight = _anchor.Left + _anchor.ActualWidth;
        double spaceRight = work.Right - anchorRight;
        double spaceLeft = _anchor.Left - work.Left;

        // À droite s'il y a la place (ou plus de place qu'à gauche), sinon à gauche.
        bool openOnRight = spaceRight >= width + Gap || spaceRight >= spaceLeft;
        double left = openOnRight ? anchorRight + Gap : _anchor.Left - width - Gap;

        // Garde-fous : toujours entièrement visible, même sur un petit écran.
        Left = Math.Clamp(left, work.Left, Math.Max(work.Left, work.Right - width));
        Top = Math.Clamp(_anchor.Top, work.Top, Math.Max(work.Top, work.Bottom - height));
    }

    /// <summary>
    /// Zone de travail (écran sans la barre des tâches) de l'écran où se trouve le widget,
    /// convertie en unités WPF. Windows donne des pixels réels ; WPF travaille en pixels
    /// "logiques" (1/96 de pouce) : la conversion compte dès que la mise à l'échelle n'est pas à 100 %.
    /// </summary>
    private Rect GetAnchorWorkArea()
    {
        IntPtr handle = new WindowInteropHelper(_anchor).Handle;
        if (handle == IntPtr.Zero || PresentationSource.FromVisual(_anchor)?.CompositionTarget is not { } target)
            return SystemParameters.WorkArea;

        System.Drawing.Rectangle area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        Matrix toWpf = target.TransformFromDevice;
        Point topLeft = toWpf.Transform(new Point(area.Left, area.Top));
        Point bottomRight = toWpf.Transform(new Point(area.Right, area.Bottom));
        return new Rect(topLeft, bottomRight);
    }
}
