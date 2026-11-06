using Kmp.Core;
using Xunit;

namespace Kmp.Core.Tests;

public sealed class LicenseScheduleTests
{
    const long Min = 60_000;
    const long Hour = 60 * Min;
    const long Day = 24 * Hour;
    const long Now = 1_800_000_000_000;

    [Fact]
    public void FirstCheckAndUnreachableServerAreAlwaysDue()
    {
        Assert.True(LicenseSchedule.Due(Now, Now + 30 * Day, lastOk: null, online: true));
        Assert.True(LicenseSchedule.Due(Now, Now + 30 * Day, lastOk: Now - Min, online: false));
        // Durmasının üstünden 7 gün geçmiş olsa da açılışta bir kez sorulur.
        Assert.True(LicenseSchedule.Due(Now, Now - 30 * Day, lastOk: null, online: true));
    }

    [Fact]
    public void PlayingChecksOnceADay()
    {
        Assert.False(LicenseSchedule.Due(Now, Now + 30 * Day, Now - 23 * Hour, true));
        Assert.True(LicenseSchedule.Due(Now, Now + 30 * Day, Now - 24 * Hour, true));
        // Bitişi bilinmeyen (eski kayıt) hesap da çalıyor sayılır.
        Assert.False(LicenseSchedule.Due(Now, null, Now - Hour, true));
    }

    [Fact]
    public void StoppedChecksEveryThirtyMinutes()
    {
        Assert.False(LicenseSchedule.Due(Now, Now - Hour, Now - 15 * Min, true));
        Assert.True(LicenseSchedule.Due(Now, Now - Hour, Now - 30 * Min, true));
        // 15 dk'lık zamanlayıcı turu birkaç saniye erken gelse de ikinci turda sorulur.
        Assert.True(LicenseSchedule.Due(Now, Now - Hour, Now - 30 * Min + 5_000, true));
        // Yayın tam şimdi durdu, son sorgu saatler önce: hemen sorulur.
        Assert.True(LicenseSchedule.Due(Now, Now, Now - 5 * Hour, true));
    }

    [Fact]
    public void StoppedForSevenDaysOnlyChecksOnStartup()
    {
        Assert.True(LicenseSchedule.Due(Now, Now - 7 * Day + Min, Now - Hour, true));
        Assert.False(LicenseSchedule.Due(Now, Now - 7 * Day, Now - Hour, true));
        Assert.False(LicenseSchedule.Due(Now, Now - 7 * Day, Now - 3 * Day, true));
    }
}
