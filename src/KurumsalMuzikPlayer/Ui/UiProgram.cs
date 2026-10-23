using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Kmp.Core;

namespace KurumsalMuzikPlayer.Ui;

static class UiProgram
{
    public static int Run(bool minimized)
    {
        Log.Init("pencere");
        using var mutex = new Mutex(true, Ipc.UiMutex, out var created);
        if (!created) return 0; // pencere zaten açık; motor onu öne getirir
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Pencere çöktü", e.ExceptionObject as Exception);
        // Eski/zayıf ekran kartlarında takılmasın: arayüz yazılımla çizilir (BRIEF §3).
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Error("Pencere hatası", e.Exception);
            e.Handled = true;
        };
        var window = new MainWindow();
        if (minimized)
        {
            window.WindowState = WindowState.Minimized;
            window.ShowActivated = false;
        }
        Log.Info("Pencere açıldı");
        app.Run(window);
        Log.Info("Pencere kapandı");
        return 0;
    }
}
