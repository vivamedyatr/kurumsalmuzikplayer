using System;
using System.Collections.Generic;
using System.IO;

namespace Kmp.Core;

/// <summary>
/// Albüm kabı (BRIEF §5): bir albümün bütün şarkı parçaları tek dosyada, art arda. Program parçaları sunucudan
/// geldiği gibi ekler; ayrı içindekiler dosyası yok, açılışta kayıt başlıkları taranır.
///
/// Biçim (little-endian):
///   dosya başı  8 bayt: "KMPA" + sürüm (u32 = 1)
///   kayıt başı 20 bayt: "KMPS" + şarkı kimliği (u32) + parça (u16) + ayrılmış (u16) + uzunluk (i32) + CRC-32 (u32)
///   kayıt gövdesi
/// Şarkı kimliği şarkının sunucudaki yolundan türetilir (TrackId); albüme şarkı eklense de kaymaz.
/// Yarım kalan son kayıt (elektrik kesintisi) açılışta kesilip atılır. Aynı parça yeniden eklenirse sonuncusu geçerli.
/// Tek yazar (indirici), çok okuyucu (çalar).
/// </summary>
public sealed class AlbumContainer : IDisposable
{
    const uint FileMagic = 0x41504D4B; // "KMPA"
    const uint RecordMagic = 0x53504D4B; // "KMPS"
    const uint FormatVersion = 1;
    public const int FileHeaderSize = 8;
    public const int RecordHeaderSize = 20;
    public const int MaxRecord = 16 * 1024 * 1024;

    struct Entry
    {
        public long Offset;
        public int Length;
        public uint Crc;
    }

    readonly object _indexGate = new();
    readonly object _writeGate = new();
    readonly Dictionary<long, Entry> _index = new();
    FileStream? _writer;
    long _length;

    public string FilePath { get; }

    AlbumContainer(string path) => FilePath = path;

    public static AlbumContainer Open(string path)
    {
        var c = new AlbumContainer(path);
        c.Load();
        return c;
    }

    public long Length { get { lock (_indexGate) return _length; } }
    public int Count { get { lock (_indexGate) return _index.Count; } }

    public bool Contains(uint track, int segment)
    {
        lock (_indexGate) return _index.ContainsKey(Key(track, segment));
    }

    static long Key(uint track, int segment) => ((long)track << 16) | (uint)segment;

    /// <summary>Şarkının kaptaki kimliği: SHA-256(yol) ilk 4 bayt.</summary>
    public static uint TrackId(string trackKey)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToUInt32(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(trackKey)), 0);
    }

    void Load()
    {
        if (!File.Exists(FilePath)) return;
        using var fs = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        var head = new byte[RecordHeaderSize];
        if (fs.Length < FileHeaderSize || !ReadExact(fs, head, FileHeaderSize)
            || BitConverter.ToUInt32(head, 0) != FileMagic || BitConverter.ToUInt32(head, 4) != FormatVersion)
        {
            Log.Warn($"Albüm kabı tanınmadı, sıfırlanıyor: {Path.GetFileName(FilePath)}");
            fs.SetLength(0);
            return;
        }
        long pos = FileHeaderSize;
        while (pos + RecordHeaderSize <= fs.Length)
        {
            fs.Position = pos;
            if (!ReadExact(fs, head, RecordHeaderSize) || BitConverter.ToUInt32(head, 0) != RecordMagic) break;
            var track = BitConverter.ToUInt32(head, 4);
            int segment = BitConverter.ToUInt16(head, 8);
            var len = BitConverter.ToInt32(head, 12);
            if (len < 0 || len > MaxRecord || pos + RecordHeaderSize + len > fs.Length) break;
            _index[Key(track, segment)] = new Entry { Offset = pos + RecordHeaderSize, Length = len, Crc = BitConverter.ToUInt32(head, 16) };
            pos += RecordHeaderSize + len;
        }
        if (pos != fs.Length)
        {
            Log.Warn($"Albüm kabının sonu yarım, {fs.Length - pos} bayt atıldı: {Path.GetFileName(FilePath)}");
            fs.SetLength(pos);
        }
        _length = pos;
    }

    /// <summary>Bir parçayı kabın sonuna ekler ve diske yazılmasını bekler.</summary>
    public void Append(uint track, int segment, byte[] data, int count)
    {
        if (segment < 0 || segment > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(segment));
        if (count < 0 || count > MaxRecord || count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));
        var crc = Crc32.Compute(data, 0, count);
        var head = new byte[RecordHeaderSize];
        Put(head, 0, RecordMagic);
        Put(head, 4, track);
        Put16(head, 8, segment);
        Put(head, 12, (uint)count);
        Put(head, 16, crc);
        lock (_writeGate)
        {
            _writer ??= OpenWriter();
            var start = _writer.Length;
            _writer.Position = start;
            _writer.Write(head, 0, head.Length);
            _writer.Write(data, 0, count);
            _writer.Flush(true);
            lock (_indexGate)
            {
                _index[Key(track, segment)] = new Entry { Offset = start + RecordHeaderSize, Length = count, Crc = crc };
                _length = start + RecordHeaderSize + count;
            }
        }
    }

    FileStream OpenWriter()
    {
        var fs = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1 << 16);
        if (fs.Length == 0)
        {
            var head = new byte[FileHeaderSize];
            Put(head, 0, FileMagic);
            Put(head, 4, FormatVersion);
            fs.Write(head, 0, head.Length);
            fs.Flush(true);
            lock (_indexGate) _length = FileHeaderSize;
        }
        return fs;
    }

    /// <summary>Parçayı okur; yoksa ya da bozuksa (CRC tutmuyor) null.</summary>
    public byte[]? TryRead(uint track, int segment)
    {
        Entry e;
        lock (_indexGate)
        {
            if (!_index.TryGetValue(Key(track, segment), out e)) return null;
        }
        using var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        fs.Position = e.Offset;
        var buf = new byte[e.Length];
        if (!ReadExact(fs, buf, e.Length) || Crc32.Compute(buf, 0, e.Length) != e.Crc)
        {
            Log.Warn($"Bozuk parça: {Path.GetFileName(FilePath)} şarkı {track} parça {segment}");
            return null;
        }
        return buf;
    }

    /// <summary>Yazma ucunu kapatır (albüm tamamlandı). Okuma sürer.</summary>
    public void CloseWriter()
    {
        lock (_writeGate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    public void Dispose() => CloseWriter();

    static bool ReadExact(Stream s, byte[] buf, int count)
    {
        var read = 0;
        while (read < count)
        {
            var n = s.Read(buf, read, count - read);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }

    static void Put(byte[] b, int at, uint v)
    {
        b[at] = (byte)v;
        b[at + 1] = (byte)(v >> 8);
        b[at + 2] = (byte)(v >> 16);
        b[at + 3] = (byte)(v >> 24);
    }

    static void Put16(byte[] b, int at, int v)
    {
        b[at] = (byte)v;
        b[at + 1] = (byte)(v >> 8);
    }
}
