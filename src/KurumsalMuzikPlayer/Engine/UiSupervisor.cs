using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Kmp.Core;
using Newtonsoft.Json.Linq;

namespace KurumsalMuzikPlayer.Engine;

/// <summary>
/// Pencere sürecini açar ve izler (BRIEF §4). Motor 2 sn'de bir "ping" gönderir; pencere cevabı kendi arayüz
/// iş parçacığından verir. 15 sn cevap gelmezse pencere donmuş sayılır: kapatılıp yeniden açılır (müzik etkilenmez).
/// </summary>
sealed class UiSupervisor
{
    const int PingEveryMs = 2000;
    const int HangAfterMs = 15_000;
    const int SlowPongMs = 2000;

    readonly string _exe;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly object _gate = new();
    LineChannel? _ui;
    int _uiPid;
    long _lastPongMs;
    long _pingSentMs;
    int _pingN;
    bool _uiMinimized;
    long _launchedAtMs = -60_000;
    volatile bool _closing;

    public event Action<JObject>? CommandReceived;
    public event Action? ShowRequested;
    public event Action? QuitRequested;

    public int Restarts { get; private set; }
    public long MaxPongMs { get; private set; }

    public UiSupervisor(string exe) => _exe = exe;

    public void Start()
    {
        new Thread(AcceptLoop) { IsBackground = true, Name = "pencere-boru" }.Start();
        new Thread(WatchLoop) { IsBackground = true, Name = "pencere-bekci" }.Start();
    }

    void AcceptLoop()
    {
        while (!_closing)
        {
            NamedPipeServerStream server;
            try
            {
                server = new NamedPipeServerStream(Ipc.EnginePipe, PipeDirection.InOut, 4, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            }
            catch (IOException e)
            {
                Log.Warn($"Pencere borusu açılamadı: {e.Message}");
                Thread.Sleep(1000);
                continue;
            }
            try
            {
                server.WaitForConnection();
            }
            catch (Exception e)
            {
                Log.Warn($"Pencere bağlantısı beklenirken: {e.Message}");
                server.Dispose();
                continue;
            }
            var ch = new LineChannel(server);
            ch.Received += m => OnMessage(ch, m);
            ch.Start("pencere-baglanti");
        }
    }

    void OnMessage(LineChannel ch, JObject m)
    {
        switch ((string?)m["t"])
        {
            case "hello" when (string?)m["role"] == "show":
                ch.Close();
                ShowRequested?.Invoke();
                break;
            case "hello" when (string?)m["role"] == "quit":
                ch.Close();
                QuitRequested?.Invoke();
                break;
            case "hello" when (string?)m["role"] == "ui":
                LineChannel? old;
                lock (_gate)
                {
                    old = _ui;
                    _ui = ch;
                    _uiPid = (int?)m["pid"] ?? 0;
                    _lastPongMs = _clock.ElapsedMilliseconds;
                    // Açılan pencere bağlandı: "ikinci kopya açma" beklemesi biter (kapatılırsa hemen yeniden açılabilsin).
                    _launchedAtMs = -60_000;
                }
                if (old != null && old != ch) old.Close();
                ch.Closed += () =>
                {
                    lock (_gate)
                    {
                        if (_ui != ch) return;
                        _ui = null;
                    }
                    Log.Info("Pencere kapandı");
                };
                Log.Info($"Pencere bağlandı, pid {_uiPid}");
                break;
            case "pong":
                lock (_gate)
                {
                    if (ch != _ui) return;
                    var now = _clock.ElapsedMilliseconds;
                    _lastPongMs = now;
                    _uiMinimized = (bool?)m["min"] ?? false;
                    if ((int?)m["n"] == _pingN)
                    {
                        var latency = now - _pingSentMs;
                        if (latency > MaxPongMs) MaxPongMs = latency;
                        if (latency > SlowPongMs) Log.Warn($"Pencere {latency} ms geç yanıt verdi");
                    }
                }
                break;
            default:
                CommandReceived?.Invoke(m);
                break;
        }
    }

    void WatchLoop()
    {
        while (!_closing)
        {
            Thread.Sleep(PingEveryMs);
            LineChannel? ui;
            int pid;
            bool minimized;
            lock (_gate)
            {
                ui = _ui;
                pid = _uiPid;
                minimized = _uiMinimized;
                if (ui == null) continue;
                var silent = _clock.ElapsedMilliseconds - _lastPongMs;
                if (silent <= HangAfterMs)
                {
                    _pingN++;
                    _pingSentMs = _clock.ElapsedMilliseconds;
                    ui.Send(new JObject { ["t"] = "ping", ["n"] = _pingN });
                    continue;
                }
                _ui = null;
            }
            Restarts++;
            Log.Warn($"Pencere {HangAfterMs / 1000} sn'dir yanıt vermiyor (pid {pid}); kapatılıp yeniden açılıyor");
            ui.Close();
            try
            {
                using var p = Process.GetProcessById(pid);
                p.Kill();
                p.WaitForExit(5000);
            }
            catch (Exception e)
            {
                Log.Warn($"Donan pencere kapatılamadı: {e.Message}");
            }
            Launch(minimized);
        }
    }

    /// <summary>Pencere açıksa öne getirir, değilse açar.</summary>
    public void Show(bool minimized = false)
    {
        LineChannel? ui;
        lock (_gate) ui = _ui;
        if (ui != null && ui.Send(new JObject { ["t"] = "show" })) return;
        lock (_gate)
        {
            // Az önce açıldıysa (henüz bağlanmadıysa) ikinci kopya açma.
            if (_clock.ElapsedMilliseconds - _launchedAtMs < 10_000) return;
        }
        Launch(minimized);
    }

    void Launch(bool minimized)
    {
        lock (_gate) _launchedAtMs = _clock.ElapsedMilliseconds;
        try
        {
            Process.Start(new ProcessStartInfo(_exe, minimized ? "--ui --gizli" : "--ui") { UseShellExecute = false })?.Dispose();
        }
        catch (Exception e)
        {
            Log.Error("Pencere açılamadı", e);
        }
    }

    public void Send(JObject msg) => SendState(msg);

    public void SendState(JObject state)
    {
        LineChannel? ui;
        lock (_gate) ui = _ui;
        ui?.Send(state);
    }

    /// <summary>Motor kapanırken pencereyi de kapatır (pencere motoru yeniden başlatmaya çalışmasın).</summary>
    public void CloseUi()
    {
        _closing = true;
        LineChannel? ui;
        lock (_gate) ui = _ui;
        if (ui == null) return;
        ui.Send(new JObject { ["t"] = "close" });
        Thread.Sleep(300);
        ui.Close();
    }
}
