using Game.Network.Enums;

namespace Game.Network.Packages.Handlers;

public sealed class InitialWorldSnapshotPackageHandler : PackageHandlerBase<InitialWorldSnapshotPackage>
{
    public override void Handle(InitialWorldSnapshotPackage package, NetNode? netNode, bool isServer)
    {
        if (isServer || netNode == null || GameManager.Project == null || package.Epoch != netNode.ConnectionEpoch)
        {
            return;
        }

        var project = GameManager.Project;
        package.ClientList.From = package.From;
        PackageDispatcher.Handle(package.ClientList, netNode, false);
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        foreach (var values in package.Players)
        {
            var guid = values.GetValue("PlayerGUID", Guid.Empty);
            if (subsystemPlayers.PlayersData.Any(player => player.PlayerGUID == guid))
            {
                continue;
            }

            var playerData = new PlayerData(project);
            playerData.Load(values);
            subsystemPlayers.AddPlayerData(playerData);
        }

        project.AddEntities(InitialWorldSnapshotPackage.DeserializeEntities(project, package.EntityData));
        netNode.CurrentConnectionPhase = ConnectionPhase.Live;
        netNode.QueuePackage(new ConnectionPhaseAckPackage(package.Epoch, ConnectionPhase.WorldSnapshotApplied));
        ScreensManager.FindScreen<GameLoadingScreen>("GameLoading", true)!.WorldSnapshotApplied();
    }
}
