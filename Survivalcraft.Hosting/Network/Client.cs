using System.Net;

using Engine.Network;

using EntitySystem.Core;

namespace Game.Network;

public class Client : WorldConnection
{
    // 本地节点的会话记录；尚未控制角色时使用 Guid.Empty。
    public Client(Guid characterId) : base(characterId: characterId)
    {
        State = ClientState.Connected;
        TokenId = Guid.NewGuid();
    }

    /// <summary>
    ///     服务端创建新的Client
    /// </summary>
    /// <param name="netPeer">peer</param>
    /// <param name="id">客户端ID</param>
    /// <param name="tokenId">客户端tokenId</param>
    /// <param name="characterId">当前控制的角色 ID；尚无角色时为 Guid.Empty。</param>
    public Client(
        NetworkConnection? netPeer,
        byte id,
        Guid tokenId,
        Guid characterId
    ) : base(id, characterId, netPeer is null ? null : new PlayerAddress(netPeer.PeerIdentity.Address))
    {
        TokenId = tokenId;
        Peer = netPeer;
    }

    public Client(IPEndPoint iPEndPoint)
    {
        IPPoint = iPEndPoint;
        State = ClientState.NotConnected;
    }

    public Client(NetworkConnectionRequest request) : base(
        playerAddress: new PlayerAddress(request.PeerIdentity.Address)
    )
    {
        Request = request;
        IPPoint = request.TransportEndpoint;
        State = ClientState.NotConnected;
    }

    public Client(NetworkConnection peer) : base(playerAddress: new PlayerAddress(peer.PeerIdentity.Address))
    {
        Peer = peer;
        State = ClientState.NotConnected;
        TokenId = Guid.NewGuid();
    }

    public Guid TokenId { get; internal set; }

    public NetworkConnection? Peer { get; internal set; }

    private IMessageConnection? _connection;

    public IMessageConnection? Connection
    {
        get => _connection;
        internal set
        {
            _connection = value;
            BindLiveness(value);
        }
    }

    internal NetNode? Node { get; set; }

    public NetworkConnectionRequest? Request { get; internal set; }

    public IPEndPoint? IPPoint { get; internal set; }

    public Entity? CachePlayerEntity { get; set; }

    public override string ToString()
    {
        return $"Client : {{ID : {ID}, CharacterId : {CharacterId}}}";
    }
}
