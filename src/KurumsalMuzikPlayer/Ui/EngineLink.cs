using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using Kmp.Core;
using Newtonsoft.Json.Linq;

namespace KurumsalMuzikPlayer.Ui;

/// <summary>
/// Pencerenin motorla bağlantısı. Bağlantı koparsa yeniden dener; motor 10 sn'den uzun yoksa onu başlatır
/// (motor ile pencere birbirinin bekçisi; BRIEF §4). Olaylar bağlantı iş parçacığında gelir.
/// </summary>
sealed class EngineLink
{
    const int StartEngineAfterMs = 10_000;
    const int EngineStartCooldownMs = 30_000;

    readonly Stopwatch _clock = Stopwatch.StartNew();
    LineChannel? _channel;
    volatile bool _closing;
    long _lastEngineStartMs = -EngineStartCooldownMs;

    public event Action<JObject>? MessageReceived;
    public event Action<bool>? ConnectionChanged;

    public void Start() => new Thread(Loop) { IsBackground = true, Name = "motor-baglanti" }.Start();

    void Loop()
    {
        var lostAt = _clock.ElapsedMilliseconds;
        while (!_closing)
        {
            var pipe = new NamedPipeClientStream(".", Ipc.EnginePipe, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                pipe.Connect(2000);
            }
            catch (Exception)
            {
                pipe.Dispose();
                var now = _clock.ElapsedMilliseconds;
                if (now - lostAt > StartEngineAfterMs && now - _lastEngineStartMs > EngineStartCooldownMs && !_closing)
                {
                    _lastEngineStartMs = now;
                    Log.Warn("Motor çalışmıyor; başlatılıyor");
                    try
                    {
                        var exe = Process.GetCurrentProcess().MainModule!.FileName;
                        Process.Start(new ProcessStartInfo(exe, "--ui-acik") { UseShellExecute = false })?.Dispose();
                    }
                    catch (Exception e)
                    {
                        Log.Error("Motor başlatılamadı", e);
                    }
                }
                Thread.Sleep(1000);
                continue;
            }
            var ch = new LineChannel(pipe);
            var closed = new ManualResetEventSlim();
            ch.Received += m => MessageReceived?.Invoke(m);
            ch.Closed += closed.Set;
            ch.Start("motor");
            ch.Send(new JObject { ["t"] = "hello", ["role"] = "ui", ["pid"] = Process.GetCurrentProcess().Id });
            _channel = ch;
            Log.Info("Motora bağlandı");
            ConnectionChanged?.Invoke(true);
            closed.Wait();
            _channel = null;
            lostAt = _clock.ElapsedMilliseconds;
            if (!_closing)
            {
                Log.Warn("Motor bağlantısı koptu");
                ConnectionChanged?.Invoke(false);
            }
        }
    }

    public void Send(JObject msg) => _channel?.Send(msg);

    /// <summary>Motor "kapan" dedi ya da pencere kapanıyor: motoru yeniden başlatma.</summary>
    public void Close()
    {
        _closing = true;
        _channel?.Close();
    }
}
