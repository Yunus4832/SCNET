using System.Net;

using Engine.Network;

using EntitySystem.Core;

using Game.Players;
using Game.Protocol.Identity;

namespace Game.Network;

public class Client
{
    // 本地节点的会话记录；尚未控制角色时使用 Guid.Empty。
    public Client(Guid characterId, Project project)
    {
        CharacterId = characterId;
        Project = project;
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
    /// <param name="project">Project</param>
    public Client(
        NetworkConnection? netPeer,
        byte id,
        Guid tokenId,
        Guid characterId,
        Project? project
    )
    {
        ID = id;
        CharacterId = characterId;
        TokenId = tokenId;
        Project = project;
        Peer = netPeer;
        PlayerAddress = netPeer is null ? null : new PlayerAddress(netPeer.PeerIdentity.Address);
        IsAddedToNetNode = true;
    }

    public Client(IPEndPoint iPEndPoint)
    {
        IPPoint = iPEndPoint;
        State = ClientState.NotConnected;
    }

    public Client(NetworkConnectionRequest request)
    {
        Request = request;
        PlayerAddress = new PlayerAddress(request.PeerIdentity.Address);
        IPPoint = request.TransportEndpoint;
        State = ClientState.NotConnected;
    }

    public Client(NetworkConnection peer)
    {
        Peer = peer;
        PlayerAddress = new PlayerAddress(peer.PeerIdentity.Address);
        State = ClientState.NotConnected;
        TokenId = Guid.NewGuid();
    }

    public bool IsLocalRemote { get; set; }

    public Guid SessionId { get; } = Guid.NewGuid();

    public byte ID { get; internal set; }

    public Guid CharacterId { get; internal set; }

    public PlayerAddress? PlayerAddress { get; internal set; }

    public Guid TokenId { get; internal set; }

    public bool Granted { get; internal set; }

    public NetworkConnection? Peer { get; internal set; }

    public NetworkConnectionRequest? Request { get; internal set; }

    public IPEndPoint? IPPoint { get; internal set; }

    private Project? Project { get; set; }

    public bool IsConnected => Peer != null && Peer.IsConnected;

    public ClientState State { get; set; }

    public ConnectionPhase ConnectionPhase { get; set; }

    public Guid ConnectionEpoch { get; set; }

    public WorldPlayer? Subject => PlayerAddress is { } address
        ? Project?.FindSubsystem<SubsystemPlayers>(false)?.Store.Find(address)
        : null;

    public CharacterData? CharacterData => CharacterId == Guid.Empty
        ? null
        : Project?.FindSubsystem<SubsystemCharacters>(false)
            ?.FindCharacterData(player => player.CharacterId == CharacterId);

    public Entity? CachePlayerEntity { get; set; }

    /// <summary>
    ///     是否在连接成功列表
    /// </summary>
    public bool IsAddedToNetNode { get; private set; }

    public void SetProject(Project project)
    {
        Project = project;
    }

    public override bool Equals(object? obj)
    {
        return obj is Client c && c.SessionId == SessionId;
    }

    public override int GetHashCode()
    {
        return SessionId.GetHashCode();
    }

    public static bool operator ==(Client? a, Client? b)
    {
        return a is null ? b is null : a.Equals(b);
    }

    public static bool operator !=(Client? a, Client? b)
    {
        return !(a == b);
    }

    public override string ToString()
    {
        return $"Client : {{ID : {ID}, CharacterId : {CharacterId}}}";
    }
}
