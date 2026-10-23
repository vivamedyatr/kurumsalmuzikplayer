using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Kmp.Core;

/// <summary>
/// Faz 0 dayanıklılık deneyi (BRIEF §8): motorun içinde, gerçek indirme gibi arşiv yazar ve geri okur.
///   Container — 1.0 yöntemi: sunucudan şifreli gelmiş gibi rastgele baytlar, albüm başına tek kap dosyası.
///   Files     — 2.x yöntemi: her parça bilgisayarda AES ile şifrelenip ayrı dosyaya yazılır (.vmp.part → .vmp).
/// Karşılaştırma için ikisi de var; antivirüsün hangisine tepki verdiği görülür.
/// </summary>
public sealed class ArchiveExperiment
{
    public enum Pattern { Container, Files }

    const int SegmentBytes = 200 * 1024; // ~10 sn, 160 kbps
    const int SegmentsPerTrack = 21; // ~3,5 dk
    const int TracksPerAlbum = 40;

    readonly string _dir;
    readonly long _target;
    readonly double _mbps;
    readonly object _statsGate = new();
    readonly Stopwatch _clock = new();
    readonly List<AlbumContainer> _albums = new();
    readonly List<string> _files = new();
    readonly Random _rng = new();
    Thread? _thread;
    volatile bool _stop;
    byte[]? _filesKey;

    public Pattern Mode { get; }
    public long Written { get; private set; }
    public int AlbumCount { get; private set; }
    public int FileCount { get; private set; }
    public long Reads { get; private set; }
    public int ReadErrors { get; private set; }
    public double MaxWriteMs { get; private set; }
    public int SlowWrites { get; private set; }
    public bool Done { get; private set; }
    public string? Error { get; private set; }

    public ArchiveExperiment(string dir, double gigabytes, Pattern pattern, double mbps)
    {
        _dir = dir;
        _target = (long)(gigabytes * 1024 * 1024 * 1024);
        _mbps = mbps;
        Mode = pattern;
    }

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "deneme-arsiv", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
        _thread?.Join(3000);
        foreach (var a in _albums) a.Dispose();
    }

    void Run()
    {
        try
        {
            Log.Info($"Dayanıklılık deneyi başlıyor: {ModeName}, hedef {_target / 1073741824.0:0.#} GB, hız {(_mbps > 0 ? _mbps + " Mbps" : "sınırsız")}, klasör {_dir}");
            if (Directory.Exists(_dir))
            {
                Log.Info("Önceki deneme arşivi siliniyor");
                Directory.Delete(_dir, true);
            }
            Directory.CreateDirectory(_dir);
            _clock.Start();
            var data = new byte[SegmentBytes];
            var album = 0;
            while (!_stop && Written < _target)
            {
                album++;
                var container = Mode == Pattern.Container ? AlbumContainer.Open(Path.Combine(_dir, $"album-{album:0000}.kmp")) : null;
                if (container != null) lock (_statsGate) _albums.Add(container);
                for (var t = 0; t < TracksPerAlbum && !_stop && Written < _target; t++)
                    for (var s = 0; s < SegmentsPerTrack && !_stop && Written < _target; s++)
                    {
                        _rng.NextBytes(data);
                        var sw = Stopwatch.StartNew();
                        if (container != null) container.Append(t, s, data, data.Length);
                        else WriteFilePattern(album, t, s, data);
                        sw.Stop();
                        lock (_statsGate)
                        {
                            Written += data.Length;
                            if (sw.Elapsed.TotalMilliseconds > MaxWriteMs) MaxWriteMs = sw.Elapsed.TotalMilliseconds;
                            if (sw.Elapsed.TotalMilliseconds > 1000)
                            {
                                SlowWrites++;
                                Log.Warn($"Deneme: bir parçanın yazılması {sw.ElapsedMilliseconds} ms sürdü");
                            }
                        }
                        if ((Written / SegmentBytes) % 25 == 0) ReadBackRandom();
                        Throttle();
                    }
                container?.CloseWriter();
                AlbumCount = album;
            }
            Done = !_stop;
            Log.Info("Dayanıklılık deneyi yazmayı bitirdi: " + Summary());
            // Yazma bitti; çalma gibi saniyede bir okumaya devam et.
            while (!_stop)
            {
                ReadBackRandom();
                Thread.Sleep(1000);
            }
        }
        catch (Exception e)
        {
            Error = e.Message;
            Log.Error("Dayanıklılık deneyi durdu", e);
        }
    }

    void Throttle()
    {
        if (_mbps <= 0) return;
        var due = Written / (_mbps * 125_000.0);
        var ahead = due - _clock.Elapsed.TotalSeconds;
        if (ahead > 0.005) Thread.Sleep(TimeSpan.FromSeconds(ahead));
    }

    void WriteFilePattern(int album, int track, int segment, byte[] plain)
    {
        _filesKey ??= RandomBytes(32);
        string name;
        using (var sha = SHA256.Create())
            name = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes($"{album}/{track}/{segment}"))).Replace("-", "").Substring(0, 32).ToLowerInvariant();
        var dir = Path.Combine(_dir, name.Substring(0, 2));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name + ".vmp");
        using (var aes = Aes.Create())
        {
            aes.Key = _filesKey;
            aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            var body = enc.TransformFinalBlock(plain, 0, plain.Length);
            using (var fs = new FileStream(path + ".part", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(aes.IV, 0, aes.IV.Length);
                fs.Write(body, 0, body.Length);
                fs.Flush(true);
            }
        }
        if (File.Exists(path)) File.Delete(path);
        File.Move(path + ".part", path);
        lock (_statsGate)
        {
            _files.Add(path);
            FileCount = _files.Count;
        }
    }

    void ReadBackRandom()
    {
        try
        {
            if (Mode == Pattern.Container)
            {
                AlbumContainer a;
                lock (_statsGate)
                {
                    if (_albums.Count == 0) return;
                    a = _albums[_rng.Next(_albums.Count)];
                }
                if (a.Count == 0) return;
                var t = _rng.Next(TracksPerAlbum);
                var s = _rng.Next(SegmentsPerTrack);
                if (!a.Contains(t, s)) return;
                var ok = a.TryRead(t, s) != null;
                lock (_statsGate)
                {
                    Reads++;
                    if (!ok) ReadErrors++;
                }
            }
            else
            {
                string path;
                lock (_statsGate)
                {
                    if (_files.Count == 0) return;
                    path = _files[_rng.Next(_files.Count)];
                }
                var raw = File.ReadAllBytes(path);
                using var aes = Aes.Create();
                aes.Key = _filesKey!;
                var iv = new byte[16];
                Buffer.BlockCopy(raw, 0, iv, 0, 16);
                aes.IV = iv;
                using var dec = aes.CreateDecryptor();
                var plain = dec.TransformFinalBlock(raw, 16, raw.Length - 16);
                lock (_statsGate)
                {
                    Reads++;
                    if (plain.Length != SegmentBytes) ReadErrors++;
                }
            }
        }
        catch (Exception e)
        {
            lock (_statsGate) ReadErrors++;
            Log.Warn($"Deneme okuma hatası: {e.Message}");
        }
    }

    string ModeName => Mode == Pattern.Container ? "albüm kabı (1.0)" : "tek tek şifreli dosya (2.x)";

    public string Summary()
    {
        lock (_statsGate)
        {
            var mins = _clock.Elapsed.TotalMinutes;
            var count = Mode == Pattern.Container ? $"{AlbumCount} albüm dosyası" : $"{FileCount} dosya";
            return $"{ModeName}: {Written / 1073741824.0:0.00} / {_target / 1073741824.0:0.#} GB, {count}, {mins:0} dk, "
                 + $"en yavaş yazma {MaxWriteMs:0} ms, 1 sn'den uzun yazma {SlowWrites}, okuma {Reads} (hata {ReadErrors})"
                 + (Error != null ? $", HATA: {Error}" : "");
        }
    }

    static byte[] RandomBytes(int n)
    {
        var b = new byte[n];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(b);
        return b;
    }
}
