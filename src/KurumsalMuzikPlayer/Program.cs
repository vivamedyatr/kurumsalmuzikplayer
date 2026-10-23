using System;
using System.Globalization;
using KurumsalMuzikPlayer.Engine;
using KurumsalMuzikPlayer.Ui;

namespace KurumsalMuzikPlayer;

/// <summary>
/// Tek exe, iki rol (BRIEF §4):
///   (bağımsız değişkensiz)  motor — tepside, görünmez; müzik, indirme, zamanlayıcı. Zaten çalışıyorsa pencereyi açtırır.
///   --ui [--gizli]         pencere — ayrı süreç; donarsa motor onu kapatıp yeniden açar.
/// Motor seçenekleri: --gizli (pencereyi simge durumunda aç), --ui-acik (pencere zaten açık, yenisini açma),
///   --deneme-arsiv [--gb 6] [--desen kap|dosya] [--mbps 6]  Faz 0 dayanıklılık deneyi.
/// </summary>
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var a = new Args(args);
        return a.Has("--ui") ? UiProgram.Run(a.Has("--gizli")) : EngineProgram.Run(a);
    }
}

sealed class Args
{
    readonly string[] _args;

    public Args(string[] args) => _args = args;

    public bool Has(string name) => Array.IndexOf(_args, name) >= 0;

    public string? Value(string name)
    {
        var i = Array.IndexOf(_args, name);
        return i >= 0 && i + 1 < _args.Length ? _args[i + 1] : null;
    }

    public double Number(string name, double fallback) =>
        double.TryParse(Value(name)?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
