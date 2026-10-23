using System;
using System.Security.Cryptography;
using System.Text;

namespace Kmp.Core;

/// <summary>
/// Arşiv parçalarının şifresi (BRIEF §5). Worker parçayı /e/ adresinden şifreli verir; program olduğu gibi saklar,
/// çalarken bellekte çözer. Web projesindeki src/lib/archive-crypto.ts ile birebir:
///   sayaç = SHA-256("kmp-iv.&lt;nesne yolu&gt;")[0..12] + 00 00 00 00, AES-256-CTR (sağdaki 32 bit artar).
/// .NET Framework'te hazır CTR yok: sayaç blokları AES-ECB ile şifrelenip veriyle XOR'lanır.
/// </summary>
public static class ArchiveCrypto
{
    /// <summary>Şifreler ya da çözer (CTR'de ikisi aynı işlem). İlk <paramref name="count"/> bayt işlenir.</summary>
    public static byte[] Crypt(byte[] key, string objectKey, byte[] data, int count = -1)
    {
        if (count < 0 || count > data.Length) count = data.Length;
        var counter = Counter(objectKey);
        var output = new byte[count];
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        using var enc = aes.CreateEncryptor();
        // Anahtar akışı 4 KB'lık parçalar hâlinde üretilir (blok blok çağırmaktan çok daha hızlı).
        const int Chunk = 4096;
        var counters = new byte[Chunk];
        var stream = new byte[Chunk];
        for (var pos = 0; pos < count; pos += Chunk)
        {
            var n = Math.Min(Chunk, count - pos);
            var blocks = (n + 15) / 16;
            for (var b = 0; b < blocks; b++)
            {
                Buffer.BlockCopy(counter, 0, counters, b * 16, 16);
                Increment(counter);
            }
            enc.TransformBlock(counters, 0, blocks * 16, stream, 0);
            for (var i = 0; i < n; i++) output[pos + i] = (byte)(data[pos + i] ^ stream[i]);
        }
        return output;
    }

    static byte[] Counter(string objectKey)
    {
        using var sha = SHA256.Create();
        var h = sha.ComputeHash(Encoding.UTF8.GetBytes("kmp-iv." + objectKey));
        var c = new byte[16];
        Buffer.BlockCopy(h, 0, c, 0, 12);
        return c;
    }

    static void Increment(byte[] c)
    {
        for (var i = 15; i >= 12; i--)
            if (++c[i] != 0) return;
    }
}
