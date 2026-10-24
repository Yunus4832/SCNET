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
public sealed class SubsystemCharactersConnectionTest
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
            var subsystem = new SubsystemCharacters();
            var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
            data.CharacterId = Guid.NewGuid();
            var player = new ComponentCharacter { CharacterData = data };
            typeof(CharacterData).GetProperty(nameof(CharacterData.ComponentCharacter))!.SetValue(data, player);
            var records = (List<CharacterData>)typeof(SubsystemCharacters)
                .GetField("_characters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(subsystem)!;
            records.Add(data);

            // World loading restores an entity before its owner connects.
            subsystem.UpdateComponentCharacters();
            Assert.Empty(subsystem.ComponentCharacters);

            var client = new Client(null, 1, Guid.NewGuid(), data.CharacterId, null);
            CommonLib.Net.Clients.Add(client.ID, client);
            Assert.Empty(subsystem.ComponentCharacters);

            // Join completion must refresh even when AddEntity is a no-op for that entity.
            subsystem.UpdateComponentCharacters();
            Assert.Same(player, Assert.Single(subsystem.ComponentCharacters));
            subsystem.UpdateComponentCharacters();
            Assert.Same(player, Assert.Single(subsystem.ComponentCharacters));

            CommonLib.Net.Clients.Remove(client.ID);
            subsystem.UpdateComponentCharacters();
            Assert.Empty(subsystem.ComponentCharacters);
        }
        finally
        {
            CommonLib.Net.Clients = previousClients;
            RunMode.Value = previousMode;
        }
    }
}
