using System.Security.Cryptography;

using Engine.Network;

using Game.Commands.Http;
using Game.Network.Chunks;

namespace Game.Configuration;

public sealed class HostPreferences
{
    public string ConsoleLanguageType { get; set; } = "zh-CN";

    public int ServerPort { get; set; } = 28887;

    public int BroadcastPort { get; set; } = 28888;

    public RelayPolicy InvitationRelayPolicy { get; set; } = RelayPolicy.Allowed;

    public bool HttpCommandEnabled { get; set; }

    public int HttpCommandPort { get; set; } = HttpCommandProtocol.DefaultPort;

    public string HttpCommandAccessToken { get; set; } = string.Empty;

    public int MaxClientVisibilityRange { get; set; } =
        NetworkTerrainPolicy.DefaultMaxClientVisibilityRange;

    public int NewYearCelebrationLastYear { get; set; } = 2015;

    public int CreatureTotalLimit { get; set; } = 24;

    public int CreatureAreaLimit { get; set; } = 3;

    public int CreatureMaxPlayerAreaLimit { get; set; } = 48;

    public int CreatureMaxPointLimit { get; set; } = 3;

    public int CreatureAreaRadius { get; set; } = 16;

    public int CreatureTotalLimitConstant { get; set; } = 18;

    public int CreatureAreaLimitConstant { get; set; } = 4;

    public int CreatureAreaRadiusConstant { get; set; } = 2;

    public float CreatureSpawnIntervalTime { get; set; } = 60f;

    public float CreatureConstantSpawnIntervalTime { get; set; } = 1f;

    public int ServerChunkCountSendPer { get; set; } =
        NetworkTerrainPolicy.DefaultServerChunkCountSendPer;

    public int ServerChunkBytesSendPerSecond { get; set; } =
        NetworkTerrainPolicy.DefaultServerChunkBytesSendPerSecond;

    public bool AutoGarbageCollect { get; set; } = true;

    public bool EnsureHttpCommandAccessToken()
    {
        if (!string.IsNullOrWhiteSpace(HttpCommandAccessToken) && HttpCommandAccessToken.Length >= 32) { return false; }

        HttpCommandAccessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Log.Information("Generated a new HTTP command access token for this instance.");
        return true;
    }
}
