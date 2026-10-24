using Game.Network.Transport;
using Game.Players;

namespace Game.Network.Handling;

public sealed class CharacterDataPackageHandler : PackageHandlerBase<CharacterDataPackage>
{
    internal static bool CanCreateCharacter(Client? sender, IEnumerable<Guid> existingPlayers) =>
        sender is { PlayerAddress: not null } &&
        sender.CharacterId == Guid.Empty &&
        !existingPlayers.Contains(sender.CharacterId);

    internal static bool AcceptsDirection(CharacterDataPackage.DataType type, bool isServer) =>
        isServer
            ? type is CharacterDataPackage.DataType.Create or CharacterDataPackage.DataType.SetUpdateLocation
            : type is CharacterDataPackage.DataType.Modify or CharacterDataPackage.DataType.CreationRejected;

    internal static CharacterCreationError? ValidateCreation(
        CharacterDataPackage package,
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

        if (!CanCreateCharacter(sender, players.Characters.Select(player => player.CharacterId)) ||
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

    public override void Handle(CharacterDataPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(CharacterDataPackage)}");
            return;
        }

        if (GameManager.Project is null || !AcceptsDirection(package.Type, isServer))
        {
            return;
        }

        var project = GameManager.Project;
        CharacterData? characterData;
        var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
        switch (package.Type)
        {
            case CharacterDataPackage.DataType.Create:

                void Reject(CharacterCreationError error)
                {
                    if (context.Sender is { } sender)
                    {
                        netNode.QueuePackage(
                            new CharacterDataPackage
                            {
                                Type = CharacterDataPackage.DataType.CreationRejected,
                                CreationRequestId = package.CreationRequestId, CreationError = error
                            },
                            PackageAudience.To(sender)
                        );
                    }
                }

                var error = ValidateCreation(
                    package,
                    context.Sender,
                    subsystemCharacters,
                    project.FindSubsystem<SubsystemPlayers>(true)!.Store,
                    out var name,
                    out var requestedClass
                );
                if (error is { } failure)
                {
                    Reject(failure);
                    break;
                }

                characterData = new CharacterData(project)
                {
                    CharacterId = Guid.NewGuid(),
                    OwnerAddress = context.Sender!.PlayerAddress!.Value,
                    Name = name,
                    CharacterSkinName = package.SkinName,
                    CharacterSex = requestedClass
                };
                context.Sender!.CharacterId = characterData.CharacterId;
                subsystemCharacters.AddCharacterData(characterData);
                netNode.QueuePackage(CharacterPackageBuilder.Capture(subsystemCharacters), PackageAudience.Global);
                break;
            case CharacterDataPackage.DataType.CreationRejected:
                subsystemCharacters.RejectCharacterCreation(package.CreationRequestId, package.CreationError);
                break;
            case CharacterDataPackage.DataType.Modify:
                if (!ProtocolEnum.TryDecode(package.CharacterSex, out CharacterSex updatedClass))
                {
                    break;
                }

                var characterData2 = subsystemCharacters.FindCharacterData(p => p.CharacterId == package.CharacterId);
                if (characterData2 != null)
                {
                    characterData2.Name = package.CharacterName;
                    characterData2.CharacterSkinName = package.SkinName;
                    characterData2.CharacterSex = updatedClass;
                }

                break;
            case CharacterDataPackage.DataType.SetUpdateLocation:
                var player = subsystemCharacters.Characters.Find(x => ReferenceEquals(x.Client, context.Sender));
                if (player == null)
                {
                    Log.Warning(
                        $"Ignored terrain update location without matching player: " +
                        $"client={context.Sender?.ID.ToString() ?? "null"}, center={package.UpdateLocation.Center}."
                    );
                    break;
                }

                if (context.Sender is null ||
                    !project.FindSubsystem<SubsystemNetworkInterest>(true)!
                        .SetRequestedLocation(context.Sender, player, package.UpdateLocation))
                {
                    Log.Warning(
                        $"Rejected invalid terrain update location: client={context.Sender?.ID.ToString() ?? "null"}, " +
                        $"center={package.UpdateLocation.Center}."
                    );
                }

                break;
        }
    }
}
