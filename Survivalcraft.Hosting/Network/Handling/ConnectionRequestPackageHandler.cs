using Engine.Network;

using EntitySystem.Core;

using Game.Admission;

namespace Game.Network.Handling;

public sealed class ConnectionRequestPackageHandler(string modDataHash) : PackageHandlerBase<ConnectionRequestPackage>
{
    public override void Handle(ConnectionRequestPackage package, PackageReceiveContext context)
    {
        if (context.Node is not { } node ||
            context.Sender?.Request is not { } request ||
            context.Project is not { } project)
        {
            return;
        }

        if (!context.IsServer)
        {
            request.Reject(
                NetNode.CreateUnconnectedPayload(
                    new ConnectionRejectPackage(
                        ConnectionMessages.Create("NotGameServer", "This endpoint is not a game server")
                    )
                )
            );
            return;
        }

        // The subject comes exclusively from the completed DTLS proof, never from application payloads.
        var address = new PlayerAddress(request.PeerIdentity.Address);
        var error = AdmissionError(package, project, node, address);
        if (error is not null)
        {
            request.Reject(NetNode.CreateUnconnectedPayload(new ConnectionRejectPackage(error)));
            Log.Information(
                $"Rejected connection request from {request.TransportEndpoint}: {error.Section}.{error.Key}"
            );
            return;
        }

        if (node.AdmissionController is not { } admission)
        {
            request.Reject(
                NetNode.CreateUnconnectedPayload(
                    new ConnectionRejectPackage(
                        ConnectionMessages.Create("AuthenticationNotReady", "Server authentication is not ready")
                    )
                )
            );
            return;
        }

        admission.Begin(request, package);
    }

    internal void CompleteAdmission(
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
        node.BeginClientBootstrap(client);
    }

    private LocalizedText? AdmissionError(
        ConnectionRequestPackage package,
        Project project,
        NetNode node,
        PlayerAddress address
    )
    {
        if (package.Magic != GameProtocol.ConnectionMagic)
        {
            return ConnectionMessages.Create("InvalidHeader", "Unrecognized connection request");
        }

        if (package.ProtocolVersion != GameProtocol.CurrentVersion)
        {
            return ConnectionMessages.Create("ProtocolMismatch", "Client and server protocol versions do not match");
        }

        var hash = modDataHash;
        if (!string.Equals(package.ModDataHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionMessages.Create(
                "ModsMismatch",
                "Client and server mods do not match. Refresh server information and try again."
            );
        }

        return PlayerAdmission.Rejection(project, node, address, package.TmpToken);
    }
}
