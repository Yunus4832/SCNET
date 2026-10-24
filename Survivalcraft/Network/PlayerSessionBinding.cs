using EntitySystem.Core;

using Game.Protocol.Identity;

namespace Game.Network;

/// <summary>Initial session admission and one-role control binding; never a persistent player store.</summary>
internal static class PlayerSessionBinding
{
    // First-stage one-player/one-role initialization from trusted server metadata.
    // Ownership is not an authentication proof and no player record is created here.
    internal static void BindInitialCharacter(PlayerAddress owner, Guid characterId)
    {
        if (CommonLib.WorkType != WorkType.Client || PlayerSessionQuery.Find(owner) is not { } session) { return; }

        if (session.CharacterId != Guid.Empty && session.CharacterId != characterId)
        {
            throw new InvalidDataException("A session cannot control a different character implicitly.");
        }

        session.CharacterId = characterId;
    }

    internal static void PrepareLocalPlayer(Project project)
    {
        if (CommonLib.WorkType == WorkType.Client || CommonLib.Net.Self is not { PlayerAddress: { } address } session)
        {
            return;
        }

        var player = project.FindSubsystem<SubsystemPlayers>(true)!.Store.GetOrCreate(address);
        if (player.Ban is not null) { throw new InvalidOperationException("该玩家已被此存档封禁。"); }

        session.CharacterId = player.CharacterId;
    }
}
