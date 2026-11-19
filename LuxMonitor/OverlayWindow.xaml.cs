using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using LuxMonitor.Models;

namespace LuxMonitor;

/// <summary>
/// Fenêtre du mode jeu : transparente, toujours au premier plan, traversée par les clics.
///
/// Méthode volontairement simple et sûre : une fenêtre Windows ordinaire posée par-dessus,
/// sans rien injecter dans le jeu (contrairement à RivaTuner). Aucun risque avec les anti-triche.
/// Limite : invisible au-dessus d'un jeu en plein écran "exclusif" (rare sur Windows 11).
/// </summary>
public partial class OverlayWindow : Window
{
    private const double ScreenMargin = 12;

    // Styles étendus de fenêtre Windows (API user32).
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020; // Les clics traversent la fenêtre
    private const int WsExToolWindow = 0x00000080;  // Absente de Alt+Tab
    private const int WsExNoActivate = 0x08000000;  // Ne prend jamais le focus (le jeu le garde)

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    private readonly DispatcherTimer _keepOnTopTimer;
    private readonly Action<double, double> _positionChanged;
    private readonly Action _exitRequested;
    private readonly DispatcherTimer _hoverTimer;
    private bool _isOverExitButton;
    private OverlayCorner _corner;
    private Point? _customPosition;
    private bool _isMoving;

    /// <param name="positionChanged">Appelé quand l'utilisateur a déplacé la barre (pour l'enregistrer).</param>
    /// <param name="exitRequested">Appelé quand on clique sur le bouton de retour au widget.</param>
    public OverlayWindow(object dataContext, OverlayCorner corner, Point? customPosition,
        Action<double, double> positionChanged, Action exitRequested)
    {
        InitializeComponent();
        DataContext = dataContext;
        _corner = corner;
        _customPosition = customPosition;
        _positionChanged = positionChanged;
        _exitRequested = exitRequested;

        // Une fenêtre traversée par les clics ne "voit" pas la souris : on regarde donc nous-mêmes,
        // plusieurs fois par seconde, si le curseur survole le bouton de retour.
        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _hoverTimer.Tick += (_, _) => UpdateExitButtonHover();

        // La taille change avec le texte : on recale la fenêtre à chaque fois.
        SizeChanged += (_, _) => Reposition();

        // Certains jeux repassent devant les autres fenêtres : on réaffirme régulièrement
        // la position "au premier plan", sans voler le focus.
        _keepOnTopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _keepOnTopTimer.Tick += (_, _) => KeepOnTop();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                _keepOnTopTimer.Start();
                _hoverTimer.Start();
            }
            else
            {
                _keepOnTopTimer.Stop();
                _hoverTimer.Stop();
            }
        };
    }

    /// <summary>Place la barre dans un coin (annule une position personnalisée).</summary>
    public void SetCorner(OverlayCorner corner)
    {
        _corner = corner;
        _customPosition = null;
        Reposition();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        SetWindowStyles(clickThrough: true, noActivate: true);
    }

    // ---- Bouton de retour au widget ----

    /// <summary>
    /// Astuce : la fenêtre reste traversée par les clics, SAUF quand la souris est sur le bouton.
    /// À ce moment-là seulement, elle devient cliquable (sans jamais prendre le focus au jeu).
    /// </summary>
    private void UpdateExitButtonHover()
    {
        if (_isMoving || !IsVisible)
            return;

        bool isOver = IsCursorOver(ExitButton);
        if (isOver == _isOverExitButton)
            return;

        _isOverExitButton = isOver;
        SetWindowStyles(clickThrough: !isOver, noActivate: true);
        ExitButton.Opacity = isOver ? 1 : 0.6;
        ExitButton.Background = isOver
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF))
            : System.Windows.Media.Brushes.Transparent;
    }

    /// <summary>Compare la position du curseur (en pixels écran) au rectangle de l'élément.</summary>
    private static bool IsCursorOver(FrameworkElement element)
    {
        if (!GetCursorPos(out NativePoint cursor) || PresentationSource.FromVisual(element) is null)
            return false;

        Point topLeft = element.PointToScreen(new Point(0, 0));
        Point bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));

        return cursor.X >= topLeft.X && cursor.X <= bottomRight.X
            && cursor.Y >= topLeft.Y && cursor.Y <= bottomRight.Y;
    }

    private void ExitButton_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isMoving)
            return;

        _isOverExitButton = false;
        ExitButton.Opacity = 0.6;
        ExitButton.Background = System.Windows.Media.Brushes.Transparent;
        _exitRequested();
    }

    // ---- Déplacement ----

    /// <summary>
    /// Rend la barre temporairement cliquable pour la déplacer à la souris.
    /// Dès qu'on relâche le bouton (ou qu'on appuie sur Échap/Entrée), elle redevient traversée par les clics.
    /// </summary>
    public void BeginMove()
    {
        if (!IsVisible)
            Show();

        _isMoving = true;
        SetWindowStyles(clickThrough: false, noActivate: false);
        MoveFrame.Visibility = Visibility.Visible;
        Cursor = System.Windows.Input.Cursors.SizeAll;
        Activate();
    }

    private void EndMove()
    {
        if (!_isMoving)
            return;

        _isMoving = false;
        MoveFrame.Visibility = Visibility.Collapsed;
        Cursor = null;
        SetWindowStyles(clickThrough: true, noActivate: true);
        _isOverExitButton = false;

        _customPosition = new Point(Left, Top);
        _positionChanged(Left, Top);
    }

    private void Overlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isMoving)
            return;

        DragMove(); // Rend la main quand on relâche le bouton.
        EndMove();
    }

    private void Overlay_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Escape or System.Windows.Input.Key.Enter)
            EndMove();
    }

    /// <summary>
    /// Règle deux comportements de la fenêtre :
    /// - clickThrough : les clics passent à travers, vers ce qu'il y a en dessous ;
    /// - noActivate : la fenêtre ne prend jamais le focus (le jeu le garde).
    /// </summary>
    private void SetWindowStyles(bool clickThrough, bool noActivate)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        int exStyle = GetWindowLong(hwnd, GwlExStyle) | WsExToolWindow;
        exStyle = clickThrough ? exStyle | WsExTransparent : exStyle & ~WsExTransparent;
        exStyle = noActivate ? exStyle | WsExNoActivate : exStyle & ~WsExNoActivate;
        SetWindowLong(hwnd, GwlExStyle, exStyle);

        IsHitTestVisible = !clickThrough;
    }

    // ---- Position ----

    /// <summary>
    /// Position personnalisée si l'utilisateur a déplacé la barre, sinon le coin choisi.
    /// Les coins sont calculés dans la zone de travail (hors barre des tâches), où qu'elle soit :
    /// la barre ne passe ainsi jamais sous la barre des tâches.
    /// </summary>
    private void Reposition()
    {
        if (_isMoving)
            return;

        if (_customPosition is Point custom)
        {
            // On garde la barre visible même si un écran a été débranché depuis.
            double maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - ActualWidth;
            double maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - ActualHeight;
            Left = Math.Clamp(custom.X, SystemParameters.VirtualScreenLeft, Math.Max(SystemParameters.VirtualScreenLeft, maxLeft));
            Top = Math.Clamp(custom.Y, SystemParameters.VirtualScreenTop, Math.Max(SystemParameters.VirtualScreenTop, maxTop));
            return;
        }

        Rect area = SystemParameters.WorkArea;
        bool left = _corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft;
        bool top = _corner is OverlayCorner.TopLeft or OverlayCorner.TopRight;

        Left = left ? area.Left + ScreenMargin : area.Right - ActualWidth - ScreenMargin;
        Top = top ? area.Top + ScreenMargin : area.Bottom - ActualHeight - ScreenMargin;
    }

    private void KeepOnTop()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    // ---- Appels à l'API Windows (P/Invoke) ----

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newValue);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
