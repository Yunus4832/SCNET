namespace Game;

public sealed class StartupRequest
{
    public bool HasExplicitSession { get; set; }

    public string SessionName { get; set; } = string.Empty;

    public string? World { get; set; }

    public string? Seed { get; set; }

    public GameMode? GameMode { get; set; }

    public string? ConnectHost { get; set; }

    public int? ConnectPort { get; set; }

    public string? PlayerName { get; set; }

    public bool ForceWorldRunServer { get; set; }

    public int? ServerPort { get; set; }

    public int? BroadcastPort { get; set; }

    /// <summary>Optional HTTP command host enablement override for this startup session.</summary>
    public bool? HttpCommandEnabled { get; set; }

    /// <summary>
    ///     Optional HTTP command listener port override for this process.
    /// </summary>
    public int? HttpCommandPort { get; set; }

    /// <summary>Optional HTTP command access token override for this process.</summary>
    public string? HttpCommandAccessToken { get; set; }

    public bool Save { get; set; }
}
