using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Kmp.Core;

/// <summary>
/// Sunucunun offline paketi (POST /api/offline/manifest; web: src/lib/offline-manifest.ts). Saatte bir alınır,
/// paket.json olarak saklanır (arşiv anahtarı hariç; o DPAPI ile ayrı durur).
/// </summary>
public sealed class Manifest
{
    public string Version { get; set; } = "";
    public long ServerNow { get; set; }
    public List<Slot> Slots { get; set; } = new();
    public List<Genre> Genres { get; set; } = new();
    public List<Album> Albums { get; set; } = new();
    public List<Track> Tracks { get; set; } = new();
    public MediaInfo Media { get; set; } = new();
    public PackInfo? Pack { get; set; }
    public DeviceAlbums? Device { get; set; }

    public sealed class Slot
    {
        public int Hour { get; set; }
        public string Genre { get; set; } = "";
    }

    public sealed class Genre
    {
        public string Slug { get; set; } = "";
        public string Name { get; set; } = "";
        public CoverColors? Cover { get; set; }
    }

    public sealed class CoverColors
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string Ink { get; set; } = "";
    }

    public sealed class Album
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Genre { get; set; } = "";
        public string? CoverKey { get; set; }
    }

    public sealed class Track
    {
        public string Key { get; set; } = "";
        public string Album { get; set; } = "";
        public string Genre { get; set; } = "";
        public string Title { get; set; } = "";
        public long DurationMs { get; set; }
        /// <summary>Parça süreleri (sn); sayısı parça sayısı.</summary>
        public List<double> Segments { get; set; } = new();

        [JsonIgnore] uint? _id;
        /// <summary>Albüm kabındaki kimliği.</summary>
        [JsonIgnore] public uint Id => _id ??= AlbumContainer.TrackId(Key);

        /// <summary>Parçanın sunucudaki yolu (şifre sayacı buna bağlı).</summary>
        public string SegmentKey(int i) => $"{Key}/{i:0000}.ts";
    }

    public sealed class MediaInfo
    {
        public string Base { get; set; } = "";
        public string Token { get; set; } = "";
        public long ExpiresAt { get; set; }
        public ArchiveKey? Archive { get; set; }
    }

    public sealed class ArchiveKey
    {
        public string Id { get; set; } = "";
        public string Key { get; set; } = "";
    }

    public sealed class PackInfo
    {
        public long RotatedAt { get; set; }
        public long NextRotationAt { get; set; }
    }

    public sealed class DeviceAlbums
    {
        public List<string> Albums { get; set; } = new();
        public long? LastUpdateAt { get; set; }
    }

    /// <summary>
    /// İndirme sırası için tarzlar: şu an çalan tarz, sonra sıradaki saatlerinki, sonra akıştaki diğerleri
    /// (İstanbul saati; 00–08 arası son dilim sürer).
    /// </summary>
    public List<string> GenreOrder(int istanbulHour)
    {
        var slots = Slots.OrderBy(s => s.Hour).ToList();
        var cur = slots.FindLastIndex(s => s.Hour <= istanbulHour);
        if (cur < 0) cur = slots.Count - 1;
        var rotated = slots.Skip(Math.Max(cur, 0)).Concat(slots.Take(Math.Max(cur, 0))).Select(s => s.Genre);
        return rotated.Concat(Genres.Select(g => g.Slug)).Distinct().ToList();
    }
}
