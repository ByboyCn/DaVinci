using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DaVinci.Management;
using DaVinci.Patches;
using HarmonyLib;

namespace DaVinci;

/// <summary>
/// 达芬奇 (DaVinci) — DSP 极简无头服务端增强插件。
/// 与 Nebula 联用：在 -nebula-server -batchmode 模式下裁剪无用子系统，
/// 并提供一个 protobuf 管理端口。
/// </summary>
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("dsp.nebula", BepInDependency.DependencyFlags.SoftDependency)]
public class DaVinciPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.byboy.davinci";
    public const string PluginName = "DaVinci";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Log;
    internal static ConfigEntry<int> HttpPort;
    internal static ConfigEntry<bool> EnableHttp;
    internal static ConfigEntry<int> TargetUps;

    private Harmony harmony;
    public static ManagementServer Server { get; private set; }

    public static bool IsDedicated =>
        Environment.GetCommandLineArgs().Length > 0 &&
        Array.IndexOf(Environment.GetCommandLineArgs(), "-nebula-server") >= 0;

    private void Awake()
    {
        Log = Logger;

        HttpPort = Config.Bind("Management", "HttpPort", 28420,
            "管理端口（仅监听 127.0.0.1）。/status /players /save /stop");
        EnableHttp = Config.Bind("Management", "EnableHttp", true, "启用 HTTP 管理端口");
        TargetUps = Config.Bind("Performance", "TargetUps", 60, "服务端目标 UPS（等价 Nebula -ups）");

        if (!IsDedicated)
        {
            Log.LogInfo("DaVinci idle (not a dedicated server run)");
            return;
        }

        Log.LogInfo($"DaVinci {PluginVersion} — 达芬奇无头服务端模式启动");

        harmony = new Harmony(PluginGuid);
        harmony.PatchAll(typeof(DaVinciPlugin).Assembly);

        if (EnableHttp.Value)
        {
            Server = new ManagementServer();
            Server.Start(HttpPort.Value);
        }

        UnityEngine.Application.targetFrameRate = 30;
        VFAudio.audioVolume = 0f;
        VFAudio.backgroundMute = true;
    }

    private void Update()
    {
        ManagementServer.DrainMainThread();
    }

    private void OnDestroy()
    {
        Server?.Stop();
        harmony?.UnpatchSelf();
    }
}
