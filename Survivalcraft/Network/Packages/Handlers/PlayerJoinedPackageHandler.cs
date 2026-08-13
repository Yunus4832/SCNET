namespace Game.Network.Packages.Handlers;

public sealed class PlayerJoinedPackageHandler : PackageHandlerBase<PlayerJoinedPackage>
{
    public override void Handle(PlayerJoinedPackage package, NetNode? netNode, bool isServer)
    {
        if (isServer || netNode == null || GameManager.Project == null)
        {
            return;
        }

        var project = GameManager.Project;
        if (package.ClientInfo.Client != null &&
            !netNode.Clients.ContainsKey(package.ClientInfo.Client.ID))
        {
            package.ClientInfo.From = package.From;
            PackageDispatcher.Handle(package.ClientInfo, netNode, false);
        }

        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        var guid = package.PlayerData.GetValue("PlayerGUID", Guid.Empty);
        if (subsystemPlayers.PlayersData.All(player => player.PlayerGUID != guid))
        {
            var playerData = new PlayerData(project);
            playerData.Load(package.PlayerData);
            subsystemPlayers.AddPlayerData(playerData);
        }

        var entities = InitialWorldSnapshotPackage.DeserializeEntities(project, package.EntityData);
        foreach (var entity in entities)
        {
            project.FindEntityById(entity.EntityId, existing => project.RemoveEntity(existing, true));
        }

        project.AddEntities(entities);
    }
}
