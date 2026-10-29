using System;
using System.IO;
using System.Windows;
using Kmp.Core;

namespace KurumsalMuzikPlayer.Ui;

/// <summary>
/// Pencerenin yeri ve ölçeği hatırlanır (pencere.json). Kayıtlı yer artık ekranda değilse (monitör söküldü, çözünürlük
/// değişti) ortada açılır; ölçek ekrana sığacak şekilde sınırlanır (WindowFrame).
/// </summary>
static class WindowPlacement
{
    sealed class Saved
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Scale { get; set; } = 1;
    }

    static string FilePath => Path.Combine(AppPaths.DataDir, "pencere.json");

    /// <summary>Yeri uygular, açılış ölçeğini döner.</summary>
    public static double Restore(Window w)
    {
        var s = JsonFile.Read<Saved>(FilePath);
        var scale = WindowFrame.FitScale(s?.Scale is > 0 ? s.Scale : 1);
        if (s == null) return scale;
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        // Başlık çubuğunun bir kısmı ekranda kalmalı ki pencere tutulup taşınabilsin.
        if (screen.Contains(new Point(s.Left + 40, s.Top + 10)) && screen.Contains(new Point(s.Left + 200, s.Top + 10)))
        {
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Left = s.Left;
            w.Top = s.Top;
        }
        return scale;
    }

    /// <summary>
    /// Pencere geri getirilince: başlık çubuğu ekranda değilse (ekran kapanıp açıldı, çözünürlük ya da monitör değişti)
    /// çalışma alanının ortasına alınır. Taşındıysa true.
    /// </summary>
    public static bool EnsureOnScreen(Window w)
    {
        if (w.WindowState != WindowState.Normal) return false;
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (screen.Contains(new Point(w.Left + 40, w.Top + 10)) && screen.Contains(new Point(w.Left + 200, w.Top + 10))) return false;
        var work = SystemParameters.WorkArea;
        w.Left = work.Left + Math.Max(0, (work.Width - w.ActualWidth) / 2);
        w.Top = work.Top + Math.Max(0, (work.Height - w.ActualHeight) / 2);
        return true;
    }

    public static void Save(Window w, double scale)
    {
        if (w.WindowState != WindowState.Normal || double.IsNaN(scale) || scale <= 0) return;
        JsonFile.Write(FilePath, new Saved { Left = w.Left, Top = w.Top, Scale = scale });
    }
}
