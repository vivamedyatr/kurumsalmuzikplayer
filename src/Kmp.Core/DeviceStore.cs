using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Kmp.Core;

/// <summary>Bu bilgisayarın şube kaydı. Sunucunun verdiği anahtar dahil.</summary>
public sealed class DeviceInfo
{
    public string Token { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string Code { get; set; } = "";
    /// <summary>Hesabın hizmet bitişi (Unix ms).</summary>
    public long ServiceEndsAt { get; set; }
    /// <summary>Sunucuya en son başarıyla ulaşılan an (Unix ms; 7 gün kuralı, Faz 3).</summary>
    public long LastOkAt { get; set; }
}

/// <summary>
/// cihaz.bin — Windows'un koruma alanıyla (DPAPI, kullanıcı kapsamı) şifreli. Dosya başka bir kullanıcıya ya da
/// bilgisayara kopyalansa açılmaz (BRIEF §2.2, §5).
/// </summary>
public static class DeviceStore
{
    static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Kurumsal Müzik Player · cihaz");
    static string FilePath => Path.Combine(AppPaths.DataDir, "cihaz.bin");

    public static DeviceInfo? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
            var d = JsonConvert.DeserializeObject<DeviceInfo>(Encoding.UTF8.GetString(plain));
            return d is { Token.Length: > 0 } ? d : null;
        }
        catch (Exception e)
        {
            Log.Warn($"cihaz.bin okunamadı ({e.GetType().Name}); yeniden giriş gerekecek");
            return null;
        }
    }

    public static void Save(DeviceInfo d)
    {
        var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(d)), Entropy, DataProtectionScope.CurrentUser);
        var tmp = FilePath + ".tmp";
        File.WriteAllBytes(tmp, data);
        if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
        else File.Move(tmp, FilePath);
    }

    public static void Clear()
    {
        try { File.Delete(FilePath); }
        catch (Exception e) { Log.Warn($"cihaz.bin silinemedi: {e.Message}"); }
    }
}
