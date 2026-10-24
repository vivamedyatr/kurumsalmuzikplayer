using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Kmp.Core;

/// <summary>Çalma geçmişi (BRIEF §2.2): müzik ve anons, bu bilgisayarda son 7 gün. "Ne çaldı?" sorusu buradan.</summary>
public static class History
{
    const long KeepMs = 7 * 86_400_000L;
    const int Max = 3000;
    static readonly object Gate = new();
    static List<Entry> _items = JsonFile.Read<List<Entry>>(FilePath) ?? new List<Entry>();
    static Timer? _saveTimer;

    public sealed class Entry
    {
        public long At { get; set; }
        /// <summary>music | anons</summary>
        public string Kind { get; set; } = "music";
        public string Title { get; set; } = "";
        public string Sub { get; set; } = "";
    }

    static string FilePath => Path.Combine(AppPaths.DataDir, "gecmis.json");

    public static void Add(string kind, string title, string sub = "")
    {
        var now = Clock.Now();
        lock (Gate)
        {
            var last = _items.LastOrDefault();
            // Aynı şarkı art arda iki kez yazılmaz (yeniden bağlanma, liste tazeleme).
            if (last != null && last.Kind == kind && last.Title == title && now - last.At < 10 * 60_000) return;
            _items.Add(new Entry { At = now, Kind = kind, Title = title, Sub = sub });
            var cutoff = now - KeepMs;
            if (_items.Count > Max || _items[0].At < cutoff) _items = _items.Where(i => i.At >= cutoff).Skip(Math.Max(0, _items.Count - Max)).ToList();
            _saveTimer ??= new Timer(_ => Save(), null, 5000, Timeout.Infinite);
        }
    }

    /// <summary>En yeniden eskiye.</summary>
    public static List<Entry> Recent(int n)
    {
        lock (Gate) return _items.Skip(Math.Max(0, _items.Count - n)).Reverse().ToList();
    }

    public static void Save()
    {
        List<Entry> copy;
        lock (Gate)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
            copy = _items.ToList();
        }
        JsonFile.Write(FilePath, copy);
    }
}
