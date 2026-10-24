using System;
using System.IO;
using System.Security.Principal;

namespace Kmp.Core;

/// <summary>Programın veri klasörleri (BRIEF §4). KMP_DATA_DIR ile değiştirilebilir (deneme için).</summary>
public static class AppPaths
{
    public const string ProductName = "Kurumsal Müzik Player";

    static readonly string Root = Environment.GetEnvironmentVariable("KMP_DATA_DIR") is { Length: > 0 } dir
        ? dir
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kurumsal Muzik Player");

    public static string DataDir => Ensure(Root);
    public static string LogDir => Ensure(Path.Combine(Root, "gunluk"));
    public static string TestArchiveDir => Path.Combine(Root, "deneme-arsiv");
    public static string SettingsFile => Path.Combine(Ensure(Root), "ayarlar.json");

    /// <summary>Kullanıcıya özel ad parçası (boru ve kilit adları; aynı bilgisayardaki başka kullanıcıyla karışmasın).</summary>
    public static string UserKey { get; } = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;

    static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
