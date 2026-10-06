using System.Text;

using EntitySystem.Core;

namespace Game.Network.Packages.Handlers;

public sealed class ConnectionRequestPackageHandler : PackageHandlerBase<ConnectionRequestPackage>
{
    public override void Handle(ConnectionRequestPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ConnectionRequestPackage)}");
            return;
        }

        var connectionError = new StringBuilder();
        if (package.MultiplayerClientId == Guid.Empty)
        {
            if (context.Sender == null)
            {
                return;
            }

            if (context.Sender.Request != null)
            {
                netNode.SendWriterFromPackage(
                    new ConnectionRejectPackage("联机客户端 ID 无效"),
                    context.Sender.Request,
                    true);
            }

            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        AcceptClient(package, GameManager.Project, netNode, connectionError, context);
    }

    private static void AcceptClient(
        ConnectionRequestPackage package,
        Project project,
        NetNode netNode,
        StringBuilder connectionError,
        PackageReceiveContext context
    )
    {
        var multiplayerClientId = package.MultiplayerClientId;
        var serverModDataHash = CurrentModRuntime.Value?.ModDataHash ?? ModProfileManager.EmptyDataHash;

        if (package.Magic == ConnectionRequestPackage.VerifyMagic)
        {
            if (multiplayerClientId == Guid.Empty)
            {
                connectionError.AppendLine("联机客户端 ID 无效");
            }
            else if (CommonLib.Net.Self?.GUID == multiplayerClientId)
            {
                connectionError.AppendLine("客户端和服务器使用了相同的联机客户端 ID");
            }
            else if (netNode.Peers.FirstOrDefault(c =>
                         !ReferenceEquals(c, context.Sender) &&
                         (c.GUID == multiplayerClientId || c.TokenId == package.TmpToken)) != null)
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
            else if (netNode.ClientCount >=
                     project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.MaxOnlinePlayerCount)
            {
                connectionError.AppendLine("在线人数达到最大，拒绝加入");
            }

            if (connectionError.Length > 0)
            {
                if (context.Sender == null || context.Sender.Request == null)
                {
                    return;
                }

                netNode.SendWriterFromPackage(
                    new ConnectionRejectPackage(connectionError.ToString()),
                    context.Sender.Request,
                    true);
                Log.Information("Received connection request from " + context.Sender.IPPoint + ", rejected -- " +
                                connectionError);
            }
            else
            {
                if (context.Sender == null || context.Sender.Request == null)
                {
                    return;
                }

                netNode.PendingPeer = context.Sender.Request.Accept();
                var addClient = netNode.CreateClient(
                    netNode.PendingPeer,
                    package.TmpToken,
                    multiplayerClientId);
                var clientPackage = new ClientPackage(addClient.ID, addClient.TokenId, addClient.GUID);
                foreach (var c in netNode.Peers)
                {
                    netNode.AgreeOnPendingPeer.Add(c.ID);
                    netNode.SendWriterFromPackage(clientPackage, c.Peer, true);
                }

                Log.Information("Received connection request from " + context.Sender.IPPoint + ", accepted");

                netNode.DeliveryEvent(null, null);
            }
        }
        else
        {
            if (context.Sender == null || context.Sender.Request == null)
            {
                return;
            }

            netNode.SendWriterFromPackage(new ConnectionRejectPackage("无法识别的数据包头"), context.Sender.Request, true);
            Log.Information("无法识别的数据包头");
        }
    }
}
