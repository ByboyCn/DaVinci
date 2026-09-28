using System.Collections.Generic;
using ProtoBuf;

namespace DaVinci.Management;

/// <summary>达芬奇管理协议（protobuf）。字段编号只增不改，保持前后兼容。</summary>
[ProtoContract]
public class StatusResponse
{
    [ProtoMember(1)] public string GameVersion { get; set; }
    [ProtoMember(2)] public string SaveName { get; set; }
    [ProtoMember(3)] public int StarCount { get; set; }
    [ProtoMember(4)] public long GameTick { get; set; }
    [ProtoMember(5)] public float Ups { get; set; }
    [ProtoMember(6)] public int PlayerCount { get; set; }
    [ProtoMember(7)] public List<PlayerInfo> Players { get; set; } = new();
    [ProtoMember(8)] public long UptimeSeconds { get; set; }
}

[ProtoContract]
public class PlayerInfo
{
    [ProtoMember(1)] public string Username { get; set; }
    [ProtoMember(2)] public ushort PlayerId { get; set; }
    [ProtoMember(3)] public string PlanetName { get; set; }
}

[ProtoContract]
public class CommandResponse
{
    [ProtoMember(1)] public bool Ok { get; set; }
    [ProtoMember(2)] public string Message { get; set; } = "";
}
