using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;

namespace DaVinci.Management;

/// <summary>
/// 轻量 HTTP 管理端口（仅 127.0.0.1）。
/// GET /status  — protobuf（默认）或 ?format=json
/// GET /players — 同上
/// POST /save   — 手动存档
/// POST /stop   — 安全停服（保存并退出）
/// </summary>
public class ManagementServer
{
    private HttpListener listener;
    private Thread worker;
    private volatile bool running;
    private readonly Stopwatch uptime = Stopwatch.StartNew();

    private static readonly ConcurrentQueue<Action> mainThreadQueue = new();

    /// <summary>由插件每帧调用，在主线程执行排入的动作。</summary>
    public static void DrainMainThread()
    {
        while (mainThreadQueue.TryDequeue(out var a))
        {
            try { a(); }
            catch (Exception e) { DaVinciPlugin.Log.LogWarning($"主线程任务失败: {e.Message}"); }
        }
    }

    public void Start(int port)
    {
        listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        running = true;
        worker = new Thread(Loop) { IsBackground = true, Name = "DaVinci.Management" };
        worker.Start();
        DaVinciPlugin.Log.LogInfo($"管理端口: http://127.0.0.1:{port}/status");
    }

    public void Stop()
    {
        running = false;
        try { listener?.Stop(); } catch { }
    }

    private void Loop()
    {
        while (running)
        {
            HttpListenerContext ctx;
            try { ctx = listener.GetContext(); }
            catch { break; }
            try { Handle(ctx); }
            catch (Exception e) { DaVinciPlugin.Log.LogWarning($"管理请求处理失败: {e.Message}"); }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url.AbsolutePath.TrimEnd('/');
        var json = ctx.Request.QueryString["format"] == "json";

        switch (path)
        {
            case "/status":
            case "/players":
                var status = BuildStatus();
                Reply(ctx, json ? ToJson(status) : ToProto(status), json);
                break;

            case "/save":
                mainThreadQueue.Enqueue(() => { GameSave.AutoSave(); });
                Reply(ctx, Ok("save requested"), json);
                break;

            case "/stop":
                mainThreadQueue.Enqueue(() =>
                {
                    DaVinciPlugin.Log.LogInfo("管理端口请求停服，保存并退出");
                    GameSave.AutoSave();
                    UnityEngine.Application.Quit();
                });
                Reply(ctx, Ok("stopping"), json);
                break;

            default:
                ctx.Response.StatusCode = 404;
                Reply(ctx, Ok("unknown endpoint"), json);
                break;
        }
    }

    private StatusResponse BuildStatus()
    {
        var s = new StatusResponse
        {
            UptimeSeconds = (long)uptime.Elapsed.TotalSeconds,
        };
        try { s.GameVersion = GameConfig.gameVersion.ToFullString(); } catch { }
        try { s.GameTick = GameMain.gameTick; } catch { }
        try
        {
            if (GameMain.galaxy != null)
            {
                s.StarCount = GameMain.galaxy.starCount;
            }
        }
        catch { }
        try
        {
            // 存档名：从 Nebula 命令行配置取
            var opts = AccessTools.TypeByName("NebulaModel.Config")
                ?.GetProperty("CommandLineOptions", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            s.SaveName = Traverse.Create(opts).Property("SaveName").GetValue<string>() ?? "";
        }
        catch { }

        foreach (var p in QueryNebulaPlayers())
        {
            s.Players.Add(p);
        }
        s.PlayerCount = s.Players.Count;
        return s;
    }

    /// <summary>软依赖 Nebula：通过反射读取在线玩家列表。</summary>
    private static System.Collections.Generic.List<PlayerInfo> QueryNebulaPlayers()
    {
        var result = new System.Collections.Generic.List<PlayerInfo>();
        try
        {
            var multiplayer = AccessTools.TypeByName("NebulaWorld.Multiplayer");
            var session = Traverse.Create(multiplayer).Property("Session").GetValue<object>();
            if (session == null) return result;
            var players = Traverse.Create(session).Property("World").Property("Players").GetValue<IEnumerable>();
            if (players == null) return result;
            foreach (var player in players)
            {
                var t = Traverse.Create(player);
                result.Add(new PlayerInfo
                {
                    Username = t.Property("Username").GetValue<string>() ?? "?",
                    PlayerId = t.Property("Id").GetValue<ushort>(),
                });
            }
        }
        catch (Exception e)
        {
            DaVinciPlugin.Log.LogDebug($"读取 Nebula 玩家列表失败: {e.Message}");
        }
        return result;
    }

    private static byte[] ToProto<T>(T obj)
    {
        using var ms = new MemoryStream();
        ProtoBuf.Serializer.Serialize(ms, obj);
        return ms.ToArray();
    }

    private static byte[] ToJson(object obj)
    {
        var sb = new StringBuilder("{");
        bool first = true;
        foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!first) sb.Append(',');
            first = false;
            var v = p.GetValue(obj, null);
            sb.Append('"').Append(p.Name).Append("\":");
            if (v is string str) sb.Append('"').Append(Escape(str)).Append('"');
            else if (v is IList list)
            {
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(ToJson(list[i]));
                }
                sb.Append(']');
            }
            else sb.Append(v ?? 0);
        }
        sb.Append('}');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

    private static CommandResponse Ok(string msg) => new() { Ok = true, Message = msg };

    private static void Reply(HttpListenerContext ctx, CommandResponse resp, bool json) =>
        Reply(ctx, json ? ToJson(resp) : ToProto(resp), json);

    private static void Reply(HttpListenerContext ctx, byte[] body, bool json)
    {
        ctx.Response.ContentType = json ? "application/json" : "application/x-protobuf";
        ctx.Response.ContentLength64 = body.Length;
        ctx.Response.OutputStream.Write(body, 0, body.Length);
        ctx.Response.Close();
    }
}
