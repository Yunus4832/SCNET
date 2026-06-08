using System.Text;

using EntitySystem.Core;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ConnectionRequestPackage : IPackage
{
    public const int VerifyMagic = 9421523; //校验码

    public string CommunityAccountId = string.Empty;

    public int Magic;

    public string Password = string.Empty;

    public Guid TmpToken;

    public string Token = string.Empty;

    public string User = string.Empty;

    public string Version = string.Empty;

    public List<ModEntity> ModInfos = [];

    public string Nickname = string.Empty;

    public byte ID => (byte)PackageType.ConnectionRequest;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.NotConnected;

    public ConnectionRequestPackage()
    {
    }

    public ConnectionRequestPackage(
        Guid tmpToken,
        string serverVersion,
        string user,
        string token,
        string passwd,
        List<ModEntity> modEntities
    )
    {
        Magic = VerifyMagic;
        TmpToken = tmpToken;
        User = user;
        Token = token;
        Version = serverVersion;
        Password = passwd;
        ModInfos.AddRange(modEntities);
    }


    public void ReadData(PackageStreamReader reader)
    {
        Magic = reader.ReadInt32();
        Version = reader.ReadString();
        Password = reader.ReadString();
        User = reader.ReadString();
        TmpToken = reader.ReadGuid();
        Token = reader.ReadString();
        var count = reader.ReadByte();
        ModInfos = [];
        for (var i = 0; i < count; i++)
        {
            var modEntity = new ModEntity
            {
                ResourcesMd5 = reader.ReadString(),
                ModInfo = new ModInfo
                {
                    Name = reader.ReadString(),
                    PackageName = reader.ReadString(),
                    Version = reader.ReadString()
                }
            };
            ModInfos.Add(modEntity);
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(Password);
        writer.Write(User);
        writer.Write(TmpToken);
        writer.Write(Token);
        writer.Write((byte)ModInfos.Count);
        foreach (var m in ModInfos)
        {
            writer.Write(m.ResourcesMd5);
            writer.Write(m.ModInfo.Name);
            writer.Write(m.ModInfo.PackageName);
            writer.Write(m.ModInfo.Version);
        }
    }

    public void AcceptClient(
        Project project,
        string token,
        NetNode netNode,
        StringBuilder connectionError,
        bool useExternalPassword = false,
        string saveKey = ""
    )
    {
        var guid = Guid.Empty;
        if (!string.IsNullOrEmpty(token))
        {
            guid = new Guid(token);
        }

        var pwd2 = project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.Password;
        var mList = ModsManager.ModList;
        foreach (var info in ModInfos)
        {
            var find = mList.Find(x => x.Equals(info));
            if (find == null)
            {
                connectionError.AppendLine($"服务器没有安装客户端Mod[{info.ModInfo.Name}:{info.ModInfo.Version}]");
            }
            else
            {
                if (project is { SendToClientMode: true })
                {
                    continue;
                }

                if (find.ResourcesMd5 == info.ResourcesMd5 || info.ModInfo.PackageName == "survivalcraft")
                {
                    continue;
                }

                connectionError.AppendLine($"资源包{info.ModInfo.Name}校验不通过");
                connectionError.AppendLine($"[服务端]{find.ResourcesMd5}");
                connectionError.AppendLine($"[客户端]{info.ResourcesMd5}");
            }
        }

        foreach (var item in mList)
        {
            var find = ModInfos.Find(x => x.Equals(item));
            if (find == null)
            {
                connectionError.AppendLine($"客户端没有安装服务器Mod[{item.ModInfo.Name}:{item.ModInfo.Version}]");
            }
        }

        if (Magic == VerifyMagic)
        {
            if (CommonLib.Net.Self?.GUID == guid)
            {
                connectionError.AppendLine("客户端和服务器token相同");
            }
            else if (ModInfos.Count != mList.Count)
            {
                connectionError.AppendLine("客户端Mod数量和服务器Mod数量不一致");
                if (mList.Count > 0)
                {
                    var i = 1;
                    foreach (var m in mList)
                    {
                        connectionError.AppendLine($"[服务器]{i++}.{m.ModInfo.Name}:{m.ModInfo.Version}");
                    }
                }
                else
                {
                    connectionError.AppendLine("[服务器]无");
                }

                if (ModInfos.Count > 0)
                {
                    var i = 1;
                    foreach (var m in ModInfos)
                    {
                        connectionError.AppendLine($"[客户端]{i++}.{m.ModInfo.Name}:{m.ModInfo.Version}");
                    }
                }
                else
                {
                    connectionError.AppendLine("[客户端]无");
                }
            }
            else if (netNode.Peers.FirstOrDefault(c => c != From && (c.GUID == guid || c.TokenId == TmpToken)) !=
                     null)
            {
                connectionError.AppendLine("你的ID与服务器中某个在线玩家的ID相同");
            }
            else if (Version != VersionsManager.ProtocolVersion)
            {
                connectionError.AppendLine("客户端和服务器版本不一致");
            }
            else if (project.FindSubsystem<SubsystemPlayers>(true)!.BlackPlayerGuidList.ContainsKey(token))
            {
                connectionError.AppendLine("你已被禁止加入服务器");
            }
            else if (netNode.ClientCount >= project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.MaxOnlinePlayerCount)
            {
                connectionError.AppendLine("在线人数达到最大，拒绝加入");
            }
            else if (!string.IsNullOrEmpty(pwd2))
            {
                if (!useExternalPassword && Password != pwd2)
                {
                    connectionError.AppendLine("房间密码验证错误");
                }
            }
            else if (string.IsNullOrEmpty(token))
            {
                connectionError.AppendLine("身份信息验证错误");
            }

            if (connectionError.Length > 0)
            {
                ModsManager.Configs.Remove(saveKey); //如果验证失败应该清理登录信息
                if (From == null || From.Request == null)
                {
                    return;
                }

                netNode.SendWriterFromPackage(new ConnectionRejectPackage(connectionError.ToString()),
                    From.Request,
                    true);
                Log.Information("Received connection request from " + From.IPPoint + ", rejected -- " +
                                connectionError);
            }
            else
            {
                if (From == null || From.Request == null)
                {
                    return;
                }

                netNode.PendingPeer = From.Request.Accept();
                var addClient =
                    netNode.CreateClient(netNode.PendingPeer, TmpToken, guid, CommunityAccountId, Nickname);
                var clientPackage = new ClientPackage(addClient.ID, addClient.TokenId, addClient.GUID,
                    addClient.CommunityAccountId, addClient.Nickname);
                foreach (var c in netNode.Peers)
                {
                    netNode.AgreeOnPendingPeer.Add(c.ID);
                    netNode.SendWriterFromPackage(clientPackage, c.Peer, true);
                }

                Log.Information("Received connection request from " + From.IPPoint + ", accepted");

                netNode.DeliveryEvent(null, null);
            }
        }
        else
        {
            if (From == null || From.Request == null)
            {
                return;
            }

            netNode.SendWriterFromPackage(new ConnectionRejectPackage("无法识别的数据包头"), From.Request, true);
            Log.Information("无法识别的数据包头");
        }
    }
}
