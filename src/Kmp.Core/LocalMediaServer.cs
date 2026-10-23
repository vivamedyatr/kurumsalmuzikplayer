using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Kmp.Core;

/// <summary>
/// Arşivdeki şarkıları mpv'ye verir (BRIEF §5): sadece bu bilgisayardan erişilen (127.0.0.1) küçük bir HTTP adresi.
/// Her şarkı bir HLS listesi; parçalar istendiği anda albüm kabından okunup bellekte çözülür, açık hâli diske yazılmaz.
/// Adreste rastgele bir oturum parçası var; başka programlar tahmin edemez.
/// </summary>
public sealed class LocalMediaServer : IDisposable
{
    readonly Library _library;
    readonly TcpListener _listener;
    readonly string _secret;
    volatile bool _stop;

    public LocalMediaServer(Library library)
    {
        _library = library;
        var b = new byte[16];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
        _secret = BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
        _listener = new TcpListener(IPAddress.Loopback, 0);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start()
    {
        _listener.Start();
        new Thread(AcceptLoop) { IsBackground = true, Name = "yerel-medya" }.Start();
        Log.Info($"Yerel medya adresi hazır (127.0.0.1:{Port})");
    }

    /// <summary>Şarkının mpv'ye verilecek adresi.</summary>
    public string TrackUrl(Manifest.Track t) => $"http://127.0.0.1:{Port}/{_secret}/{t.Id:x8}/liste.m3u8";

    void AcceptLoop()
    {
        while (!_stop)
        {
            TcpClient client;
            try { client = _listener.AcceptTcpClient(); }
            catch (Exception) { if (_stop) return; continue; }
            ThreadPool.QueueUserWorkItem(_ => Serve(client));
        }
    }

    void Serve(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 10_000;
                client.SendTimeout = 30_000;
                var stream = client.GetStream();
                // Bağlantı açık kaldıkça istekler sırayla cevaplanır (ffmpeg HLS'te bağlantıyı yeniden kullanır).
                while (!_stop)
                {
                    var request = ReadHeader(stream);
                    if (request == null) return;
                    var parts = request.Split(' ');
                    if (parts.Length < 2 || (parts[0] != "GET" && parts[0] != "HEAD")) { Respond(stream, 405, "text/plain", null, false); return; }
                    var (status, type, body) = Handle(parts[1]);
                    Respond(stream, status, type, body, parts[0] == "HEAD");
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (Exception e) { Log.Warn("Yerel medya: " + e.Message); }
        }
    }

    (int, string, byte[]?) Handle(string path)
    {
        // /<oturum>/<şarkı>/liste.m3u8  ·  /<oturum>/<şarkı>/<NNNN>.ts
        var seg = path.Split('?')[0].Trim('/').Split('/');
        if (seg.Length != 3 || seg[0] != _secret) return (404, "text/plain", null);
        if (!uint.TryParse(seg[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id)) return (404, "text/plain", null);
        var track = _library.Current?.Tracks.FirstOrDefault(t => t.Id == id);
        if (track == null) return (404, "text/plain", null);
        if (seg[2] == "liste.m3u8") return (200, "application/vnd.apple.mpegurl", Encoding.UTF8.GetBytes(Playlist(track)));
        if (seg[2].Length == 7 && seg[2].EndsWith(".ts") && int.TryParse(seg[2].Substring(0, 4), out var i) && i < track.Segments.Count)
        {
            var data = _library.ReadSegment(track, i);
            return data == null ? (404, "text/plain", null) : (200, "video/mp2t", data);
        }
        return (404, "text/plain", null);
    }

    static string Playlist(Manifest.Track t)
    {
        var sb = new StringBuilder();
        sb.Append("#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-PLAYLIST-TYPE:VOD\n#EXT-X-MEDIA-SEQUENCE:0\n");
        sb.Append("#EXT-X-TARGETDURATION:").Append(Math.Ceiling(t.Segments.DefaultIfEmpty(10).Max()).ToString(CultureInfo.InvariantCulture)).Append('\n');
        for (var i = 0; i < t.Segments.Count; i++)
            sb.Append("#EXTINF:").Append(t.Segments[i].ToString("0.000", CultureInfo.InvariantCulture)).Append(",\n").Append($"{i:0000}.ts\n");
        sb.Append("#EXT-X-ENDLIST\n");
        return sb.ToString();
    }

    static string? ReadHeader(Stream s)
    {
        // İstek başlığı boş satırla ("\r\n\r\n") biter; gövde yok (sadece GET/HEAD).
        var buf = new StringBuilder();
        while (true)
        {
            if (buf.Length >= 8192) return null;
            var b = s.ReadByte();
            if (b < 0) return null;
            buf.Append((char)b);
            var n = buf.Length;
            if (n >= 4 && buf[n - 4] == '\r' && buf[n - 3] == '\n' && buf[n - 2] == '\r' && buf[n - 1] == '\n') break;
        }
        var text = buf.ToString();
        var end = text.IndexOf("\r\n", StringComparison.Ordinal);
        return end > 0 ? text.Substring(0, end) : null;
    }

    static void Respond(Stream s, int status, string type, byte[]? body, bool headOnly)
    {
        var reason = status switch { 200 => "OK", 404 => "Not Found", _ => "Method Not Allowed" };
        var head = $"HTTP/1.1 {status} {reason}\r\nContent-Type: {type}\r\nContent-Length: {body?.Length ?? 0}\r\nCache-Control: no-store\r\nConnection: keep-alive\r\n\r\n";
        var h = Encoding.ASCII.GetBytes(head);
        s.Write(h, 0, h.Length);
        if (body != null && !headOnly) s.Write(body, 0, body.Length);
        s.Flush();
    }

    public void Dispose()
    {
        _stop = true;
        try { _listener.Stop(); } catch { }
    }
}
