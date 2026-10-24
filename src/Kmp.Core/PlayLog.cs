using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;

namespace Kmp.Core;

/// <summary>
/// Yayın kaydı (web BRIEF §14): çalan şarkılar ve yayının açıldığı/durduğu anlar bilgisayarda birikir, 15 dk'da bir
/// /api/offline/log'a toplu gider. İnternet yoksa bekler (7 güne kadar; sunucu daha eskisini almaz). Anonslar burada
/// değil: anons çalma bildirimiyle sunucuda kayda yazılır. Kayıt sadece bilgi; hatası çalmayı asla etkilemez.
/// </summary>
public static class PlayLog
{
    const long KeepMs = 7 * 86_400_000L;
    const int Max = 5000;
    const int Batch = 2000;
    const int SendEveryMs = 15 * 60_000;
    static readonly object Gate = new();
    static List<Event> _queue = JsonFile.Read<List<Event>>(FilePath) ?? new List<Event>();
    static string? _lastState;
    static Timer? _saveTimer;
    static Timer? _sendTimer;
    static Func<string?>? _token;
    static int _sending;

    public sealed class Event
    {
        [JsonProperty("t")] public long T { get; set; }
        [JsonProperty("k")] public string K { get; set; } = "";
        [JsonProperty("s", NullValueHandling = NullValueHandling.Ignore)] public string? S { get; set; }
        [JsonProperty("a", NullValueHandling = NullValueHandling.Ignore)] public string? A { get; set; }
        [JsonProperty("g", NullValueHandling = NullValueHandling.Ignore)] public string? G { get; set; }
    }

    static string FilePath => Path.Combine(AppPaths.DataDir, "kayit-kuyrugu.json");

    static string? Clip(string? s, int n) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim().Length > n ? s.Trim().Substring(0, n) : s.Trim();

    static void Add(Event e)
    {
        lock (Gate)
        {
            _queue.Add(e);
            var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - KeepMs;
            if (_queue.Count > Max || _queue[0].T < cutoff) _queue = _queue.Where(x => x.T >= cutoff).Skip(Math.Max(0, _queue.Count - Max)).ToList();
            _saveTimer ??= new Timer(_ => SaveNow(), null, 5000, Timeout.Infinite);
        }
    }

    public static void Song(string title, string album, string genre)
    {
        var s = Clip(title, 300);
        if (s != null) Add(new Event { T = Clock.Now(), K = "m", S = s, A = Clip(album, 300), G = Clip(genre, 200) });
    }

    /// <summary>Yayın açıldı / durdu. Aynı durum art arda yazılmaz.</summary>
    public static void State(bool on)
    {
        var k = on ? "on" : "off";
        lock (Gate)
        {
            if (_lastState == k) return;
            _lastState = k;
        }
        Add(new Event { T = Clock.Now(), K = k });
    }

    public static void SaveNow()
    {
        List<Event> copy;
        lock (Gate)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
            copy = _queue.ToList();
        }
        JsonFile.Write(FilePath, copy);
    }

    public static void StartSending(Func<string?> token)
    {
        _token = token;
        _sendTimer?.Dispose();
        _sendTimer = new Timer(_ => Flush(), null, 30_000, SendEveryMs);
    }

    public static void StopSending()
    {
        _sendTimer?.Dispose();
        _sendTimer = null;
    }

    public static void Flush()
    {
        var token = _token?.Invoke();
        if (token == null || Interlocked.Exchange(ref _sending, 1) == 1) return;
        try
        {
            List<Event> batch;
            lock (Gate) batch = _queue.Take(Batch).ToList();
            if (batch.Count == 0) return;
            var r = ServerApi.SendAsync(HttpMethod.Post, "/api/offline/log", token, new { events = batch }).GetAwaiter().GetResult();
            // 400: bozuk kayıt (kuyruk tıkanmasın), 401: cihaz silindi (kayıt anlamsız; girişe dönüşü oturum yapar).
            if (r.Status is 200 or 400 or 401)
            {
                lock (Gate) _queue.RemoveRange(0, Math.Min(batch.Count, _queue.Count));
                SaveNow();
                if (r.Status == 400) Log.Warn("Yayın kaydı reddedildi (bozuk kayıt atlandı)");
            }
        }
        catch
        {
            // İnternet yok: kayıtlar bekler.
        }
        finally
        {
            Interlocked.Exchange(ref _sending, 0);
        }
    }
}
