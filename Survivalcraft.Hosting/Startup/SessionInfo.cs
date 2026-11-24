using Engine.Network;

using Game.Servers;
using Game.World;

namespace Game.Startup;

public sealed class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public SessionTarget Target { get; set; } = SessionTarget.MainMenu;

    public string World { get; set; } = "World";

    /// <summary>启动准备触发重启后，允许完成原世界创建请求的一次性授权。</summary>
    public bool CreateWorldIfMissing { get; set; }

    /// <summary>所选模组环境就绪后才执行的一次性完整世界创建请求。</summary>
    public WorldCreationRequest? WorldCreation { get; set; }

    /// <summary>启动准备触发重启后，继续完成世界托管启用操作的一次性请求。</summary>
    public bool ForceWorldRunServer { get; set; }

    public string Seed { get; set; } = string.Empty;

    public GameMode? GameMode { get; set; }

    public TerrainCreationOptions TerrainCreation { get; set; } = new();


    public NetworkTarget? RemoteTarget { get; set; }

    public string? ServerIdentityAddress { get; set; }

    public int ServerPort { get; set; }

    public int BroadcastPort { get; set; }

    /// <summary>可选的玩家名称，用于启动后自动进入世界。</summary>
    public string? AutoJoinCharacterName { get; set; }

    public Admission.ServerAdmissionOverrides AdmissionOverrides { get; set; } = new();

    public P2PRoomOverrides P2POverrides { get; set; } = new();

    /// <summary>随启动会话恢复的可选 HTTP 命令宿主启用设置。</summary>
    public bool? HttpCommandEnabled { get; set; }

    /// <summary>随启动会话恢复的可选 HTTP 命令端口设置。</summary>
    public int? HttpCommandPort { get; set; }

    /// <summary>随启动会话恢复的可选 HTTP 命令访问令牌。</summary>
    public string? HttpCommandAccessToken { get; set; }
}

/// <summary>在模组环境切换期间保留世界创建草稿，不提前生成存档。</summary>
public sealed record WorldCreationRequest(
    WorldSettings Settings,
    WorldHostingSettings Hosting,
    Admission.ServerAdmissionPolicy Admission
);
