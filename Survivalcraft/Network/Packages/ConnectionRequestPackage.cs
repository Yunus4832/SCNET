using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using EntitySystem.Core;

using Game.ContentProviders;
using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ConnectionRequestPackage : IPackage
{
    private const int _verifyMagic = 9421523; //校验码

    private string _communityAccountId = string.Empty;

    private int _magic;

    private string _password = string.Empty;

    private Guid _tmpToken;

    private string _token = string.Empty;

    private string _user = string.Empty;

    private string _version = string.Empty;

    private List<ModEntity> _modInfos = [];

    private string _nickname = string.Empty;

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
        _magic = _verifyMagic;
        _tmpToken = tmpToken;
        _user = user;
        _token = token;
        _version = serverVersion;
        _password = passwd;
        _modInfos.AddRange(modEntities);
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        var connectionError = new StringBuilder();
        if (string.IsNullOrEmpty(_token))
        {
            if (From == null)
            {
                return;
            }

            if (From.Request != null)
            {
                netNode.SendWriterFromPackage(new ConnectionRejectPackage("身份信息为空，验证失败"), From.Request, true);
            }

            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;

        if (project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.IsNeedCommunityLogin)
        {
            var token = string.Empty;
            var key = ModsManager.GetMd5(_user + "/" + _token);
            var saveKey = "loginCache" + key;
            if (ModsManager.Configs.TryGetValue(saveKey, out var v))
            {
                const bool useExternalPassword = false;
                var jsonObj = JsonSerializer.Deserialize<JsonObject>(v)!;
                var dataInfo = (jsonObj["data"] as JsonObject)!;
                _nickname = dataInfo["nickname"]?.ToString() ?? string.Empty;
                _communityAccountId = dataInfo["id"]?.ToString() ?? string.Empty;
                token = dataInfo["token"]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(_nickname))
                {
                    _nickname = "Anonymous_" + new LcgRandom().Int(10000, 99999);
                }

                if (From != null)
                {
                    Log.Information(
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}[{From.IPPoint}]用户[缓存]登录名称[{_nickname}]，社区ID[{_communityAccountId}]");
                }

                AcceptClient(project, token, netNode, connectionError, useExternalPassword, saveKey);
            }
            else
            {
                var header = new Dictionary<string, string> { { "Content-Type", "application/x-www-form-urlencoded" } };
                if (From is not { IPPoint: not null })
                {
                    return;
                }

                var postData = WebManager.UrlParametersToStream(new Dictionary<string, string>
                {
                    { "user", _user }, { "token", key }, { "client_ip", From.IPPoint.ToString() },
                    { "server_ip", CommonLib.GetInnerIp() }
                });
                WebManager.Post(
                    SchubExternalContentProvider.GetPath("/com/checkUser_t"),
                    new Dictionary<string, string>(),
                    header,
                    postData,
                    new CancellableProgress(),
                    data =>
                    {
                        const bool useExternalPassword = false;
                        var ret = Encoding.UTF8.GetString(data);
                        var jsonObj = JsonSerializer.Deserialize<JsonObject>(ret)!;
                        if (jsonObj["code"]?.ToString() != "200")
                        {
                            connectionError.AppendLine("用户校验失败，请重新登录社区");
                        }
                        else
                        {
                            //缓存信息
                            ModsManager.Configs[saveKey] = ret;
                            var dataInfo = (jsonObj["data"] as JsonObject)!;
                            _nickname = dataInfo["nickname"]?.ToString() ?? string.Empty;
                            _communityAccountId = dataInfo["id"]?.ToString() ?? string.Empty;
                            token = dataInfo["token"]?.ToString() ?? string.Empty;
                            if (string.IsNullOrEmpty(_nickname))
                            {
                                _nickname = "Anonymous_" + new LcgRandom().Int(10000, 99999);
                            }

                            Log.Information(
                                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}[{From.IPPoint}]用户登录名称[{_nickname}]，社区ID[{_communityAccountId}]");
                        }

                        AcceptClient(project, token, netNode, connectionError, useExternalPassword, saveKey);
                    }, e => { Log.Information($"用户[{_user}]验证失败:{e.Message}"); });
            }
        }
        else
        {
            AcceptClient(project, _token, netNode, connectionError);
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _magic = reader.ReadInt32();
        _version = reader.ReadString();
        _password = reader.ReadString();
        _user = reader.ReadString();
        _tmpToken = reader.ReadGuid();
        _token = reader.ReadString();
        var count = reader.ReadByte();
        _modInfos = [];
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
            _modInfos.Add(modEntity);
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_magic);
        writer.Write(_version);
        writer.Write(_password);
        writer.Write(_user);
        writer.Write(_tmpToken);
        writer.Write(_token);
        writer.Write((byte)_modInfos.Count);
        foreach (var m in _modInfos)
        {
            writer.Write(m.ResourcesMd5);
            writer.Write(m.ModInfo.Name);
            writer.Write(m.ModInfo.PackageName);
            writer.Write(m.ModInfo.Version);
        }
    }

    private void AcceptClient(
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
        foreach (var info in _modInfos)
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
            var find = _modInfos.Find(x => x.Equals(item));
            if (find == null)
            {
                connectionError.AppendLine($"客户端没有安装服务器Mod[{item.ModInfo.Name}:{item.ModInfo.Version}]");
            }
        }

        if (_magic == _verifyMagic)
        {
            if (CommonLib.Net.Self?.GUID == guid)
            {
                connectionError.AppendLine("客户端和服务器token相同");
            }
            else if (_modInfos.Count != mList.Count)
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

                if (_modInfos.Count > 0)
                {
                    var i = 1;
                    foreach (var m in _modInfos)
                    {
                        connectionError.AppendLine($"[客户端]{i++}.{m.ModInfo.Name}:{m.ModInfo.Version}");
                    }
                }
                else
                {
                    connectionError.AppendLine("[客户端]无");
                }
            }
            else if (netNode.Peers.FirstOrDefault(c => c != From && (c.GUID == guid || c.TokenId == _tmpToken)) !=
                     null)
            {
                connectionError.AppendLine("你的ID与服务器中某个在线玩家的ID相同");
            }
            else if (_version != VersionsManager.ProtocolVersion)
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
                if (!useExternalPassword && _password != pwd2)
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
                    netNode.CreateClient(netNode.PendingPeer, _tmpToken, guid, _communityAccountId, _nickname);
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
