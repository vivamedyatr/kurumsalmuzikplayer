using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Kmp.Core;
using Xunit;

namespace Kmp.Core.Tests;

public sealed class ArchiveCryptoTests
{
    const string Secret = "deneme-gizli";
    const string Key = "music/deep-house/gece-yarisi/01/0000.ts";

    static byte[] ArchiveKey()
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return h.ComputeHash(Encoding.UTF8.GetBytes("kmp-arsiv.v1"));
    }

    static string Hex(byte[] b) => BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();

    // Web projesindeki archive-crypto.test.ts "ortak örnek" ile aynı: iki taraf birebir uyuşmalı.
    [Fact]
    public void MatchesWebSample()
    {
        var data = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        Assert.Equal("827873d3c6300f30cbe7936024546813", Hex(ArchiveCrypto.Crypt(ArchiveKey(), Key, data)).Substring(0, 32));
    }

    [Fact]
    public void RoundTripsLargeSegment()
    {
        var data = new byte[1_041_332];
        new Random(7).NextBytes(data);
        var once = ArchiveCrypto.Crypt(ArchiveKey(), Key, data);
        Assert.NotEqual(data.Take(64), once.Take(64));
        Assert.Equal(data, ArchiveCrypto.Crypt(ArchiveKey(), Key, once));
    }

    [Fact]
    public void PrefixDecryptsAlone()
    {
        var data = new byte[5000];
        new Random(3).NextBytes(data);
        var enc = ArchiveCrypto.Crypt(ArchiveKey(), Key, data);
        Assert.Equal(data.Take(188), ArchiveCrypto.Crypt(ArchiveKey(), Key, enc, 188));
    }
}
