using System;
using System.Reflection;
using Microsoft.Win32;

namespace Kmp.Core;

public static class AppInfo
{
    /// <summary>Tam sürüm, örn. "1.0.0-faz0" (pencerenin sağ altında yazar).</summary>
    public static string Version { get; } =
        (Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly)
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";

    public static string Describe()
    {
        string net;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            net = key?.GetValue("Release")?.ToString() ?? "?";
        }
        catch { net = "?"; }
        return $"{AppPaths.ProductName} {Version}, Windows {Environment.OSVersion.Version}, .NET Framework sürüm kodu {net}, "
             + $"{(Environment.Is64BitProcess ? "64" : "32")} bit, {Environment.ProcessorCount} çekirdek";
    }
}

public static class Ipc
{
    public static string EnginePipe => "kmp-motor-" + AppPaths.UserKey;
    public static string EngineMutex => @"Local\kmp-motor-" + AppPaths.UserKey;
    public static string UiMutex => @"Local\kmp-pencere-" + AppPaths.UserKey;
}
