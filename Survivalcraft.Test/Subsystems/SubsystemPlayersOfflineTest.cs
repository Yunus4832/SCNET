using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core;

using Game;
using Game.Extensions;
using Game.Managers;
using Game.Network;
using Game.Subsystems;
using Game.Terrains;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class SubsystemPlayersOfflineTest
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
            var subsystem = new SubsystemPlayers();
            var updater = (TerrainUpdater)RuntimeHelpers.GetUninitializedObject(typeof(TerrainUpdater));
            SetField(updater, "_pendingLocations", new Dictionary<int, TerrainUpdateLocation?>());
            var parametersField = typeof(TerrainUpdater).GetField("_updateParameters",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var parameters = Activator.CreateInstance(parametersField.FieldType)!;
            parametersField.FieldType.GetField("Locations")!
                .SetValue(parameters, new Dictionary<int, TerrainUpdateLocation>());
            parametersField.SetValue(updater, parameters);
            SetField(subsystem, "_subsystemTerrain", new SubsystemTerrain { TerrainUpdater = updater });
            var records = (List<PlayerData>)typeof(SubsystemPlayers)
                .GetField("_playersData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(subsystem)!;
            var updated = 0;
            var removed = 0;
            subsystem.PlayerRemoved += _ => removed++;
            for (byte id = 1; id <= 2; id++)
            {
                var player = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
                player.PlayerGUID = Guid.NewGuid();
                player.PlayerIndex = id;
                player.Name = $"Player {id}";
                typeof(PlayerData).GetProperty(nameof(PlayerData.IsMainPlayer))!.SetValue(player, id == 1);
                var machine = new StateMachine();
                machine.AddState("RequestOffline", Actions.Empty, () =>
                {
                    updated++;
                    subsystem.QueuePlayerOffline(player);
                    subsystem.QueuePlayerOffline(player);
                    Assert.Equal(2, subsystem.PlayersData.Count);
                    Assert.Equal(0, removed);
                }, Actions.Empty);
                machine.TransitionTo("RequestOffline");
                SetField(player, "_stateMachine", machine);
                records.Add(player);
                CommonLib.Net.Clients.Add(id, new Client(null, id, Guid.NewGuid(), player.PlayerGUID, null));
            }

            subsystem.Update(0.05f);

            Assert.Equal(2, updated);
            Assert.Equal(2, removed);
            Assert.Empty(subsystem.PlayersData);
            Assert.Equal(2, subsystem.PlayerList.Count);
            Assert.All(subsystem.PlayerList.Values, entry => Assert.False(entry.IsOnline));
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
