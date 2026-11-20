using System.Reflection;
using System.Runtime.CompilerServices;

using Game.Characters;
using Game.Components;
using Game.Network;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class SubsystemCharactersConnectionTest
{
    [Fact]
    public void RestoredPlayerListRefreshesAfterConnectionWithoutAnotherEntityEvent()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var node = WorldNetworkBinding.For(project).RequireNode();
        var subsystem = new SubsystemCharacters();
        project.InitializeSubsystem(subsystem);
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(data, project);
        data.CharacterId = Guid.NewGuid();
        var player = new ComponentCharacter { CharacterData = data };
        typeof(CharacterData).GetProperty(nameof(CharacterData.ComponentCharacter))!.SetValue(data, player);
        var records = (List<CharacterData>)typeof(SubsystemCharacters)
            .GetField("_characters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(subsystem)!;
        records.Add(data);

        // World loading restores an entity before its owner connects.
        subsystem.UpdateComponentCharacters();
        Assert.Empty(subsystem.ComponentCharacters);

        var client = new Client(null, 1, Guid.NewGuid(), data.CharacterId);
        node.AddClient(client);
        Assert.Empty(subsystem.ComponentCharacters);

        // Join completion must refresh even when AddEntity is a no-op for that entity.
        subsystem.UpdateComponentCharacters();
        Assert.Same(player, Assert.Single(subsystem.ComponentCharacters));
        subsystem.UpdateComponentCharacters();
        Assert.Same(player, Assert.Single(subsystem.ComponentCharacters));

        if (node.Clients.TryGetValue(client.ID, out var removedConnection))
        {
            node.UnregisterClient(removedConnection);
        }

        subsystem.UpdateComponentCharacters();
        Assert.Empty(subsystem.ComponentCharacters);
    }
}
