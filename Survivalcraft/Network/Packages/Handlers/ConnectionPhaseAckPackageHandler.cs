using Game.Network.Enums;

namespace Game.Network.Packages.Handlers;

public sealed class ConnectionPhaseAckPackageHandler : PackageHandlerBase<ConnectionPhaseAckPackage>
{
    public override void Handle(ConnectionPhaseAckPackage package, NetNode? netNode, bool isServer)
    {
        if (!isServer || netNode == null || package.From == null || package.From.ConnectionEpoch != package.Epoch)
        {
            return;
        }

        switch (package.Phase)
        {
            case ConnectionPhase.BootstrapApplied when package.From.ConnectionPhase == ConnectionPhase.BootstrapSent:
                package.From.ConnectionPhase = ConnectionPhase.BootstrapApplied;
                package.From.State = ClientState.ProjectLoaded;
                Log.Debug($"Client[{package.From.ID}]已应用Bootstrap，发送初始世界快照");
                netNode.OnClientBootstrapApplied?.Invoke(package.From);
                break;
            case ConnectionPhase.WorldSnapshotApplied
                when package.From.ConnectionPhase == ConnectionPhase.WorldSnapshotSent:
                package.From.ConnectionPhase = ConnectionPhase.WorldSnapshotApplied;
                package.From.ConnectionPhase = ConnectionPhase.Live;
                Log.Debug($"Client[{package.From.ID}]已应用初始世界快照，进入Live");
                netNode.OnClientBecameLive?.Invoke(package.From);
                break;
        }
    }
}
