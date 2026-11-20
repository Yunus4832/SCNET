using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.Core;

using Game.Characters;
using Game.Components;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Replication;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class RowingPublicationTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorityRelaysOnlyOwnedRowingWithoutPresentationComponents(bool spoofSender)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        using var replica = new RuntimeTestProject(RuntimeSide.Client);
        var authority = WorldNetworkBinding.For(project).RequireNode();
        var clientNode = WorldNetworkBinding.For(replica).RequireNode();
        var characters = new SubsystemCharacters();
        project.AddSubsystem(characters);
        var interest = new SubsystemNetworkInterest();
        project.AddSubsystem(interest);
        var actor = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        authority.AddClient(actor);
        var observer = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid())
        {
            State = ClientState.Playing,
            ConnectionPhase = ConnectionPhase.Live
        };
        authority.AddClient(observer);
        var server = new Client(Guid.Empty);
        var pair = InProcessMessageConnection.CreatePair(authority, observer, clientNode, server);
        using var toClient = pair.ToClient;
        using var toAuthority = pair.ToAuthority;
        var characterComponent = new ComponentCharacter();
        project.InitializeComponent(characterComponent);
        var entity = characterComponent.Entity;
        entity.EntityId = 42;
        var body = new ComponentBody();
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(entity)!).Add(body);
        ((Dictionary<Entity, bool>)typeof(Project).GetField(
                "entityDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .GetValue(project)!).Add(entity, true);
        var character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(character, project);
        character.CharacterId = actor.CharacterId;
        character.ComponentCharacter = characterComponent;
        ((List<CharacterData>)typeof(SubsystemCharacters).GetField(
                "_characters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .GetValue(characters)!).Add(character);
        interest.Entities.Seed(observer, EntityInterestGroup.Creatures, [entity.EntityId]);
        Assert.Null(entity.FindComponent<ComponentHumanModel>());
        var message = new CreatureRowingStatePackage { EntityId = entity.EntityId, RowLeft = true };

        new ComponentBehaviorPackageHandler().Handle(
            message,
            new PackageReceiveContext(authority, authority.Runtime, spoofSender ? observer : actor)
        );

        Assert.Equal(spoofSender ? 0 : 1, authority.PendingPackageCount);
        if (!spoofSender)
        {
            typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(authority, null);
            Assert.Equal(
                1,
                WorldNetworkBinding.For(clientNode.Runtime).Inbox.Drain(
                    1,
                    received => Assert.Equal(message, received.Package)
                )
            );
        }
    }
}
