using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Kmp.Core;

/// <summary>ayarlar.json (motor yazar).</summary>
public sealed class Settings
{
    public int Volume { get; set; } = 80;

    public static Settings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(AppPaths.SettingsFile, Encoding.UTF8)) ?? new Settings();
        }
        catch (Exception e)
        {
            Log.Warn($"ayarlar.json okunamadı: {e.Message}");
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            var tmp = AppPaths.SettingsFile + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(this, Formatting.Indented), new UTF8Encoding(false));
            if (File.Exists(AppPaths.SettingsFile)) File.Replace(tmp, AppPaths.SettingsFile, null);
            else File.Move(tmp, AppPaths.SettingsFile);
        }
        catch (Exception e)
        {
            Log.Warn($"ayarlar.json yazılamadı: {e.Message}");
        }
    }
}
