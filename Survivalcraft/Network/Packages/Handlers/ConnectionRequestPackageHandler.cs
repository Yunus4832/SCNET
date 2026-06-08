using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using EntitySystem.Core;

using Game.ContentProviders;
using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ConnectionRequestPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        var connectionError = new StringBuilder();
        if (string.IsNullOrEmpty(Token))
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
            var key = ModsManager.GetMd5(User + "/" + Token);
            var saveKey = "loginCache" + key;
            if (ModsManager.Configs.TryGetValue(saveKey, out var v))
            {
                const bool useExternalPassword = false;
                var jsonObj = JsonSerializer.Deserialize<JsonObject>(v)!;
                var dataInfo = (jsonObj["data"] as JsonObject)!;
                Nickname = dataInfo["nickname"]?.ToString() ?? string.Empty;
                CommunityAccountId = dataInfo["id"]?.ToString() ?? string.Empty;
                token = dataInfo["token"]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(Nickname))
                {
                    Nickname = "Anonymous_" + new LcgRandom().Int(10000, 99999);
                }

                if (From != null)
                {
                    Log.Information(
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}[{From.IPPoint}]用户[缓存]登录名称[{Nickname}]，社区ID[{CommunityAccountId}]");
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
                    { "user", User }, { "token", key }, { "client_ip", From.IPPoint.ToString() },
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
                            Nickname = dataInfo["nickname"]?.ToString() ?? string.Empty;
                            CommunityAccountId = dataInfo["id"]?.ToString() ?? string.Empty;
                            token = dataInfo["token"]?.ToString() ?? string.Empty;
                            if (string.IsNullOrEmpty(Nickname))
                            {
                                Nickname = "Anonymous_" + new LcgRandom().Int(10000, 99999);
                            }

                            Log.Information(
                                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}[{From.IPPoint}]用户登录名称[{Nickname}]，社区ID[{CommunityAccountId}]");
                        }

                        AcceptClient(project, token, netNode, connectionError, useExternalPassword, saveKey);
                    }, e => { Log.Information($"用户[{User}]验证失败:{e.Message}"); });
            }
        }
        else
        {
            AcceptClient(project, Token, netNode, connectionError);
        }
    }
}

public sealed class ConnectionRequestPackageHandler : PackageHandlerBase<ConnectionRequestPackage>
{
    public override void Handle(ConnectionRequestPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ConnectionRequestPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
