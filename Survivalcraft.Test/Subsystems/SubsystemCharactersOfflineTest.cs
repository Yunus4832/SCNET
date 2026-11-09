using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Runtime;
using Engine.Core.Security;

using Game.Characters;
using Game.Extensions;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class SubsystemCharactersOfflineTest
{
    [Fact]
    public void OfflineRequestsRunAfterAllPlayersUpdateAndOnlyOnce()
    {
        var previousNodeRole = CommonLib.Net.Role;
        var previousClients = CommonLib.Net.Clients.Values.ToArray();
        var previousGarbageCollect = Game.Configuration.HostingConfiguration.Preferences.AutoGarbageCollect;
        try
        {
            CommonLib.Net.Role = NodeRole.Authority;
            ConnectionTestEnvironment.SetRoster(CommonLib.Net, []);
            Game.Configuration.HostingConfiguration.Preferences.AutoGarbageCollect = false;
            using var project = new RuntimeTestProject(RuntimeSide.Authority);
            var subsystem = new SubsystemCharacters();
            project.InitializeSubsystem(subsystem);
            using var data = new Terrain();
            var terrain = new SubsystemTerrain { Terrain = data };
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Demands))!.SetValue(
                terrain,
                new TerrainDemandManager(data, Game.Terrains.Distribution.TerrainContentRole.Authority)
            );
            SetField(subsystem, "_subsystemTerrain", terrain);
            var records = (List<CharacterData>)typeof(SubsystemCharacters)
                .GetField("_characters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(subsystem)!;
            var updated = 0;
            var removed = 0;
            subsystem.CharacterRemoved += _ => removed++;
            for (byte id = 1; id <= 2; id++)
            {
                var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
                typeof(CharacterData).GetField(
                    "<Project>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )!.SetValue(player, project);
                player.CharacterId = Guid.NewGuid();
                player.CharacterIndex = id;
                player.Name = $"Player {id}";
                CharacterTestEnvironment.SetSyntheticOwner(
                    player,
                    new PlayerAddress(IdentityAddress.Encode(new byte[32]))
                );
                player.CharacterSkinName = "$Male1";
                typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(player, id == 1);
                var machine = new StateMachine();
                machine.AddState(
                    "RequestOffline",
                    Actions.Empty,
                    () =>
                    {
                        updated++;
                        subsystem.QueueCharacterOffline(player);
                        subsystem.QueueCharacterOffline(player);
                        Assert.Equal(2, subsystem.Characters.Count);
                        Assert.Equal(0, removed);
                    },
                    Actions.Empty
                );
                machine.TransitionTo("RequestOffline");
                SetField(player, "_stateMachine", machine);
                records.Add(player);
                WorldNetworkBinding.For(project).RequireNode()
                    .AddClient(new Client(null, id, Guid.NewGuid(), player.CharacterId));
            }

            subsystem.Update(0.05f);

            Assert.Equal(2, updated);
            Assert.Equal(2, removed);
            Assert.Empty(subsystem.Characters);
            Assert.Equal(2, subsystem.CharacterList.Count);
            Assert.All(subsystem.CharacterList.Values, entry => Assert.False(entry.IsOnline));
        }
        finally
        {
            Game.Configuration.HostingConfiguration.Preferences.AutoGarbageCollect = previousGarbageCollect;
            ConnectionTestEnvironment.SetRoster(CommonLib.Net, previousClients);
            CommonLib.Net.Role = previousNodeRole;
        }
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
