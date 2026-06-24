using System.Net;

using EntitySystem.Core;

using Game.Network.Enums;

using LiteNetLib;

namespace Game.Network;

public class Client
{
    // 服务器本身构造
    public Client(Guid guid, Project project)
    {
        GUID = guid;
        Project = project;
        State = ClientState.Connected;
        TokenId = Guid.NewGuid();
    }

    /// <summary>
    /// 服务端创建新的Client
    /// </summary>
    /// <param name="netPeer">peer</param>
    /// <param name="id">客户端ID</param>
    /// <param name="tokenId">客户端tokenId</param>
    /// <param name="guid">客户端Guid</param>
    /// <param name="project">Project</param>
    /// <param name="dataId">社区账号ID</param>
    /// <param name="nickName">社区昵称</param>
    public Client(
        NetPeer? netPeer,
        byte id,
        Guid tokenId,
        Guid guid,
        Project? project,
        string dataId,
        string nickName
    )
    {
        ID = id;
        GUID = guid;
        TokenId = tokenId;
        Project = project;
        Nickname = nickName;
        CommunityAccountId = dataId;
        Peer = netPeer;
        IsAddedToNetNode = true;
    }

    public Client(IPEndPoint iPEndPoint)
    {
        IPPoint = iPEndPoint;
        State = ClientState.NotConnected;
    }

    public Client(ConnectionRequest request)
    {
        Request = request;
        IPPoint = request.RemoteEndPoint;
        State = ClientState.NotConnected;
    }

    public Client(NetPeer peer)
    {
        Peer = peer;
        State = ClientState.NotConnected;
        TokenId = Guid.NewGuid();
    }

    public bool IsLocalRemote { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public byte ID { get; internal set; }

    public Guid GUID { get; }

    public Guid TokenId { get; internal set; }

    public bool Granted { get; internal set; }

    public NetPeer? Peer { get; internal set; }

    public ConnectionRequest? Request { get; internal set; }

    public IPEndPoint? IPPoint { get; internal set; }

    private Project? Project { get; set; }

    private SubsystemPlayers SubsystemPlayers { get; set; } = null!;

    public bool IsConnected => Peer != null && Peer.ConnectionState != ConnectionState.Disconnected;

    public string CommunityAccountId { get; set; } = "-1";

    public ClientState State { get; set; }

    public PlayerData PlayerData
    {
        get { return SubsystemPlayers.PlayersData.Find(p => p.ClientId == ID)!; }
    }

    public Entity? CachePlayerEntity { get; set; }

    /// <summary>
    /// 是否在连接成功列表
    /// </summary>
    public bool IsAddedToNetNode { get; private set; }

    public void SetProject(Project project)
    {
        Project = project;
        SubsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
    }

    public override bool Equals(object? obj)
    {
        return obj is Client c && c.ID == ID;
    }

    public override int GetHashCode()
    {
        return GUID.GetHashCode();
    }

    public static bool operator ==(Client? a, Client? b)
    {
        return (a is null && b is null) || (a is not null && b is not null && a.ID == b.ID);
    }

    public static bool operator !=(Client? a, Client? b)
    {
        return !(a == b);
    }

    public override string ToString()
    {
        return $"Client : {{ID : {ID}, GUID : {GUID}}}";
    }
}
