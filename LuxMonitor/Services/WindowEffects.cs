using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LuxMonitor.Services;

/// <summary>
/// Demande à Windows 11 (au compositeur DWM) de flouter ce qu'il y a derrière la fenêtre :
/// c'est l'effet Acrylic, celui des menus de Windows 11. C'est Windows qui fait le calcul,
/// pas notre application : l'effet est fluide et ne coûte presque rien.
/// API documentée par Microsoft (DwmSetWindowAttribute), disponible depuis Windows 11 22H2.
/// </summary>
public static class WindowEffects
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    private const int DwmwcpRound = 2;              // Coins arrondis
    private const int DwmsbtNone = 1;               // Pas d'effet
    private const int DwmsbtTransientWindow = 3;    // Acrylic

    private const int GwlStyle = -16;
    private const int WsSysMenu = 0x00080000;

    private const int Windows11_22H2Build = 22621;

    /// <summary>
    /// Préparation, une seule fois à la création de la fenêtre :
    /// coins arrondis, pas de boutons système, surface prête à recevoir un effet.
    /// </summary>
    public static void Prepare(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        // Retire les boutons système (réduire, agrandir, fermer) que Windows dessinerait par-dessus notre contenu.
        int style = GetWindowLong(hwnd, GwlStyle);
        SetWindowLong(hwnd, GwlStyle, style & ~WsSysMenu);

        SetAttribute(hwnd, DwmwaWindowCornerPreference, DwmwcpRound);

        // WPF peint normalement un fond opaque : on le rend transparent pour laisser voir l'effet,
        // et on étend le "cadre" de la fenêtre à toute sa surface.
        if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target)
            target.BackgroundColor = Colors.Transparent;

        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    /// <summary>
    /// Active ou désactive l'effet Acrylic, en teinte sombre ou claire.
    /// Peut être appelé à tout moment (changement de thème).
    /// Renvoie true si l'Acrylic est effectivement actif.
    /// </summary>
    public static bool SetBackdrop(Window window, bool useAcrylic, bool isDark)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return false;

        SetAttribute(hwnd, DwmwaUseImmersiveDarkMode, isDark ? 1 : 0);

        if (Environment.OSVersion.Version.Build < Windows11_22H2Build)
            return false;

        int backdrop = useAcrylic ? DwmsbtTransientWindow : DwmsbtNone;
        bool succeeded = SetAttribute(hwnd, DwmwaSystemBackdropType, backdrop) == 0;
        return useAcrylic && succeeded;
    }

    private static int SetAttribute(IntPtr hwnd, int attribute, int value)
        => DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));

    // ---- Appels à l'API Windows (P/Invoke) ----

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newValue);
}
