using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using EntitySystem.Core;

using Game.ContentProviders;

namespace Game.Network.Packages.Handlers;

public sealed class ConnectionRequestPackageHandler : PackageHandlerBase<ConnectionRequestPackage>
{
    public override void Handle(ConnectionRequestPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ConnectionRequestPackage)}");
            return;
        }

        var connectionError = new StringBuilder();
        if (string.IsNullOrEmpty(package.Token))
        {
            if (package.From == null)
            {
                return;
            }

            if (package.From.Request != null)
            {
                netNode.SendWriterFromPackage(new ConnectionRejectPackage("身份信息为空，验证失败"), package.From.Request, true);
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
            var key = HashUtils.ComputeMd5(package.User + "/" + package.Token);
            var saveKey = "loginCache" + key;
            if (AppConfigStore.Values.TryGetValue(saveKey, out var v))
            {
                const bool useExternalPassword = false;
                var jsonObj = JsonSerializer.Deserialize<JsonObject>(v)!;
                var dataInfo = (jsonObj["data"] as JsonObject)!;
                package.Nickname = dataInfo["nickname"]?.ToString() ?? string.Empty;
                package.CommunityAccountId = dataInfo["id"]?.ToString() ?? string.Empty;
                token = dataInfo["token"]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(package.Nickname))
                {
                    package.Nickname = "Anonymous_" + new LcgRandom().Int(10000, 99999);
                }

                if (package.From != null)
                {
                    Log.Information(
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}[{package.From.IPPoint}]用户[缓存]登录名称[{package.Nickname}]，社区ID[{package.CommunityAccountId}]");
                }

                AcceptClient(package, project, token, netNode, connectionError, useExternalPassword, saveKey);
            }
            else
            {
                var header = new Dictionary<string, string> { { "Content-Type", "application/x-www-form-urlencoded" } };
                if (package.From is not { IPPoint: not null })
                {
                    return;
                }

                var postData = WebManager.UrlParametersToStream(new Dictionary<string, string>
                {
                    { "user", package.User }, { "token", key }, { "client_ip", package.From.IPPoint.ToString() },
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
                            AppConfigStore.Values[saveKey] = ret;
                            var dataInfo = (jsonObj["data"] as JsonObject)!;
                            package.Nickname = dataInfo["nickname"]?.ToString() ?? string.Empty;
                            package.CommunityAccountId = dataInfo["id"]?.ToString() ?? string.Empty;
                            token = dataInfo["token"]?.ToString() ?? string.Empty;
                            if (string.IsNullOrEmpty(package.Nickname))
                            {
                                package.Nickname = "Anonymous_" + new LcgRandom().Int(10000, 99999);
                            }

                            Log.Information(
                                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}[{package.From.IPPoint}]用户登录名称[{package.Nickname}]，社区ID[{package.CommunityAccountId}]");
                        }

                        AcceptClient(package, project, token, netNode, connectionError, useExternalPassword, saveKey);
                    }, e => { Log.Information($"用户[{package.User}]验证失败:{e.Message}"); });
            }
        }
        else
        {
            AcceptClient(package, project, package.Token, netNode, connectionError);
        }
    }

    private static void AcceptClient(
        ConnectionRequestPackage package,
        Project project,
        string token,
        NetNode netNode,
        StringBuilder connectionError,
        bool useExternalPassword = false,
        string saveKey = ""
    )
    {
        var hasValidToken = Guid.TryParse(token, out var guid);

        var pwd2 = project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.Password;
        var serverModDataHash = CurrentModRuntime.Value?.ModDataHash ?? ModProfileManager.EmptyDataHash;

        if (package.Magic == ConnectionRequestPackage.VerifyMagic)
        {
            if (!hasValidToken)
            {
                connectionError.AppendLine("身份信息验证错误");
            }
            else if (CommonLib.Net.Self?.GUID == guid)
            {
                connectionError.AppendLine("客户端和服务器token相同");
            }
            else if (netNode.Peers.FirstOrDefault(c =>
                         c != package.From && (c.GUID == guid || c.TokenId == package.TmpToken)) != null)
            {
                connectionError.AppendLine("你的ID与服务器中某个在线玩家的ID相同");
            }
            else if (package.Version != VersionsManager.ProtocolVersion)
            {
                connectionError.AppendLine("客户端和服务器版本不一致");
            }
            else if (!string.Equals(package.ModDataHash, serverModDataHash, StringComparison.OrdinalIgnoreCase))
            {
                connectionError.AppendLine("客户端模组与服务器不一致，请刷新服务器信息后重试");
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
                if (!useExternalPassword && package.Password != pwd2)
                {
                    connectionError.AppendLine("房间密码验证错误");
                }
            }
            if (connectionError.Length > 0)
            {
                AppConfigStore.Values.Remove(saveKey); // 如果验证失败应该清理登录信息
                if (package.From == null || package.From.Request == null)
                {
                    return;
                }

                netNode.SendWriterFromPackage(
                    new ConnectionRejectPackage(connectionError.ToString()),
                    package.From.Request,
                    true);
                Log.Information("Received connection request from " + package.From.IPPoint + ", rejected -- " +
                                connectionError);
            }
            else
            {
                if (package.From == null || package.From.Request == null)
                {
                    return;
                }

                netNode.PendingPeer = package.From.Request.Accept();
                var addClient = netNode.CreateClient(
                    netNode.PendingPeer,
                    package.TmpToken,
                    guid,
                    package.CommunityAccountId,
                    package.Nickname);
                var clientPackage = new ClientPackage(addClient.ID, addClient.TokenId, addClient.GUID,
                    addClient.CommunityAccountId, addClient.Nickname);
                foreach (var c in netNode.Peers)
                {
                    netNode.AgreeOnPendingPeer.Add(c.ID);
                    netNode.SendWriterFromPackage(clientPackage, c.Peer, true);
                }

                Log.Information("Received connection request from " + package.From.IPPoint + ", accepted");

                netNode.DeliveryEvent(null, null);
            }
        }
        else
        {
            if (package.From == null || package.From.Request == null)
            {
                return;
            }

            netNode.SendWriterFromPackage(new ConnectionRejectPackage("无法识别的数据包头"), package.From.Request, true);
            Log.Information("无法识别的数据包头");
        }
    }
}
