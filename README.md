# 达芬奇 (DaVinci)

DSP（戴森球计划）极简无头服务端增强插件，与 [Nebula](https://github.com/NebulaModTeam/nebula) 联用。

## 功能

- 无头模式下静音音频、跳过成就轮询
- HTTP 管理端口（仅 127.0.0.1，默认 `28420`）：
  - `GET /status` — 服务器状态，protobuf 格式（`?format=json` 切 JSON）
  - `POST /save` — 手动存档
  - `POST /stop` — 保存并安全停服
- 仅在 `-nebula-server` 启动时激活，普通游戏不受影响

## 部署

1. 游戏目录放 `steam_appid.txt`（内容一行：`1366540`）——否则直启 exe 时 SteamSDK 初始化失败，游戏会自动退出。
2. 构建产物自动输出到游戏 `BepInEx/plugins/DaVinci/`：

```
dotnet build -c Release -m:1 -nodeReuse:false
```

3. 启动服务端：

```
DSPGAME.exe -nebula-server -batchmode -load-latest -ups 60
```

## 构建

需要 .NET SDK，NuGet 源含 BepInEx feed（见 nuget.config）。
