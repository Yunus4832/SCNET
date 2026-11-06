using EntitySystem.Core;

using Game.Protocol.Identity;

namespace Game.Network;

/// <summary>Initial session admission and one-role control binding; never a persistent player store.</summary>
public static class WorldConnectionControl
{
    // First-stage one-player/one-role initialization from trusted server metadata.
    // Ownership is not an authentication proof and no player record is created here.
    public static void BindInitialCharacter(Project project, PlayerAddress owner, Guid characterId)
    {
        if (project.IsDisposed) { return; }

        var runtime = WorldRuntimeContext.For(project);
        if (runtime.IsClosed || runtime.IsAuthority || WorldConnectionQuery.Find(project, owner) is not { } session)
        {
            return;
        }

        if (session.CharacterId != Guid.Empty && session.CharacterId != characterId)
        {
            throw new InvalidDataException("A session cannot control a different character implicitly.");
        }

        session.SetControlledCharacter(characterId);
    }
}
