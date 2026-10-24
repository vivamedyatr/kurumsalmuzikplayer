using System;
using System.IO;

namespace Kmp.Core;

/// <summary>
/// Saat ve çalma izni (BRIEF §2.2): akış ve anonslar İstanbul saatiyle; bilgisayar saati her sunucu bağlantısında
/// düzeltilir. durum.json: son başarılı bağlantı ve hizmet son günü, program kapanıp açılsa da bilinir (7 gün kuralı).
/// Türkiye 2016'dan beri sürekli UTC+3: Windows'un saat dilimi kayıtları eski olsa da doğru olsun diye sabit.
/// </summary>
public static class Clock
{
    public const int OfflineDays = 7;
    const long DayMs = 86_400_000;
    static readonly TimeSpan IstanbulOffset = TimeSpan.FromHours(3);
    static readonly object Gate = new();
    static State _s = JsonFile.Read<State>(FilePath) ?? new State();

    sealed class State
    {
        public long Offset { get; set; }
        public long? LastOkAt { get; set; }
        public long? ServiceEndsAt { get; set; }
    }

    static string FilePath => Path.Combine(AppPaths.DataDir, "durum.json");

    static long LocalMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Sunucu saatine göre düzeltilmiş "şimdi" (Unix ms).</summary>
    public static long Now()
    {
        lock (Gate) return LocalMs() + _s.Offset;
    }

    /// <summary>Sunucuya ulaşıldı: saat farkı, son bağlantı ve (varsa) hizmet son günü kaydedilir.</summary>
    public static void ServerSeen(long? serverNow, long? serviceEndsAt = null)
    {
        lock (Gate)
        {
            if (serverNow is long sn and > 0)
            {
                var offset = sn - LocalMs();
                if (Math.Abs(offset - _s.Offset) > 120_000) Log.Warn($"Bilgisayar saati sunucudan farklı: {offset / 60_000} dk");
                _s.Offset = offset;
            }
            _s.LastOkAt = LocalMs();
            if (serviceEndsAt != null) _s.ServiceEndsAt = serviceEndsAt;
            JsonFile.Write(FilePath, _s);
        }
    }

    /// <summary>Çıkış/düşme: son bağlantı unutulur (yeni giriş internet ister).</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            _s = new State { Offset = _s.Offset };
            JsonFile.Write(FilePath, _s);
        }
    }

    public static long? LastOkAt
    {
        get { lock (Gate) return _s.LastOkAt; }
    }

    /// <summary>İnternetsiz kalan gün hakkı (tam gün).</summary>
    public static int OfflineDaysLeft()
    {
        lock (Gate)
        {
            if (_s.LastOkAt is not long last) return 0;
            return (int)Math.Max(0, OfflineDays - (LocalMs() - last) / DayMs);
        }
    }

    /// <summary>Çalınabilir mi? Hizmet süresi bittiyse ya da 7 gündür sunucuya ulaşılamadıysa hayır; reason Türkçe.</summary>
    public static bool PlayGate(out string? reason)
    {
        lock (Gate)
        {
            reason = null;
            if (_s.ServiceEndsAt is long end && LocalMs() + _s.Offset >= end)
                reason = "Hizmet süreniz doldu. Uzatmak için Viva Medya ile iletişime geçin.";
            else if (_s.LastOkAt is not long last)
                reason = "İlk açılış için internet bağlantısı gerekli.";
            else if (LocalMs() - last >= OfflineDays * DayMs)
                reason = "7 gündür internete bağlanılamadı. Müziğin devam etmesi için bilgisayarı internete bağlayın.";
            return reason == null;
        }
    }

    /// <summary>İstanbul saati (sunucuya göre düzeltilmiş).</summary>
    public static DateTimeOffset Istanbul(long? ms = null) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ms ?? Now()).ToOffset(IstanbulOffset);

    /// <summary>Gün içi dakika (0–1439), İstanbul.</summary>
    public static int DayMinute(DateTimeOffset t) => t.Hour * 60 + t.Minute;
}
