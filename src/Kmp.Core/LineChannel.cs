using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kmp.Core;

/// <summary>
/// Boru üzerinden satır satır JSON (motor ↔ pencere, motor ↔ mpv). Okuma ve yazma kendi iş parçacıklarında:
/// gönderen asla beklemez (karşı taraf takılsa bile), kuyruk dolarsa ileti atılır. Boru PipeOptions.Asynchronous ile
/// açılmalı; yoksa Windows aynı boruda okuma bitene kadar yazmayı bekletir.
/// </summary>
public sealed class LineChannel : IDisposable
{
    const int MaxQueue = 256;
    static readonly UTF8Encoding Utf8 = new(false);
    readonly PipeStream _pipe;
    readonly BlockingCollection<string> _outbox = new(new ConcurrentQueue<string>());
    int _closed;

    public event Action<JObject>? Received;
    public event Action? Closed;

    public LineChannel(PipeStream pipe) => _pipe = pipe;

    public bool IsOpen => _closed == 0;

    /// <summary>Okuma ve yazmayı başlatır. Received olayı okuma iş parçacığında gelir.</summary>
    public void Start(string name)
    {
        new Thread(ReadLoop) { IsBackground = true, Name = name + " okuma" }.Start();
        new Thread(WriteLoop) { IsBackground = true, Name = name + " yazma" }.Start();
    }

    void ReadLoop()
    {
        try
        {
            using var reader = new StreamReader(_pipe, Utf8, false, 4096, true);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                JObject msg;
                try { msg = JObject.Parse(line); }
                catch (JsonException) { continue; }
                Received?.Invoke(msg);
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (Exception e) { Log.Error("Boru okuma", e); }
        Close();
    }

    void WriteLoop()
    {
        try
        {
            var writer = new StreamWriter(_pipe, Utf8, 4096, true) { NewLine = "\n", AutoFlush = true };
            foreach (var line in _outbox.GetConsumingEnumerable()) writer.WriteLine(line);
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { } // karşı taraf kapandı/öldürüldü
        catch (InvalidOperationException) { }
        catch (Exception e) { Log.Error("Boru yazma", e); }
        Close();
    }

    /// <summary>İletiyi kuyruğa koyar; beklemez.</summary>
    public bool Send(JObject msg)
    {
        if (_closed != 0 || _outbox.Count >= MaxQueue) return false;
        try
        {
            _outbox.Add(msg.ToString(Formatting.None));
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        try { _outbox.CompleteAdding(); } catch { }
        try { _pipe.Dispose(); } catch { }
        Closed?.Invoke();
    }

    public void Dispose() => Close();
}
