using EntitySystem.Core;

using Game.Characters;
using Game.Network;
using Game.Protocol.Identity;
using Game.World;

namespace Survivalcraft.Test.Fixtures;

public static class CharacterTestEnvironment
{
    public static CharacterData Create(Project project, PlayerAddress owner)
    {
        if (WorldRuntimeContext.For(project).IsAuthority)
        {
            return CharacterData.CreateAuthoritative(project, owner);
        }

        var connections = WorldConnections.For(project);
        if (connections.Self is null)
        {
            connections.SetSelf(new WorldConnection(1, Guid.Empty, owner));
        }
        else if (connections.Self.PlayerAddress != owner)
        {
            throw new InvalidOperationException("The test character must use the replica endpoint's owner.");
        }

        return new CharacterData(project);
    }

    // Isolated permission tests intentionally bypass CharacterData's world construction.
    public static void SetSyntheticOwner(CharacterData character, PlayerAddress owner)
    {
        typeof(CharacterData).GetProperty(nameof(CharacterData.OwnerAddress))!.SetValue(character, owner);
    }
}
