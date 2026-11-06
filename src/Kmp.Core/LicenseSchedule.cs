using System;

namespace Kmp.Core;

/// <summary>
/// Lisans (durum) sorgusunun ne zaman yapılacağı (BRIEF §2.1d, karar Onur 7 Kas 2026). Kural tek yerde ve saf:
/// - Programın bu açılışında henüz başarılı sorgu yoksa ya da son sorgu sunucuya ulaşamadıysa: hemen (ulaşamadıysa
///   zamanlayıcının her turunda, 15 dk'da bir yeniden).
/// - Yayın çalarken: günde bir.
/// - Yayın hizmet süresi bittiği için durmuşken: 30 dk'da bir (ödeme ya da uzatma en geç 30 dk'da yansısın).
/// - Durmasının üstünden 7 gün geçtiyse: hiç; sadece program açılınca bir kez.
/// Bütün zamanlar sunucu saatine göre düzeltilmiş Unix ms (<see cref="Clock.Now"/>).
/// </summary>
public static class LicenseSchedule
{
    public static readonly TimeSpan PlayingEvery = TimeSpan.FromHours(24);
    public static readonly TimeSpan StoppedEvery = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan StoppedFor = TimeSpan.FromDays(7);

    // Zamanlayıcı sabit aralıkla döner; tur, aralığın birkaç saniye öncesine denk gelirse bir tur kaçmasın.
    static readonly TimeSpan TickSlack = TimeSpan.FromMinutes(1);

    /// <param name="now">Şimdi.</param>
    /// <param name="serviceEndsAt">Yayının durduğu an (tolerans dahil, sunucudan); bilinmiyorsa null.</param>
    /// <param name="lastOk">Bu açılıştaki son başarılı sorgu; yoksa null.</param>
    /// <param name="online">Son sorgu sunucuya ulaştı mı.</param>
    public static bool Due(long now, long? serviceEndsAt, long? lastOk, bool online)
    {
        if (lastOk is not long last || !online) return true;
        var since = TimeSpan.FromMilliseconds(now - last);
        if (serviceEndsAt is long end && now >= end)
        {
            if (TimeSpan.FromMilliseconds(now - end) >= StoppedFor) return false;
            return since >= StoppedEvery - TickSlack;
        }
        return since >= PlayingEvery - TickSlack;
    }
}
