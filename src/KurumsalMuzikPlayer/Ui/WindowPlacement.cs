using System;
using System.IO;
using System.Windows;
using Kmp.Core;

namespace KurumsalMuzikPlayer.Ui;

/// <summary>
/// Pencerenin boyutu ve yeri hatırlanır (eski BRIEF karar 26): pencere.json. Kayıtlı yer artık ekranda değilse
/// (monitör söküldü, çözünürlük değişti) ortada açılır; küçük ekrana sığdırılır.
/// </summary>
static class WindowPlacement
{
    sealed class Saved
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool Maximized { get; set; }
    }

    static string FilePath => Path.Combine(AppPaths.DataDir, "pencere.json");

    public static void Restore(Window w)
    {
        var s = JsonFile.Read<Saved>(FilePath);
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var work = SystemParameters.WorkArea;
        if (s != null && s.Width >= w.MinWidth && s.Height >= w.MinHeight)
        {
            w.Width = Math.Min(s.Width, work.Width);
            w.Height = Math.Min(s.Height, work.Height);
            // Başlık çubuğunun bir kısmı ekranda kalmalı ki pencere tutulup taşınabilsin.
            if (screen.Contains(new Point(s.Left + 40, s.Top + 10)) && screen.Contains(new Point(s.Left + Math.Min(s.Width, 200), s.Top + 10)))
            {
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                w.Left = s.Left;
                w.Top = s.Top;
            }
            if (s.Maximized) w.WindowState = WindowState.Maximized;
        }
        else
        {
            w.Width = Math.Min(w.Width, work.Width);
            w.Height = Math.Min(w.Height, work.Height);
        }
    }

    public static void Save(Window w)
    {
        if (w.WindowState == WindowState.Minimized) return;
        var r = w.WindowState == WindowState.Maximized ? w.RestoreBounds : new Rect(w.Left, w.Top, w.ActualWidth, w.ActualHeight);
        if (r.IsEmpty || r.Width < 100) return;
        JsonFile.Write(FilePath, new Saved { Left = r.Left, Top = r.Top, Width = r.Width, Height = r.Height, Maximized = w.WindowState == WindowState.Maximized });
    }
}
