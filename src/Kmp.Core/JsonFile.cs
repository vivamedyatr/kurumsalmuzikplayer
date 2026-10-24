using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Kmp.Core;

/// <summary>Küçük durum dosyaları: önce .tmp'ye yazılır, sonra yerine konur (yarım dosya kalmaz).</summary>
public static class JsonFile
{
    public static T? Read<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonConvert.DeserializeObject<T>(File.ReadAllText(path, Encoding.UTF8)) : null;
        }
        catch (Exception e)
        {
            Log.Warn($"{Path.GetFileName(path)} okunamadı: {e.Message}");
            return null;
        }
    }

    public static void Write(string path, object data)
    {
        try
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(data), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
        catch (Exception e)
        {
            Log.Warn($"{Path.GetFileName(path)} yazılamadı: {e.Message}");
        }
    }
}
