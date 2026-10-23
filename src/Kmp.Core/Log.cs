using System;
using System.IO;
using System.Text;

namespace Kmp.Core;

/// <summary>Günlük: gunluk\&lt;süreç&gt;-yyyy-MM-dd.txt, 7 günden eskisi silinir (BRIEF §4).</summary>
public static class Log
{
    const int KeepDays = 7;
    static readonly object Gate = new();
    static string _tag = "motor";

    public static void Init(string tag)
    {
        _tag = tag;
        try
        {
            foreach (var f in Directory.GetFiles(AppPaths.LogDir, "*.txt"))
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-KeepDays)) File.Delete(f);
        }
        catch { }
    }

    public static void Info(string msg) => Write("", msg);
    public static void Warn(string msg) => Write("UYARI", msg);
    public static void Error(string msg, Exception? e = null) => Write("HATA", e == null ? msg : $"{msg}: {e}");

    static void Write(string level, string msg)
    {
        var now = DateTime.Now;
        var line = $"{now:HH:mm:ss.fff} {level,-5} {msg}{Environment.NewLine}";
        lock (Gate)
        {
            try { File.AppendAllText(Path.Combine(AppPaths.LogDir, $"{_tag}-{now:yyyy-MM-dd}.txt"), line, Encoding.UTF8); }
            catch { }
        }
    }
}
