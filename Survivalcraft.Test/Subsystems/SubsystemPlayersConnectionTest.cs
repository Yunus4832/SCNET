using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Runtime;

using Game;
using Game.Components;
using Game.Network;
using Game.Subsystems;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class SubsystemPlayersConnectionTest
{
    [Fact]
    public void RestoredHeadlessPlayerListRefreshesAfterConnectionWithoutAnotherEntityEvent()
    {
        var previousMode = RunMode.Value;
        var previousClients = CommonLib.Net.Clients;
        try
        {
            RunMode.Value = RunModeType.HeadlessServer;
            CommonLib.Net.Clients = [];
            var subsystem = new SubsystemPlayers();
            var data = (PlayerData)RuntimeHelpers.GetUninitializedObject(typeof(PlayerData));
            data.PlayerGUID = Guid.NewGuid();
            var player = new ComponentPlayer { PlayerData = data };
            typeof(PlayerData).GetProperty(nameof(PlayerData.ComponentPlayer))!.SetValue(data, player);
            var records = (List<PlayerData>)typeof(SubsystemPlayers)
                .GetField("_playersData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(subsystem)!;
            records.Add(data);

            // World loading restores an entity before its owner connects.
            subsystem.UpdateComponentPlayers();
            Assert.Empty(subsystem.ComponentPlayers);

            var client = new Client(null, 1, Guid.NewGuid(), data.PlayerGUID, null);
            CommonLib.Net.Clients.Add(client.ID, client);
            Assert.Empty(subsystem.ComponentPlayers);

            // Join completion must refresh even when AddEntity is a no-op for that entity.
            subsystem.UpdateComponentPlayers();
            Assert.Same(player, Assert.Single(subsystem.ComponentPlayers));
            subsystem.UpdateComponentPlayers();
            Assert.Same(player, Assert.Single(subsystem.ComponentPlayers));

            CommonLib.Net.Clients.Remove(client.ID);
            subsystem.UpdateComponentPlayers();
            Assert.Empty(subsystem.ComponentPlayers);
        }
        finally
        {
            CommonLib.Net.Clients = previousClients;
            RunMode.Value = previousMode;
        }
    }
}
