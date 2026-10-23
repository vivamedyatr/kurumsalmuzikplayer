using System;
using System.IO;
using Kmp.Core;
using Xunit;

namespace Kmp.Core.Tests;

public sealed class AlbumContainerTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "kmp-test-" + Guid.NewGuid().ToString("N"));
    string FilePath => Path.Combine(_dir, "album.kmp");

    public AlbumContainerTests()
    {
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable("KMP_DATA_DIR", _dir); // günlük gerçek klasöre yazılmasın
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    static byte[] Data(int length, int seed)
    {
        var b = new byte[length];
        new Random(seed).NextBytes(b);
        return b;
    }

    [Fact]
    public void AppendedSegmentsReadBackAfterReopen()
    {
        using (var c = AlbumContainer.Open(FilePath))
        {
            c.Append(0, 0, Data(1000, 1), 1000);
            c.Append(0, 1, Data(2000, 2), 2000);
            c.Append(39, 20, Data(500, 3), 500);
            Assert.Equal(Data(2000, 2), c.TryRead(0, 1));
        }
        using var reopened = AlbumContainer.Open(FilePath);
        Assert.Equal(3, reopened.Count);
        Assert.Equal(Data(1000, 1), reopened.TryRead(0, 0));
        Assert.Equal(Data(500, 3), reopened.TryRead(39, 20));
        Assert.Null(reopened.TryRead(1, 0));
        Assert.Equal(AlbumContainer.FileHeaderSize + 3 * AlbumContainer.RecordHeaderSize + 3500, new FileInfo(FilePath).Length);
    }

    [Fact]
    public void TornTailIsCutOnOpen()
    {
        using (var c = AlbumContainer.Open(FilePath))
        {
            c.Append(0, 0, Data(1000, 1), 1000);
            c.Append(0, 1, Data(1000, 2), 1000);
        }
        var full = new FileInfo(FilePath).Length;
        using (var fs = new FileStream(FilePath, FileMode.Open)) fs.SetLength(full - 300); // elektrik kesildi
        using var reopened = AlbumContainer.Open(FilePath);
        Assert.Equal(1, reopened.Count);
        Assert.False(reopened.Contains(0, 1));
        Assert.Equal(full - 1000 - AlbumContainer.RecordHeaderSize, new FileInfo(FilePath).Length);
        reopened.Append(0, 1, Data(1000, 2), 1000); // yeniden iner
        Assert.Equal(Data(1000, 2), reopened.TryRead(0, 1));
    }

    [Fact]
    public void LaterCopyOfSameSegmentWins()
    {
        using var c = AlbumContainer.Open(FilePath);
        c.Append(2, 3, Data(100, 1), 100);
        c.Append(2, 3, Data(120, 9), 120);
        Assert.Equal(1, c.Count);
        Assert.Equal(Data(120, 9), c.TryRead(2, 3));
    }

    [Fact]
    public void CorruptBodyIsDetected()
    {
        using (var c = AlbumContainer.Open(FilePath)) c.Append(0, 0, Data(1000, 1), 1000);
        using (var fs = new FileStream(FilePath, FileMode.Open))
        {
            fs.Position = AlbumContainer.FileHeaderSize + AlbumContainer.RecordHeaderSize + 10;
            fs.WriteByte(0xFF);
            fs.WriteByte(0x00);
        }
        using var reopened = AlbumContainer.Open(FilePath);
        Assert.True(reopened.Contains(0, 0));
        Assert.Null(reopened.TryRead(0, 0));
    }

    [Fact]
    public void UnknownFileIsReset()
    {
        File.WriteAllText(FilePath, "bu bir albüm kabı değil");
        using var c = AlbumContainer.Open(FilePath);
        Assert.Equal(0, c.Count);
        c.Append(0, 0, Data(10, 1), 10);
        Assert.Equal(Data(10, 1), c.TryRead(0, 0));
    }

    [Fact]
    public void Crc32MatchesKnownValue()
    {
        var b = System.Text.Encoding.ASCII.GetBytes("123456789");
        Assert.Equal(0xCBF43926u, Crc32.Compute(b, 0, b.Length));
    }
}
