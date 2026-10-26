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
        // Pencere kapatılınca gizlenir (MainWindow.Closing); süreç ancak Quit ile biter.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Error("Pencere hatası", e.Exception);
            e.Handled = true;
        };
        var window = new MainWindow();
        window.Closed += (_, _) => app.Shutdown();
        app.SessionEnding += (_, _) => window.Quit();
        if (minimized)
        {
            // Windows açılışında ya da donma sonrası: pencere çizilmeden arka planda hazır bekler (tepside).
            new WindowInteropHelper(window).EnsureHandle();
            Log.Info("Pencere arka planda açıldı (tepside)");
        }
        else
        {
            window.Show();
            Log.Info("Pencere açıldı");
        }
        app.Run();
        Log.Info("Pencere kapandı");
        return 0;
    }
}
