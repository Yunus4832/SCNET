using Game.Protocol.Identity;

namespace Game.Network;

/// <summary>Initial session admission and one-role control binding; never a persistent player store.</summary>
internal static class PlayerSessionBinding
{
    // First-stage one-player/one-role initialization from trusted server metadata.
    // Ownership is not an authentication proof and no player record is created here.
    internal static void BindInitialCharacter(PlayerAddress owner, Guid characterId)
    {
        if (GameManager.Role != WorldRole.RemoteClient || PlayerSessionQuery.Find(owner) is not { } session) { return; }

        if (session.CharacterId != Guid.Empty && session.CharacterId != characterId)
        {
            throw new InvalidDataException("A session cannot control a different character implicitly.");
        }

        session.CharacterId = characterId;
    }
}
