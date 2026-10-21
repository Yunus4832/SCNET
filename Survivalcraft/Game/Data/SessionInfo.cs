using Engine.Network;

namespace Game;

public enum SessionTarget
{
    MainMenu = 0,

    WorldList = 1,

    World = 2,

    ServerBrowser = 3,

    RemoteServer = 4
}

public sealed class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public SessionTarget Target { get; set; } = SessionTarget.MainMenu;

    public string World { get; set; } = "World";

    public string Seed { get; set; } = string.Empty;

    public GameMode? GameMode { get; set; }

    public TerrainGenerationMode? TerrainGenerationMode { get; set; }

    public int? TerrainLevel { get; set; }

    public NetworkTarget? RemoteTarget { get; set; }

    public int ServerPort { get; set; }

    public int BroadcastPort { get; set; }

    /// <summary>Optional player name used to automatically enter a world after startup.</summary>
    public string? AutoJoinPlayerName { get; set; }

    public P2PRoomOverrides P2POverrides { get; set; } = new();

    /// <summary>Optional HTTP command host enablement override restored with this startup session.</summary>
    public bool? HttpCommandEnabled { get; set; }

    /// <summary>Optional HTTP command port override restored with this startup session.</summary>
    public int? HttpCommandPort { get; set; }

    /// <summary>Optional HTTP command access token restored with this startup session.</summary>
    public string? HttpCommandAccessToken { get; set; }
}
