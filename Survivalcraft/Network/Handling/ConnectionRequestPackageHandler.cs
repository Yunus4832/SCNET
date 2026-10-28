using Engine.Network;

using EntitySystem.Core;

using Game.Admission;
using Game.Network.Transport;
using Game.Protocol.Identity;

namespace Game.Network.Handling;

public sealed class ConnectionRequestPackageHandler : PackageHandlerBase<ConnectionRequestPackage>
{
    public override void Handle(ConnectionRequestPackage package, PackageReceiveContext context)
    {
        if (context.Node is not { } node ||
            context.Sender?.Request is not { } request ||
            GameManager.Project is not { } project)
        {
            return;
        }

        if (!context.IsServer)
        {
            request.Reject(NetNode.CreateUnconnectedPayload(new ConnectionRejectPackage("当前节点不是游戏服务器")));
            return;
        }

        // The subject comes exclusively from the completed DTLS proof, never from application payloads.
        var address = new PlayerAddress(request.PeerIdentity.Address);
        var error = AdmissionError(package, project, node, address);
        if (error is not null)
        {
            request.Reject(NetNode.CreateUnconnectedPayload(new ConnectionRejectPackage(error)));
            Log.Information($"Rejected connection request from {request.TransportEndpoint}: {error}");
            return;
        }

        if (node.AdmissionController is not { } admission)
        {
            request.Reject(NetNode.CreateUnconnectedPayload(new ConnectionRejectPackage("服务器认证尚未准备完成")));
            return;
        }

        admission.Begin(request, package);
    }

    internal static void CompleteAdmission(
        ConnectionRequestPackage package,
        Project project,
        NetNode node,
        NetworkConnection peer,
        string nonce
    )
    {
        var address = new PlayerAddress(peer.PeerIdentity.Address);
        var error = AdmissionError(package, project, node, address);
        if (error is not null)
        {
            node.SendWriterFromPackage(new ConnectionRejectPackage(error), peer);
            node.RemoveClient(node.ResolveSender(peer: peer), error);
            return;
        }

        if (!PlayerAdmission.TryCreateSession(
                project,
                node,
                address,
                package.TmpToken,
                character => node.CreateClient(peer, package.TmpToken, character),
                out var admitted,
                out error
            ))
        {
            node.SendWriterFromPackage(new ConnectionRejectPackage(error!), peer);
            node.RemoveClient(node.ResolveSender(peer: peer), error!);
            return;
        }

        var client = admitted!;
        node.SendWriterFromPackage(new AdmissionResultPackage(nonce, AdmissionResultCode.Accepted), peer);
        node.PendingPeer = peer;
        var notification = new ClientAddedPackage
        {
            Client = new ClientIdentity(client.ID, client.TokenId, client.CharacterId, client.PlayerAddress)
        };
        foreach (var other in node.Peers)
        {
            node.AgreeOnPendingPeer.Add(other.ID);
            node.SendWriterFromPackage(notification, other.Peer, true);
        }

        node.DeliveryEvent(null);
    }

    private static string? AdmissionError(
        ConnectionRequestPackage package,
        Project project,
        NetNode node,
        PlayerAddress address
    )
    {
        if (package.Magic != GameProtocol.ConnectionMagic) { return "无法识别的数据包头"; }

        if (package.ProtocolVersion != VersionsManager.ProtocolVersion) { return "客户端和服务器协议版本不一致"; }

        var hash = CurrentModRuntime.Value?.ModDataHash ?? ModProfileManager.EmptyDataHash;
        if (!string.Equals(package.ModDataHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            return "客户端模组与服务器不一致，请刷新服务器信息后重试";
        }

        return PlayerAdmission.Rejection(project, node, address, package.TmpToken);
    }
}
