using Game.Protocol.Identity;

namespace Game.Network;

/// <summary>One logical world connection; identity and phase are shared with its physical owner.</summary>
public class WorldConnection
{
    private IConnectionLiveness? _liveness;

    public byte ID { get; private set; }

    public Guid CharacterId { get; private set; }

    private PlayerAddress? _playerAddress;

    public PlayerAddress? PlayerAddress { get => _playerAddress; init => _playerAddress = value; }

    internal bool IsMetadataRecord { get; init; }

    public bool IsConnected => _liveness?.IsConnected ??
                               (IsMetadataRecord &&
                                World is { IsClosed: false } world &&
                                State != ClientState.NotConnected &&
                                WorldConnections.TryGet(world, out var roster) &&
                                roster!.Contains(this));

    public ClientState State { get; set; }

    public ConnectionPhase ConnectionPhase { get; set; }

    public Guid ConnectionEpoch { get; set; }

    public long ConnectionGeneration { get; internal set; }

    public WorldRuntimeContext? World { get; internal set; }

    public WorldConnection(byte id = 0, Guid characterId = default, PlayerAddress? playerAddress = null)
    {
        ID = id;
        CharacterId = characterId;
        PlayerAddress = playerAddress;
    }

    public void AssignRoutingId(byte id)
    {
        if (World is not null && ID != id)
        {
            throw new InvalidOperationException("Remove a connection from its world before changing its routing ID.");
        }

        ID = id;
    }

    public void SetControlledCharacter(Guid characterId)
    {
        CharacterId = characterId;
    }

    public void SetPlayerIdentity(PlayerAddress? address)
    {
        _playerAddress = address;
    }

    protected void BindLiveness(IConnectionLiveness? liveness)
    {
        _liveness = liveness;
    }
}
