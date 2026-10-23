using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kmp.Core;

/// <summary>Kurumsal Müzik Net ile konuşma (BRIEF §6). KMP_SERVER ile başka sunucu (yerel deneme) seçilebilir.</summary>
public static class ServerApi
{
    public static readonly string BaseUrl =
        (Environment.GetEnvironmentVariable("KMP_SERVER") is { Length: > 0 } s ? s : "https://kurumsalmuzik.net").TrimEnd('/');

    static readonly HttpClient Http = Create();

    /// <summary>Medya (Worker) için: parçalar ~1 MB, yavaş hatta uzun sürebilir.</summary>
    public static readonly HttpClient Media = CreateMedia();

    public sealed class Result
    {
        public int Status { get; set; }
        public JObject Body { get; set; } = new();
        public string? Error => (string?)Body["error"];
    }

    static HttpClient Create()
    {
        // Windows 7/8.1'de .NET varsayılanı eski TLS olabilir; sunucu TLS 1.2 ister.
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        h.DefaultRequestHeaders.Add("X-App-Version", AppInfo.Version);
        h.DefaultRequestHeaders.Add("X-App", "kmp");
        h.DefaultRequestHeaders.UserAgent.ParseAdd($"KurumsalMuzikPlayer/{AppInfo.Version.Split('-')[0]}");
        return h;
    }

    static HttpClient CreateMedia()
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd($"KurumsalMuzikPlayer/{AppInfo.Version.Split('-')[0]}");
        return h;
    }

    /// <summary>İstek gönderir. Sunucuya hiç ulaşılamazsa HttpRequestException ya da TaskCanceledException atar.</summary>
    public static async Task<Result> SendAsync(HttpMethod method, string path, string? token = null, object? body = null)
    {
        using var req = new HttpRequestMessage(method, BaseUrl + path);
        if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) req.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        using var res = await Http.SendAsync(req).ConfigureAwait(false);
        var text = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
        JObject parsed;
        try { parsed = JObject.Parse(text); }
        catch (JsonException) { parsed = new JObject(); }
        return new Result { Status = (int)res.StatusCode, Body = parsed };
    }
}
