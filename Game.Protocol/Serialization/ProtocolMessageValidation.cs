using Game.Protocol.Blocks;
using Game.Protocol.Commands;
using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Protocol.Identity;
using Game.Protocol.Messaging;
using Game.Protocol.Terrain;
using Game.Protocol.World;

namespace Game.Protocol.Serialization;

internal static partial class ProtocolMessageValidation
{
    public static void Validate(IProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!ProtocolMessageCodec.IsDefined(message.GetType()))
        {
            throw new InvalidDataException("Unknown message type.");
        }

        switch (message)
        {
            case AdmissionChallengePackage challenge:
                Text(challenge.Nonce, 43);
                Text(challenge.AuthorityName, 128);
                Text(challenge.AuthorityUrl, 2048);
                Text(challenge.AuthorityPublicKey, 256);
                Require(Enum.IsDefined(challenge.Mode), "Invalid admission mode.");
                Require(
                    challenge.Mode == ServerAdmissionMode.External ||
                    (challenge.AuthorityName.Length == 0 &&
                     challenge.AuthorityUrl.Length == 0 &&
                     challenge.AuthorityPublicKey.Length == 0),
                    "Non-external admission cannot advertise an authority."
                );
                break;
            case AdmissionProofPackage proof:
                Text(proof.Nonce, 43);
                Text(proof.Password, 1024);
                Text(proof.Approval, 16384);
                break;
            case AdmissionResultPackage result:
                Text(result.Nonce, 43);
                Require(
                    Enum.IsDefined(result.Code) && result.RetryAfterSeconds is >= 0 and <= 3600,
                    "Invalid admission result."
                );
                break;
            case ClientAddedPackage added:
                Client(added.Client ?? throw new InvalidDataException("Missing client identity."));
                break;
            case ClientListPackage list:
                Clients(list);
                break;
            case ClientStateChangedPackage changed:
                Require(Enum.IsDefined(changed.State), "Invalid client state.");
                break;
            case ServerInfoResponsePackage server:
                Require(
                    float.IsFinite(server.TimeOfDay) && float.IsFinite(server.TimeOfSeason),
                    "Invalid server state."
                );
                if (server.RequiredModProfile is { } profile) { ValidateProfile(profile); }

                ValidateRepositories(server.TemporaryRepositories);
                break;
            case BootstrapPackage bootstrap:
                Require(!string.IsNullOrWhiteSpace(bootstrap.World.Name), "Missing world name.");
                Text(bootstrap.World.Name, 256);
                Require(bootstrap.BlockMapping.Count <= ProtocolLimits.BlockCapacity, "Too many block mappings.");
                var resources = new HashSet<ResourceId>();
                var indices = new HashSet<ushort>();
                foreach (var entry in bootstrap.BlockMapping)
                {
                    Require(
                        entry.Index < ProtocolLimits.BlockCapacity &&
                        resources.Add(entry.Id) &&
                        indices.Add(entry.Index),
                        "Invalid or duplicate block mapping."
                    );
                }

                Clients(bootstrap.ClientList);
                break;
            case InitialWorldSnapshotPackage initial:
                Clients(initial.ClientList);
                CharacterEntities(initial.CharacterEntities);
                break;
            case EntitySnapshotPackage entities:
                CharacterEntities(entities.CharacterEntities);
                break;
            case SingleEntitySnapshotPackage entity:
                CharacterEntities(entity.CharacterEntities);
                break;
            case CommandRequestPackage command:
                Require(
                    command.Payload.Length <= ProtocolLimits.MaximumCommandPayloadLength,
                    "Command payload is too long."
                );
                break;
            case CommandSuggestionsPackage suggestions:
                Require(suggestions.Suggestions.Count <= 64, "Too many command suggestions.");
                break;
            case CommandPermissionSnapshotPackage permissions:
                Require(
                    permissions.PlayerAddress is { } address &&
                    address != default &&
                    permissions.PermissionGrants.Count <= 256,
                    "Invalid command permission snapshot."
                );
                break;
            case CommandResultPackage commandResult:
                Require(
                    commandResult.Result is not null && (commandResult.Result.MessageArguments?.Count ?? 0) <= 32,
                    "Invalid command result."
                );
                break;
            case MessagePackage chat:
                Message(chat.GameMessage);
                break;
            case SignBlockPackage sign:
                Require(
                    sign.Lines.Count <= byte.MaxValue && sign.Colors.Count <= byte.MaxValue,
                    "Too many sign lines or colors."
                );
                break;
            case EditableBlockDataPackage edit:
                Require(
                    edit.ItemType is BuiltInBlockEditKind.MemoryBank or BuiltInBlockEditKind.TruthTable &&
                    edit.Data.Length <= ushort.MaxValue,
                    "Invalid editable block data."
                );
                break;
            case EditableBlockValuePackage value:
                Require(
                    Enum.IsDefined(value.ItemType) &&
                    value.ItemType is not (BuiltInBlockEditKind.MemoryBank or BuiltInBlockEditKind.TruthTable) &&
                    value.Target is not EditableItemDefinitionTarget,
                    "Invalid editable block value."
                );
                break;
            case TerrainFragmentPackage fragment:
                Require(
                    fragment.ChunkFragment.Payload is { Length: <= ushort.MaxValue },
                    "Terrain fragment payload is too large."
                );
                break;
            case TerrainFragmentsRequestPackage fragments:
                Require(
                    fragments.FragmentRequests.Count is > 0
                        and <= SubsystemTerrainPackage.MaximumFragmentRequestsPerPackage,
                    "Invalid fragment request count."
                );
                foreach (var request in fragments.FragmentRequests)
                {
                    Require(
                        request.FragmentCount is > 0 and <= SubsystemTerrainPackage.MaximumRequestedFragmentCount &&
                        request.MissingFragmentIndices.Length > 0 &&
                        request.MissingFragmentIndices.All(index => index < request.FragmentCount),
                        "Invalid missing fragment metadata."
                    );
                }

                break;
            case InventorySnapshotPackage inventory:
                foreach (var slots in inventory.Slots.Values)
                {
                    foreach (var slot in slots)
                    {
                        Require(
                            slot is ItemSlotSnapshot ||
                            slot is ClothingSlotSnapshot clothes && clothes.Clothes.Count <= byte.MaxValue,
                            "Invalid inventory slot."
                        );
                    }
                }

                break;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidDataException(message); }
    }

    private static void Text(string text, int maximum) => Require(
        text is not null && text.Length <= maximum,
        "Protocol text exceeds its size limit."
    );

    private static void Client(ClientIdentity client)
    {
        Require(
            Enum.IsDefined(client.State) &&
            (client.ID == 0 || client.PlayerAddress is { } address && address != default),
            "Invalid client identity."
        );
    }

    private static void Clients(ClientListPackage list)
    {
        Require(list.List.Count <= byte.MaxValue, "Too many clients.");
        var ids = new HashSet<byte>();
        var subjects = new HashSet<PlayerAddress>();
        foreach (var client in list.List)
        {
            Client(client);
            Require(
                ids.Add(client.ID) && (client.PlayerAddress is not { } address || subjects.Add(address)),
                "Duplicate client or subject session."
            );
        }
    }

    private static void CharacterEntities(IReadOnlyList<Characters.CharacterEntitySnapshot> values)
    {
        Require(values.Count <= ProtocolLimits.MaximumCharacterEntitySnapshots, "Too many character entity summaries.");
        var characters = new HashSet<Guid>();
        var entities = new HashSet<int>();
        foreach (var value in values)
        {
            Require(
                value.CharacterId != Guid.Empty &&
                value.EntityId > 0 &&
                value.ParentBodyId >= 0 &&
                characters.Add(value.CharacterId) &&
                entities.Add(value.EntityId) &&
                float.IsFinite(value.Position.X) &&
                float.IsFinite(value.Position.Y) &&
                float.IsFinite(value.Position.Z),
                "Invalid character entity summary."
            );
        }
    }

    private static void Message(GameMessage message)
    {
        Require(
            (message.Kind == GameMessageKind.Chat) == (message.Sender is not null),
            "Only chat messages require an authenticated sender."
        );
        if (message.Sender is { } sender)
        {
            Require(
                sender.SubjectAddress != default &&
                sender.CharacterId != Guid.Empty &&
                (sender.CharacterId.HasValue || sender.CharacterName.Length == 0),
                "Invalid message sender context."
            );
        }

        Require(
            message.Content.Segments.Count <= 64 && message.LocalizationArguments.Count <= 16,
            "Too many message segments or localization arguments."
        );
    }
}
