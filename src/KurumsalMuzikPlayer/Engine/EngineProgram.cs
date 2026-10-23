using System;
using System.IO;
using System.IO.Pipes;
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
        using var mutex = new Mutex(true, Ipc.EngineMutex, out var created);
        if (!created)
        {
            // Motor zaten çalışıyor: sadece pencereyi açtır.
            AskRunningEngineToShow();
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

    static void AskRunningEngineToShow()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", Ipc.EnginePipe, PipeDirection.Out);
            pipe.Connect(3000);
            var bytes = new UTF8Encoding(false).GetBytes("{\"t\":\"hello\",\"role\":\"show\"}\n");
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();
        }
        catch (Exception e)
        {
            Log.Warn($"Çalışan motora ulaşılamadı: {e.Message}");
        }
    }
}
