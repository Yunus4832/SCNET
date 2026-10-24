using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class InitialWorldSnapshotPackageHandler : PackageHandlerBase<InitialWorldSnapshotPackage>
{
    public override void Handle(InitialWorldSnapshotPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (isServer || netNode == null || GameManager.Project == null || package.Epoch != netNode.ConnectionEpoch)
        {
            return;
        }

        var project = GameManager.Project;
        PackageDispatcher.Handle(package.ClientList, context);
        var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
        foreach (var data in package.Characters)
        {
            var values = new EntitySystem.TemplatesDatabase.ValuesDictionary();
            values.ApplyOverridesUseMessagePack(data);
            var guid = values.GetValue("CharacterId", Guid.Empty);
            if (subsystemCharacters.Characters.Any(player => player.CharacterId == guid))
            {
                continue;
            }

            var characterData = new CharacterData(project);
            characterData.Load(values);
            subsystemCharacters.AddCharacterData(characterData);
        }

        project.AddEntities(EntitySnapshotSerializer.Deserialize(project, package.EntityData));
        netNode.CurrentConnectionPhase = ConnectionPhase.Live;
        netNode.QueuePackage(
            new ConnectionPhaseAckPackage(package.Epoch, ConnectionPhase.WorldSnapshotApplied),
            PackageAudience.To(netNode.Server!)
        );
        ScreensManager.FindScreen<GameLoadingScreen>("GameLoading", true)!.WorldSnapshotApplied();
    }
}
