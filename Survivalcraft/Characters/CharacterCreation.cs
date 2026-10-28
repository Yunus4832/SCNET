using EntitySystem.Core;

using Game.Network;
using Game.Players;

namespace Game.Characters;

/// <summary>Shared authoritative creation rules for admitted local and remote players.</summary>
internal static class CharacterCreation
{
    internal static bool CanCreate(Client? sender, IEnumerable<Guid> existingPlayers) =>
        sender is { PlayerAddress: not null } &&
        sender.CharacterId == Guid.Empty &&
        !existingPlayers.Contains(sender.CharacterId);

    internal static CharacterCreationError? Validate(
        CharacterCreateRequestPackage package,
        Client? sender,
        SubsystemCharacters players,
        WorldPlayerStore playerStore,
        out string name,
        out CharacterSex characterSex
    )
    {
        name = string.Empty;
        characterSex = default;
        if (package.CreationRequestId == Guid.Empty ||
            sender?.PlayerAddress is not { } address ||
            playerStore.Find(address) is not { Ban: null } subject)
        {
            return CharacterCreationError.Unavailable;
        }

        if (subject.CharacterId != Guid.Empty) { return CharacterCreationError.ExistingCharacter; }

        if (!CanCreate(sender, players.Characters.Select(player => player.CharacterId)) ||
            !ProtocolEnum.TryDecode(package.CharacterSex, out characterSex) ||
            string.IsNullOrWhiteSpace(package.SkinName))
        {
            return CharacterCreationError.InvalidProfile;
        }

        try { name = CharacterData.SanitizeName(package.CharacterName.Trim()); }
        catch (InvalidOperationException) { return CharacterCreationError.InvalidProfile; }

        if (!CharacterData.VerifyName(name)) { return CharacterCreationError.InvalidProfile; }

        return players.IsCharacterNameAvailable(name) ? null : CharacterCreationError.NameUnavailable;
    }

    internal static CharacterCreationError? TryCreate(
        CharacterCreateRequestPackage request,
        Client? sender,
        Project project,
        NetNode node,
        out CharacterData? character,
        WidgetInputDevice inputDevice = WidgetInputDevice.None
    )
    {
        character = null;
        if (sender is null || !ReferenceEquals(node.Clients.GetValueOrDefault(sender.ID), sender))
        {
            return CharacterCreationError.Unavailable;
        }

        var characters = project.FindSubsystem<SubsystemCharacters>(true)!;
        var error = Validate(
            request,
            sender,
            characters,
            project.FindSubsystem<SubsystemPlayers>(true)!.Store,
            out var name,
            out var sex
        );
        if (error is not null) { return error; }

        character = new CharacterData(project)
        {
            CharacterId = Guid.NewGuid(), OwnerAddress = sender.PlayerAddress!.Value, Name = name,
            CharacterSkinName = request.SkinName, CharacterSex = sex, InputDevice = inputDevice
        };
        sender.CharacterId = character.CharacterId;
        if (ReferenceEquals(sender, node.Self)) { character.SetMain(); }

        characters.AddCharacterData(character);
        return null;
    }
}
