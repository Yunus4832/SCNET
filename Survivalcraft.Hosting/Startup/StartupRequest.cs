using Game.Servers;

namespace Game.Startup;

public sealed class StartupRequest
{
    public bool HasExplicitSession { get; set; }

    public string SessionName { get; set; } = string.Empty;

    public string? World { get; set; }

    public string? Seed { get; set; }

    public GameMode? GameMode { get; set; }

    public TerrainCreationOptions TerrainCreation { get; set; } = new();


    public string? ConnectHost { get; set; }

    public int? ConnectPort { get; set; }

    public string? AutoJoinCharacterName { get; set; }

    public bool ForceWorldRunServer { get; set; }

    public string? ServerIdentityAddress { get; set; }

    public int? ServerPort { get; set; }

    public int? BroadcastPort { get; set; }

    public Admission.ServerAdmissionOverrides AdmissionOverrides { get; set; } = new();

    public P2PRoomOverrides P2POverrides { get; set; } = new();

    /// <summary>仅对本次启动会话生效的可选 HTTP 命令宿主启用设置。</summary>
    public bool? HttpCommandEnabled { get; set; }

    /// <summary>仅对本进程生效的可选 HTTP 命令监听端口。</summary>
    public int? HttpCommandPort { get; set; }

    /// <summary>仅对本进程生效的可选 HTTP 命令访问令牌。</summary>
    public string? HttpCommandAccessToken { get; set; }

    public bool Save { get; set; }
}
