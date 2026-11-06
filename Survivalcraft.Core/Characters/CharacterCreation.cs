using EntitySystem.Core;

namespace Game.Characters;

/// <summary>Shared authoritative creation rules for admitted local and remote players.</summary>
public static class CharacterCreation
{
    internal static bool CanCreate(WorldConnection? sender, IEnumerable<Guid> existingPlayers) =>
        sender is { PlayerAddress: not null } &&
        sender.CharacterId == Guid.Empty &&
        !existingPlayers.Contains(sender.CharacterId);

    internal static CharacterCreationError? Validate(
        CharacterCreateRequestPackage package,
        WorldConnection? sender,
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

    public static CharacterCreationError? TryCreate(
        CharacterCreateRequestPackage request,
        WorldConnection? sender,
        Project project,
        out CharacterData? character
    )
    {
        character = null;
        if (sender is null ||
            !ReferenceEquals(WorldConnections.For(project).Members.GetValueOrDefault(sender.ID), sender))
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

        character = CharacterData.CreateAuthoritative(project, sender.PlayerAddress!.Value);
        character.CharacterId = Guid.NewGuid();
        character.Name = name;
        character.CharacterSkinName = request.SkinName;
        character.CharacterSex = sex;
        sender.SetControlledCharacter(character.CharacterId);
        if (ReferenceEquals(sender, WorldConnections.For(project).Self)) { character.SetMain(); }

        characters.AddCharacterData(character);
        return null;
    }
}
