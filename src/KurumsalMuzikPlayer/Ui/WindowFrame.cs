using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace KurumsalMuzikPlayer.Ui;

/// <summary>
/// Pencere çerçevesi (karar Onur, 24 Eki 2026): içerik 1040×610 tasarım boyutunda çizilir ve tek parça ölçeklenir;
/// pencere büyütülüp küçültülebilir ama en-boy oranı sabittir (tasarım bozulmaz). Alt sınır küçük ekranlar için,
/// üst sınır ekranın çalışma alanı. Büyütme (tam ekran) düğmesi kapalı. Windows 10/11'de başlık çubuğu koyu.
/// WM_SIZING her Windows sürümünde (7–11) aynı çalışır.
/// </summary>
static class WindowFrame
{
    public const double DesignWidth = 1040;
    public const double DesignHeight = 610;
    public const double MinScale = 0.72;
    public const double MaxScale = 1.6;

    const int WM_SIZING = 0x0214;
    const int GWL_STYLE = -16;
    const int WS_MAXIMIZEBOX = 0x10000;
    const int WMSZ_LEFT = 1, WMSZ_RIGHT = 2, WMSZ_TOP = 3, WMSZ_TOPLEFT = 4, WMSZ_TOPRIGHT = 5, WMSZ_BOTTOM = 6, WMSZ_BOTTOMLEFT = 7, WMSZ_BOTTOMRIGHT = 8;

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    /// <summary>Pencere tutamacı oluşunca çağrılır: büyütme düğmesini kaldırır, oran kilidini ve koyu başlığı kurar.</summary>
    public static void Attach(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        var style = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZEBOX);
        TryDarkTitleBar(hwnd);
        HwndSource.FromHwnd(hwnd)?.AddHook((IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg == WM_SIZING) Constrain(w, h, wParam.ToInt32(), lParam);
            return IntPtr.Zero;
        });
    }

    /// <summary>İstemci alanı tasarım oranında kalacak şekilde sürüklenen dikdörtgeni düzeltir (piksel cinsinden).</summary>
    static void Constrain(Window w, IntPtr hwnd, int edge, IntPtr lParam)
    {
        var r = (RECT)Marshal.PtrToStructure(lParam, typeof(RECT))!;
        GetWindowRect(hwnd, out var win);
        GetClientRect(hwnd, out var client);
        var chromeW = (win.Right - win.Left) - (client.Right - client.Left);
        var chromeH = (win.Bottom - win.Top) - (client.Bottom - client.Top);
        var dpi = VisualTreeHelper.GetDpi(w).DpiScaleX;
        var work = SystemParameters.WorkArea;
        var maxScale = Math.Min(MaxScale, Math.Min((work.Width * dpi - chromeW) / (DesignWidth * dpi), (work.Height * dpi - chromeH) / (DesignHeight * dpi)));
        var minScale = Math.Min(MinScale, maxScale);

        double cw = r.Right - r.Left - chromeW, ch = r.Bottom - r.Top - chromeH;
        // Yatay kenar ya da köşe: genişlik belirler; dikey kenar: yükseklik belirler.
        var byHeight = edge is WMSZ_TOP or WMSZ_BOTTOM;
        var scale = byHeight ? ch / (DesignHeight * dpi) : cw / (DesignWidth * dpi);
        scale = Math.Max(minScale, Math.Min(maxScale, scale));
        var newW = (int)Math.Round(DesignWidth * dpi * scale) + chromeW;
        var newH = (int)Math.Round(DesignHeight * dpi * scale) + chromeH;

        if (edge is WMSZ_LEFT or WMSZ_TOPLEFT or WMSZ_BOTTOMLEFT) r.Left = r.Right - newW;
        else r.Right = r.Left + newW;
        if (edge is WMSZ_TOP or WMSZ_TOPLEFT or WMSZ_TOPRIGHT) r.Top = r.Bottom - newH;
        else r.Bottom = r.Top + newH;
        Marshal.StructureToPtr(r, lParam, false);
    }

    /// <summary>Açılışta ölçek: kayıtlı ölçek ya da 1; ekrana sığmıyorsa sığacak kadar küçültülür.</summary>
    public static double FitScale(double wanted)
    {
        var work = SystemParameters.WorkArea;
        // Başlık çubuğu ve kenarlar için pay (Windows 10/11'de ~40 px yükseklik, ~16 px genişlik).
        var fit = Math.Min((work.Width - 24) / DesignWidth, (work.Height - 48) / DesignHeight);
        return Math.Max(Math.Min(MinScale, fit), Math.Min(Math.Min(MaxScale, fit), wanted));
    }

    static void TryDarkTitleBar(IntPtr hwnd)
    {
        try
        {
            var on = 1;
            // DWMWA_USE_IMMERSIVE_DARK_MODE: 20 (Windows 10 2004+ ve 11), eski 10 sürümlerinde 19. Windows 7'de yok sayılır.
            if (DwmSetWindowAttribute(hwnd, 20, ref on, 4) != 0) DwmSetWindowAttribute(hwnd, 19, ref on, 4);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
