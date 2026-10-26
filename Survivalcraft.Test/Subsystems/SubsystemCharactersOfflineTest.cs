using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Runtime;
using Engine.Core.Security;

using Game.Characters;
using Game.Extensions;
using Game.Managers;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.Terrains;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class SubsystemCharactersOfflineTest
{
    [Theory]
    [InlineData(RunModeType.Gui)]
    [InlineData(RunModeType.HeadlessServer)]
    public void OfflineRequestsRunAfterAllPlayersUpdateAndOnlyOnce(RunModeType mode)
    {
        var previousMode = RunMode.Value;
        var previousWorkType = CommonLib.WorkType;
        var previousClients = CommonLib.Net.Clients;
        var previousGarbageCollect = SettingsManager.Current.AutoGarbageCollect;
        try
        {
            RunMode.Value = mode;
            CommonLib.WorkType = WorkType.Server;
            CommonLib.Net.Clients = [];
            SettingsManager.Current.AutoGarbageCollect = false;
            var subsystem = new SubsystemCharacters();
            var updater = (TerrainUpdater)RuntimeHelpers.GetUninitializedObject(typeof(TerrainUpdater));
            SetField(updater, "_pendingLocations", new Dictionary<int, TerrainUpdateLocation?>());
            var parametersField = typeof(TerrainUpdater).GetField(
                "_updateParameters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;
            var parameters = Activator.CreateInstance(parametersField.FieldType)!;
            parametersField.FieldType.GetField("Locations")!
                .SetValue(parameters, new Dictionary<int, TerrainUpdateLocation>());
            parametersField.SetValue(updater, parameters);
            SetField(subsystem, "_subsystemTerrain", new SubsystemTerrain { TerrainUpdater = updater });
            var records = (List<CharacterData>)typeof(SubsystemCharacters)
                .GetField("_characters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(subsystem)!;
            var updated = 0;
            var removed = 0;
            subsystem.CharacterRemoved += _ => removed++;
            for (byte id = 1; id <= 2; id++)
            {
                var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
                player.CharacterId = Guid.NewGuid();
                player.CharacterIndex = id;
                player.Name = $"Player {id}";
                player.OwnerAddress = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
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
                CommonLib.Net.Clients.Add(id, new Client(null, id, Guid.NewGuid(), player.CharacterId, null));
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
            SettingsManager.Current.AutoGarbageCollect = previousGarbageCollect;
            CommonLib.Net.Clients = previousClients;
            CommonLib.WorkType = previousWorkType;
            RunMode.Value = previousMode;
        }
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
