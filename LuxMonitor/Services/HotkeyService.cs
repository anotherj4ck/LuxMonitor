using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LuxMonitor.Services;

/// <summary>
/// Raccourci clavier global (Ctrl+Maj+L) : fonctionne même quand Lux Monitor n'a pas le focus,
/// par exemple en plein jeu. On demande à Windows de nous prévenir (RegisterHotKey) : c'est la
/// méthode officielle, sans espionner le clavier (pas de "keylogger" ni de hook clavier).
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4C55; // Identifiant arbitraire propre à Lux Monitor.

    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000; // Un seul déclenchement si la touche reste enfoncée.
    private const uint VirtualKeyL = 0x4C;

    private readonly IntPtr _hwnd;
    private readonly HwndSource? _source;
    private readonly Action _onPressed;

    /// <summary>False si une autre application utilise déjà ce raccourci.</summary>
    public bool IsRegistered { get; }

    /// <param name="window">Fenêtre qui recevra le message (doit déjà exister : appeler après OnSourceInitialized).</param>
    public HotkeyService(Window window, Action onPressed)
    {
        _onPressed = onPressed;
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        IsRegistered = RegisterHotKey(_hwnd, HotkeyId, ModControl | ModShift | ModNoRepeat, VirtualKeyL);
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            _onPressed();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (IsRegistered)
            UnregisterHotKey(_hwnd, HotkeyId);
        _source?.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
