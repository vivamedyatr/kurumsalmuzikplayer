using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KurumsalMuzikPlayer.Ui;

/// <summary>Web tasarımındaki görsel parçalar: kumlu doku, tarz renk geçişleri, kapaklar.</summary>
static class Visuals
{
    /// <summary>Web'deki --grain dokusu: tarz kapaklarının üstünde ince kum.</summary>
    public static readonly Brush Grain = CreateGrain();

    static Brush CreateGrain()
    {
        const int size = 128;
        var rng = new Random(7);
        var px = new byte[size * size * 4];
        for (var i = 0; i < size * size; i++)
        {
            var v = (byte)rng.Next(256);
            px[i * 4] = px[i * 4 + 1] = px[i * 4 + 2] = v;
            px[i * 4 + 3] = (byte)rng.Next(20, 70);
        }
        var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, px, size * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, size, size),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }

    public static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return null; }
    }

    public static Brush? Solid(string? hex) => ParseColor(hex) is Color c ? Frozen(new SolidColorBrush(c)) : null;

    /// <summary>Tarz kapağı renk geçişi (web Cover: 135°, from → to).</summary>
    public static Brush Gradient(string? from, string? to)
    {
        var a = ParseColor(from) ?? Color.FromRgb(0x2A, 0x26, 0x23);
        var b = ParseColor(to) ?? a;
        return Frozen(new LinearGradientBrush(a, b, new Point(0, 0), new Point(1, 1)));
    }

    static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    static readonly Dictionary<string, ImageSource?> Cache = new();

    /// <summary>Kapak dosyasını yükler (dosyayı kilitlemez). Açılamazsa (ör. Windows 7'de webp) null: tarz kapağı görünür.</summary>
    public static ImageSource? Image(string? path, int decodeWidth)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        var key = path + "|" + decodeWidth;
        if (Cache.TryGetValue(key, out var cached)) return cached;
        ImageSource? img = null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = decodeWidth;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            img = bmp;
        }
        catch { }
        if (Cache.Count > 200) Cache.Clear();
        Cache[key] = img;
        return img;
    }

    static readonly Dictionary<string, ImageSource?> AmbientCache = new();

    /// <summary>
    /// Kapaktan yayılan renk (YouTube Music gibi): kapak 6×6 renge indirgenir, aradaki renkler yumuşak geçişle 48×48'e
    /// açılır. Küçük kapağı doğrudan büyütmek yazılımla çizimde keskin lekeler bırakıyordu (beyaz yazılı kapaklarda).
    /// </summary>
    public static ImageSource? Ambient(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        if (AmbientCache.TryGetValue(path!, out var cached)) return cached;
        ImageSource? result = null;
        try
        {
            const int n = 6, size = 48;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = n;
            bmp.DecodePixelHeight = n;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            var src = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            var px = new byte[n * n * 4];
            src.CopyPixels(px, n * 4, 0);
            var outPx = new byte[size * size * 4];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    // Çift doğrusal ara değer: 6×6 renk ızgarasından yumuşak geçiş.
                    double fx = (x + 0.5) * n / size - 0.5, fy = (y + 0.5) * n / size - 0.5;
                    int x0 = Math.Max(0, (int)Math.Floor(fx)), y0 = Math.Max(0, (int)Math.Floor(fy));
                    int x1 = Math.Min(n - 1, x0 + 1), y1 = Math.Min(n - 1, y0 + 1);
                    double tx = Math.Max(0, Math.Min(1, fx - x0)), ty = Math.Max(0, Math.Min(1, fy - y0));
                    for (var c = 0; c < 3; c++)
                    {
                        double a = px[(y0 * n + x0) * 4 + c], b = px[(y0 * n + x1) * 4 + c];
                        double d = px[(y1 * n + x0) * 4 + c], e = px[(y1 * n + x1) * 4 + c];
                        outPx[(y * size + x) * 4 + c] = (byte)((a * (1 - tx) + b * tx) * (1 - ty) + (d * (1 - tx) + e * tx) * ty);
                    }
                    outPx[(y * size + x) * 4 + 3] = 255;
                }
            var amb = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, outPx, size * 4);
            amb.Freeze();
            result = amb;
        }
        catch { }
        if (AmbientCache.Count > 100) AmbientCache.Clear();
        AmbientCache[path!] = result;
        return result;
    }

    /// <summary>Tarz adları çoğunlukla İngilizce: büyük harfe dil kuralsız çevrilir ("HIGH", "HİGH" değil).</summary>
    public static string Upper(string? s) => (s ?? "").ToUpperInvariant();

    /// <summary>WPF'te harf aralığı yok: küçük başlıklarda harflerin arasına ince boşluk konur (web letter-spacing).</summary>
    public static string Tracked(string? s) => string.Join(" ", (s ?? "").Select(c => c.ToString()));
}
