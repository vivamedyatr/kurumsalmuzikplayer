using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Kmp.Core;

namespace KurumsalMuzikPlayer.Engine;

static class EngineProgram
{
    public static int Run(Args args)
    {
        Log.Init("motor");
        if (args.Has("--kapat")) return QuitRunningEngine();
        using var mutex = new Mutex(true, Ipc.EngineMutex, out var created);
        if (!created)
        {
            // Motor zaten çalışıyor: sadece pencereyi açtır.
            SendHello("show");
            return 0;
        }
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Motor çöktü", e.ExceptionObject as Exception);
        Application.ThreadException += (_, e) => Log.Error("Motor ana iş parçacığı hatası", e.Exception);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            Application.Run(new EngineHost(args));
        }
        catch (Exception e)
        {
            Log.Error("Motor durdu", e);
            return 1;
        }
        Log.Info("Motor kapandı");
        return 0;
    }

    /// <summary>Kurulum/kaldırma öncesi: çalışan motoru düzgünce kapatır, programın bütün süreçleri bitene kadar bekler.</summary>
    static int QuitRunningEngine()
    {
        if (SendHello("quit")) Log.Info("Kurulum için motor kapatılıyor");
        var self = Process.GetCurrentProcess().Id;
        for (var i = 0; i < 100; i++)
        {
            var others = Process.GetProcessesByName("KurumsalMuzikPlayer").Where(p => p.Id != self).ToList();
            if (others.Count == 0) return 0;
            if (i == 60)
                foreach (var p in others)
                    try { p.Kill(); } catch { }
            Thread.Sleep(100);
        }
        return 1;
    }

    static bool SendHello(string role)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", Ipc.EnginePipe, PipeDirection.Out);
            pipe.Connect(3000);
            var bytes = new UTF8Encoding(false).GetBytes("{\"t\":\"hello\",\"role\":\"" + role + "\"}\n");
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();
            return true;
        }
        catch (Exception e)
        {
            Log.Warn($"Çalışan motora ulaşılamadı: {e.Message}");
            return false;
        }
    }
}
