using Game.Network.Enums;

namespace Game.Network.Packages.Handlers;

public sealed class ConnectionPhaseAckPackageHandler : PackageHandlerBase<ConnectionPhaseAckPackage>
{
    public override void Handle(ConnectionPhaseAckPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (!isServer || netNode == null || context.Sender == null || context.Sender.ConnectionEpoch != package.Epoch)
        {
            return;
        }

        switch (package.Phase)
        {
            case ConnectionPhase.BootstrapApplied when context.Sender.ConnectionPhase == ConnectionPhase.BootstrapSent:
                context.Sender.ConnectionPhase = ConnectionPhase.BootstrapApplied;
                context.Sender.State = ClientState.ProjectLoaded;
                Log.Debug($"Client[{context.Sender.ID}]已应用Bootstrap，发送初始世界快照");
                netNode.OnClientBootstrapApplied?.Invoke(context.Sender);
                break;
            case ConnectionPhase.WorldSnapshotApplied
                when context.Sender.ConnectionPhase == ConnectionPhase.WorldSnapshotSent:
                context.Sender.ConnectionPhase = ConnectionPhase.WorldSnapshotApplied;
                context.Sender.ConnectionPhase = ConnectionPhase.Live;
                Log.Debug($"Client[{context.Sender.ID}]已应用初始世界快照，进入Live");
                netNode.OnClientBecameLive?.Invoke(context.Sender);
                break;
        }
    }
}
