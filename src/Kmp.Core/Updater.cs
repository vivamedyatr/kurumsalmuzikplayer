using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Kmp.Core;

/// <summary>
/// Kendini güncelleme (BRIEF §2.2, §7.3): kurumsalmuzik.net/indir/kmp/latest.json 4 saatte bir sorulur. Yeni sürüm varsa
/// kurulum dosyası arkada iner ve SHA-256 ile doğrulanır; **sadece program yeniden açılınca** (müzik başlamadan) kurulur,
/// çalarken asla. 2.x'in /indir/latest.yml'ine dokunulmaz (o ayrı).
/// latest.json: { "version": "1.0.0-faz4", "build": 40, "file": "Kurumsal-Muzik-Player-Kurulum-1.0.0-faz4.exe", "sha256": "…", "size": 27000000 }
/// </summary>
public static class Updater
{
    const int CheckEveryMs = 4 * 60 * 60_000;
    const int MaxAttempts = 3;
    static readonly Regex SafeFile = new(@"^Kurumsal-Muzik-Player-Kurulum-[0-9A-Za-z.\-]+\.exe$");
    static Timer? _timer;
    static int _checking;

    static string FeedUrl =>
        Environment.GetEnvironmentVariable("KMP_GUNCELLEME_URL") is { Length: > 0 } u ? u : ServerApi.BaseUrl + "/indir/kmp/latest.json";
    static string Dir => Path.Combine(AppPaths.DataDir, "guncelleme");
    static string PendingFile => Path.Combine(Dir, "bekleyen.json");

    sealed class Pending
    {
        public string Version { get; set; } = "";
        public int Build { get; set; }
        public string File { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public int Attempts { get; set; }
    }

    /// <summary>
    /// Açılışta, her şeyden önce: inmiş ve doğrulanmış yeni sürüm varsa kurulumu başlatır ve true döner (motor hemen
    /// kapanmalı; kurulum bitince program kendiliğinden yeniden açılır).
    /// </summary>
    public static bool TryInstallPending()
    {
        try
        {
            var p = JsonFile.Read<Pending>(PendingFile);
            if (p == null) return false;
            var file = Path.Combine(Dir, p.File);
            if (p.Build <= AppInfo.Build || !SafeFile.IsMatch(p.File) || !File.Exists(file))
            {
                Cleanup();
                return false;
            }
            if (p.Attempts >= MaxAttempts)
            {
                Log.Warn($"Güncelleme {p.Version} {MaxAttempts} kez kurulamadı; atlanıyor");
                return false;
            }
            if (!string.Equals(Sha256(file), p.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                Log.Warn("Güncelleme dosyası doğrulanamadı; silindi");
                Cleanup();
                return false;
            }
            p.Attempts++;
            JsonFile.Write(PendingFile, p);
            Log.Info($"Güncelleme kuruluyor: {AppInfo.Version} → {p.Version}");
            Process.Start(new ProcessStartInfo(file, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /guncelleme=1") { UseShellExecute = false })?.Dispose();
            return true;
        }
        catch (Exception e)
        {
            Log.Error("Güncelleme başlatılamadı", e);
            return false;
        }
    }

    public static void Start()
    {
        _timer?.Dispose();
        _timer = new Timer(_ => Check(), null, 2 * 60_000, CheckEveryMs);
    }

    public static void Stop() => _timer?.Dispose();

    /// <summary>Yeni sürüm var mı; varsa indirip doğrular. Bilgi amaçlı: hata yayını etkilemez.</summary>
    public static void Check()
    {
        if (Interlocked.Exchange(ref _checking, 1) == 1) return;
        try
        {
            var text = ServerApi.Media.GetStringAsync(FeedUrl + "?t=" + DateTime.UtcNow.Ticks).GetAwaiter().GetResult();
            var j = JObject.Parse(text);
            var build = (int?)j["build"] ?? 0;
            var version = (string?)j["version"] ?? "";
            var fileName = (string?)j["file"] ?? "";
            var sha = ((string?)j["sha256"] ?? "").ToLowerInvariant();
            if (build <= AppInfo.Build) return;
            if (!SafeFile.IsMatch(fileName) || sha.Length != 64) throw new InvalidDataException("latest.json geçersiz");
            var current = JsonFile.Read<Pending>(PendingFile);
            if (current?.Build == build && File.Exists(Path.Combine(Dir, current.File))) return;

            Log.Info($"Yeni sürüm bulundu: {version}; arkada iniyor");
            Cleanup();
            Directory.CreateDirectory(Dir);
            var target = Path.Combine(Dir, fileName);
            var url = FeedUrl.Substring(0, FeedUrl.LastIndexOf('/') + 1) + Uri.EscapeDataString(fileName);
            using (var res = ServerApi.Media.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
            {
                res.EnsureSuccessStatusCode();
                using var input = res.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                using var output = File.Create(target + ".part");
                input.CopyTo(output);
            }
            if (!string.Equals(Sha256(target + ".part"), sha, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(target + ".part");
                throw new InvalidDataException("indirilen dosyanın SHA-256'sı tutmuyor");
            }
            File.Move(target + ".part", target);
            JsonFile.Write(PendingFile, new Pending { Version = version, Build = build, File = fileName, Sha256 = sha });
            Log.Info($"Güncelleme hazır: {version}; program yeniden açılınca kurulacak");
        }
        catch (Exception e)
        {
            Log.Warn("Güncelleme denetimi: " + (e.InnerException?.Message ?? e.Message));
        }
        finally
        {
            Interlocked.Exchange(ref _checking, 0);
        }
    }

    /// <summary>Ekranda gösterilecek: inmiş, kurulmayı bekleyen sürüm (yoksa null).</summary>
    public static string? ReadyVersion()
    {
        var p = JsonFile.Read<Pending>(PendingFile);
        return p != null && p.Build > AppInfo.Build && File.Exists(Path.Combine(Dir, p.File)) ? p.Version : null;
    }

    static void Cleanup()
    {
        try
        {
            if (Directory.Exists(Dir))
                foreach (var f in Directory.GetFiles(Dir)) File.Delete(f);
        }
        catch { }
    }

    static string Sha256(string file)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(file);
        return string.Concat(sha.ComputeHash(fs).Select(b => b.ToString("x2")));
    }
}
