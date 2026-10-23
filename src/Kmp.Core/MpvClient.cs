using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Kmp.Core;

/// <summary>
/// Ayrı süreçte çalışan mpv.exe ve JSON IPC'si (adlandırılmış boru). BRIEF §3: müzik ve anons için birer tane.
/// Olaylar mpv okuma iş parçacığında gelir.
/// </summary>
public sealed class MpvClient : IDisposable
{
    readonly string _exe;
    readonly string _pipeName;
    readonly ConcurrentDictionary<int, Action<JToken?>> _pending = new();
    Process? _proc;
    LineChannel? _channel;
    int _requestId;
    volatile bool _stopping;

    public string Name { get; }

    /// <summary>Gözlenen bir özellik değişti (ad, değer).</summary>
    public event Action<string, JToken?>? PropertyChanged;
    /// <summary>mpv olayı (end-file, file-loaded …).</summary>
    public event Action<string, JObject>? Event;
    /// <summary>mpv beklenmedik şekilde kapandı.</summary>
    public event Action? Crashed;

    public MpvClient(string exe, string name)
    {
        _exe = exe;
        Name = name;
        _pipeName = $"kmp-mpv-{name}-{Process.GetCurrentProcess().Id}";
    }

    public bool IsRunning => _proc is { HasExited: false } && _channel is { IsOpen: true };

    public void Start(IEnumerable<string> extraArgs)
    {
        _stopping = false;
        var args = new List<string>
        {
            "--no-config", "--idle=yes", "--no-video", "--audio-display=no", "--no-terminal",
            $@"--input-ipc-server=\\.\pipe\{_pipeName}",
        };
        args.AddRange(extraArgs);
        var psi = new ProcessStartInfo(_exe, string.Join(" ", args.Select(Quote)))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(_exe),
        };
        var proc = Process.Start(psi) ?? throw new IOException("mpv başlatılamadı");
        ChildProcessJob.Add(proc);
        try { proc.PriorityClass = ProcessPriorityClass.AboveNormal; } catch { }
        proc.EnableRaisingEvents = true;
        proc.Exited += (_, _) =>
        {
            if (_stopping) return;
            Log.Warn($"mpv ({Name}) kapandı, çıkış kodu {SafeExitCode(proc)}");
            Crashed?.Invoke();
        };
        _proc = proc;

        var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        var sw = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                pipe.Connect(500);
                break;
            }
            catch (TimeoutException) { }
            catch (IOException) { Thread.Sleep(100); }
            if (proc.HasExited || sw.Elapsed > TimeSpan.FromSeconds(10))
            {
                pipe.Dispose();
                throw new IOException($"mpv ({Name}) bağlantısı kurulamadı");
            }
        }
        var ch = new LineChannel(pipe);
        ch.Received += OnMessage;
        ch.Start($"mpv-{Name}");
        _channel = ch;
        Log.Info($"mpv ({Name}) başladı, pid {proc.Id}, {sw.ElapsedMilliseconds} ms");
    }

    void OnMessage(JObject msg)
    {
        var ev = (string?)msg["event"];
        if (ev == "property-change")
        {
            PropertyChanged?.Invoke((string?)msg["name"] ?? "", msg["data"]);
            return;
        }
        if (ev != null)
        {
            Event?.Invoke(ev, msg);
            return;
        }
        var id = (int?)msg["request_id"];
        if (id is int rid && _pending.TryRemove(rid, out var cb))
            cb((string?)msg["error"] == "success" ? msg["data"] : null);
    }

    public void Command(params object[] args) => Send(args, null);

    /// <summary>Komutu gönderir; cevap gelince (hata ise null ile) geri çağırır.</summary>
    public void Request(Action<JToken?> callback, params object[] args) => Send(args, callback);

    public void Observe(int id, string property) => Command("observe_property", id, property);

    void Send(object[] args, Action<JToken?>? callback)
    {
        var ch = _channel;
        if (ch == null) return;
        var id = Interlocked.Increment(ref _requestId);
        if (callback != null)
        {
            _pending[id] = callback;
            if (_pending.Count > 200) _pending.Clear(); // cevapsız kalanlar birikmesin
        }
        ch.Send(new JObject { ["command"] = JArray.FromObject(args), ["request_id"] = id });
    }

    public void Stop()
    {
        _stopping = true;
        var proc = _proc;
        try { Command("quit"); } catch { }
        try
        {
            if (proc != null && !proc.WaitForExit(2000)) proc.Kill();
        }
        catch { }
        _channel?.Close();
        _channel = null;
        _proc = null;
    }

    public void Dispose() => Stop();

    static string Quote(string a) => a.IndexOfAny(new[] { ' ', '"' }) < 0 ? a : "\"" + a.Replace("\"", "\\\"") + "\"";

    static string SafeExitCode(Process p)
    {
        try { return p.ExitCode.ToString(); } catch { return "?"; }
    }

    /// <summary>Önceki bir motordan sahipsiz kalmış, bu programın mpv'lerini kapatır.</summary>
    public static void KillOrphans(string exe)
    {
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
        {
            try
            {
                if (string.Equals(p.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warn($"Sahipsiz mpv kapatılıyor, pid {p.Id}");
                    p.Kill();
                }
            }
            catch { }
            finally { p.Dispose(); }
        }
    }
}
