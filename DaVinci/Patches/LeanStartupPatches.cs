using HarmonyLib;

namespace DaVinci.Patches;

/// <summary>
/// 极简启动补丁：只在不影响模拟的前提下裁剪枝节。
/// 注意：不伪造 SteamManager.Initialized（后续 Steamworks 调用会 NRE），
/// 正确做法是在游戏目录放置 steam_appid.txt（内容 1366540）。
/// 音频静音由插件直接置 VFAudio.audioVolume = 0（Nebula 亦做同样的事），无需 Harmony。
/// </summary>
[HarmonyPatch]
public static class LeanStartupPatches
{
}
