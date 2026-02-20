using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace RenoDXChecker;

public sealed partial class AboutWindow : Window
{
    // Sensible default — used on first open before any saved size exists
    private const int DefaultWidth  = 680;
    private const int DefaultHeight = 740;

    public AboutWindow()
    {
        InitializeComponent();
        Title = "About — RenoDX Mod Manager";
        // Set a sensible default size immediately so the window isn't huge on first open.
        // TryRestoreBounds (called on Activated) will override this with the saved size
        // from the previous session, if one exists.
        AppWindow.Resize(new Windows.Graphics.SizeInt32(DefaultWidth, DefaultHeight));
        // Restore after activated
        this.Activated += AboutWindow_Activated;
        this.Closed += AboutWindow_Closed;
    }

    private void AboutWindow_Activated(object? sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
    {
        try
        {
            this.Activated -= AboutWindow_Activated;
            TryRestoreBounds();
        }
        catch { }
    }

    private void AboutWindow_Closed(object? sender, Microsoft.UI.Xaml.WindowEventArgs e)
    {
        try { SaveBounds(); } catch { }
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    // JSON file path — works for unpackaged WinUI 3 (ApplicationData.Current throws without package identity)
    private static readonly string _settingsPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXChecker", "window_about.json");

    private void TryRestoreBounds()
    {
        try
        {
            if (!System.IO.File.Exists(_settingsPath)) return;
            var json = System.IO.File.ReadAllText(_settingsPath);
            var doc  = System.Text.Json.JsonDocument.Parse(json).RootElement;
            if (doc.TryGetProperty("X", out var jx) && doc.TryGetProperty("Y", out var jy) &&
                doc.TryGetProperty("W", out var jw) && doc.TryGetProperty("H", out var jh))
            {
                var x = jx.GetInt32(); var y = jy.GetInt32();
                var w = jw.GetInt32(); var h = jh.GetInt32();
                if (w >= 300 && h >= 200 && w <= 7680 && h <= 4320)
                {
                    var hwnd = WindowNative.GetWindowHandle(this);
                    SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, 0x0040);
                }
            }
        }
        catch { }
    }

    private void SaveBounds()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            if (!GetWindowRect(hwnd, out var r)) return;
            var w = r.Right - r.Left;
            var h = r.Bottom - r.Top;
            if (w < 100 || h < 100) return;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_settingsPath)!);
            var json = System.Text.Json.JsonSerializer.Serialize(
                new { X = r.Left, Y = r.Top, W = w, H = h });
            System.IO.File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }
}
