using Game.Protocol.Blocks;
using Game.Protocol.Characters;
using Game.Protocol.Commands;
using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Protocol.Identity;
using Game.Protocol.Messaging;
using Game.Protocol.Terrain;
using Game.Protocol.World;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageValidation
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
            case ExplosionSoundPackage explosionSound:
                Require(
                    float.IsFinite(explosionSound.Position.X) &&
                    float.IsFinite(explosionSound.Position.Y) &&
                    float.IsFinite(explosionSound.Position.Z) &&
                    float.IsFinite(explosionSound.Level) &&
                    explosionSound.Level >= 0f &&
                    float.IsFinite(explosionSound.Pitch) &&
                    explosionSound.Pitch is >= -.2f and <= .2f,
                    "Invalid explosion sound."
                );
                break;
            case DispenserSoundPackage dispenser:
                Require(
                    float.IsFinite(dispenser.Position.X) &&
                    float.IsFinite(dispenser.Position.Y) &&
                    float.IsFinite(dispenser.Position.Z),
                    "Invalid dispenser sound position."
                );
                break;
            case ClothingEffectPackage clothing:
                Require(Enum.IsDefined(clothing.Kind), "Invalid clothing effect kind.");
                break;
            case CharacterExperiencePackage experience:
                Require(
                    float.IsFinite(experience.Level) &&
                    experience.Level >= 1f &&
                    experience.LevelUps >= 0 &&
                    experience.LevelUps <= MathF.Floor(experience.Level),
                    "Invalid experience state."
                );
                break;
            case CharacterHitEffectPackage hit:
                Require(
                    float.IsFinite(hit.Position.X) &&
                    float.IsFinite(hit.Position.Y) &&
                    float.IsFinite(hit.Position.Z) &&
                    float.IsFinite(hit.Velocity.X) &&
                    float.IsFinite(hit.Velocity.Y) &&
                    float.IsFinite(hit.Velocity.Z) &&
                    float.IsFinite(hit.BodyPosition.X) &&
                    float.IsFinite(hit.BodyPosition.Y) &&
                    float.IsFinite(hit.BodyPosition.Z),
                    "Invalid hit effect position or velocity."
                );
                break;
            case CreatureSoundPackage sound:
                Require(sound.SoundKind <= 10, "Invalid creature sound kind.");
                break;
            case BodyStatesPackage bodies:
                foreach (var body in bodies.BodyList)
                {
                    Require(
                        float.IsFinite(body.AttackPhase) &&
                        body.AttackPhase is >= 0 and < 1 &&
                        body.AttackCounter >= 0 &&
                        (body.Bend is null || float.IsFinite(body.Bend.Value)) &&
                        float.IsFinite(body.DigIn) &&
                        body.DigIn >= 0,
                        "Invalid creature action state."
                    );
                }

                break;
            case VitalStatePackage vital:
                Require(
                    float.IsFinite(vital.Food) &&
                    vital.Food is >= 0 and <= 1 &&
                    float.IsFinite(vital.Stamina) &&
                    vital.Stamina is >= 0 and <= 1 &&
                    float.IsFinite(vital.Sleep) &&
                    vital.Sleep is >= 0 and <= 1 &&
                    float.IsFinite(vital.Wetness) &&
                    vital.Wetness is >= 0 and <= 1 &&
                    float.IsFinite(vital.Temperature) &&
                    vital.Temperature is >= 0 and <= 24 &&
                    float.IsFinite(vital.EnvironmentTemperature) &&
                    vital.SleepBlackoutSequence >= 0 &&
                    float.IsFinite(vital.SleepBlackoutDuration) &&
                    vital.SleepBlackoutDuration >= 0,
                    "Invalid vital state."
                );
                break;
            case VitalFeedbackPackage feedback:
                Require(feedback.MessageKey is >= 1 and <= 8, "Invalid vital feedback.");
                break;
            case SleepResultPackage sleep:
                Require(double.IsFinite(sleep.StartTime) && sleep.StartTime >= 0, "Invalid sleep start time.");
                break;
            case WakeupRequestPackage wakeRequest:
                Require(
                    double.IsFinite(wakeRequest.SleepStartTime) && wakeRequest.SleepStartTime >= 0,
                    "Invalid wake episode."
                );
                break;
            case WakeupPackage wake:
                Require(Enum.IsDefined(wake.Reason), "Invalid wake reason.");
                break;
            case ComponentFluPackage flu:
                Require(
                    Enum.IsDefined(flu.PackageEventType) &&
                    flu.EffectSequence >= 0 &&
                    float.IsFinite(flu.FluDuration) &&
                    flu.FluDuration >= 0 &&
                    float.IsFinite(flu.FluOnset) &&
                    flu.FluOnset >= 0 &&
                    float.IsFinite(flu.CoughDuration) &&
                    flu.CoughDuration >= 0 &&
                    float.IsFinite(flu.SneezeDuration) &&
                    flu.SneezeDuration >= 0 &&
                    float.IsFinite(flu.BlackoutDuration) &&
                    flu.BlackoutDuration >= 0,
                    "Invalid flu state."
                );
                break;
            case ComponentSicknessPackage sickness:
                Require(
                    float.IsFinite(sickness.SicknessDuration) &&
                    sickness.SicknessDuration >= 0 &&
                    sickness.EffectSequence >= 0 &&
                    float.IsFinite(sickness.PukeDuration) &&
                    sickness.PukeDuration >= 0 &&
                    float.IsFinite(sickness.NauseaDuration) &&
                    sickness.NauseaDuration >= 0,
                    "Invalid sickness state."
                );
                break;
            case EntityDamagePackage damage:
                Require(!damage.Destroyed || damage.Health == 0f, "Destroyed entity must have zero durability.");
                break;
            case EntityHealthPackage health:
                Require(
                    health.DeathTime is null || double.IsFinite(health.DeathTime.Value) && health.DeathTime >= 0,
                    "Invalid death time."
                );
                break;
            case EntityInjuryPackage injury:
                Require(
                    injury.DeathTime is null || double.IsFinite(injury.DeathTime.Value) && injury.DeathTime >= 0,
                    "Invalid death time."
                );
                break;
            case EntityFirePackage fire:
                Require(float.IsFinite(fire.Duration) && fire.Duration >= 0f, "Invalid entity fire duration.");
                break;
            case CreatureWhalePlumePackage plume:
                Require(
                    float.IsFinite(plume.Size) &&
                    float.IsFinite(plume.Duration) &&
                    (plume.IsActive
                        ? plume.Size is > 0 and <= 2 && plume.Duration is > 0 and <= 2
                        : plume.Size == 0 && plume.Duration == 0),
                    "Invalid whale plume."
                );
                break;
            case CreatureShapeshiftStatePackage shapeshift:
                Require(
                    double.IsFinite(shapeshift.StartTime) && shapeshift.StartTime >= 0,
                    "Invalid shapeshift start time."
                );
                break;
            case ProjectileAddedPackage { SmokeTrail: { } trail }:
                Require(
                    float.IsFinite(trail.Size) &&
                    trail.Size is > 0 and <= 16 &&
                    double.IsFinite(trail.EndTime) &&
                    trail.EndTime >= 0,
                    "Invalid projectile smoke trail."
                );
                break;
            case NewYearCelebrationAnnouncementPackage newYear:
                Require(newYear.CharacterId != Guid.Empty, "Invalid celebration character.");
                break;
            case ProjectileEffectPackage projectileEffect:
                Require(
                    Enum.IsDefined(projectileEffect.Effect) &&
                    float.IsFinite(projectileEffect.Pitch) &&
                    projectileEffect.Pitch is >= -1f and <= 1f &&
                    float.IsFinite(projectileEffect.Position.X) &&
                    float.IsFinite(projectileEffect.Position.Y) &&
                    float.IsFinite(projectileEffect.Position.Z),
                    "Invalid projectile effect."
                );
                if (projectileEffect.Effect is ProjectilePackage.EffectType.FireworksWhoosh
                    or ProjectilePackage.EffectType.FireworksExplosion)
                {
                    Require(
                        projectileEffect.Value is >= 0 and <= 255 &&
                        (projectileEffect.Effect == ProjectilePackage.EffectType.FireworksWhoosh
                            ? projectileEffect.Pitch is >= -0.2f and <= 0.2f
                            : projectileEffect.Pitch is >= -0.4f and <= 0f),
                        "Invalid fireworks effect."
                    );
                }

                break;
            case FurnitureDesignRenameRequestPackage renameDesign:
                Text(renameDesign.Name, 20);
                Require(
                    renameDesign.FurnitureIndex is >= 0 and < 65535 &&
                    renameDesign.Source.Y is >= 0 and < 256 &&
                    renameDesign.Source.Face is >= 0 and < 6,
                    "Invalid furniture rename target."
                );
                break;
            case SoundGeneratorNotePackage soundNote:
                Require(soundNote.Note.IsValid, "Invalid sound generator note.");
                break;
            case BlockBurnEffectPackage burn:
                Require(
                    Enum.IsDefined(burn.Kind) &&
                    float.IsFinite(burn.Position.X) &&
                    float.IsFinite(burn.Position.Y) &&
                    float.IsFinite(burn.Position.Z),
                    "Invalid block burn effect position."
                );
                break;
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
                Require(
                    Enum.IsDefined(changed.State) &&
                    changed.ConnectionEpoch != Guid.Empty &&
                    changed.ConnectionGeneration > 0,
                    "Invalid client state or lifetime."
                );
                break;
            case ClientRemovedPackage removed:
                Require(
                    removed.ConnectionEpoch != Guid.Empty && removed.ConnectionGeneration > 0,
                    "Invalid removed connection lifetime."
                );
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
                Controllers(entities.Characters);
                break;
            case SingleEntitySnapshotPackage entity:
                CharacterEntities(entity.CharacterEntities);
                Controllers(entity.Characters);
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
                    commandResult.Result is not null,
                    "Invalid command result."
                );
                Localization.LocalizedTextLimits.Validate(commandResult.Result!.Message);
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
            case TerrainCellDeltaPackage deltas:
                Require(
                    deltas.CellDeltas is { Count: > 0 and <= TerrainCellDeltaPackage.MaximumDeltas },
                    "Invalid terrain delta batch size."
                );
                Require(
                    deltas.CellDeltas.All(delta => delta.Cell.Y is >= 0 and < 256 &&
                                                   delta.BaseContentVersion >= 0 &&
                                                   delta.ResultContentVersion > delta.BaseContentVersion
                    ),
                    "Invalid terrain delta metadata."
                );
                break;
            case TerrainChunksRequestPackage chunks:
                Require(
                    chunks.ChunkRequests.Count is > 0 and <= TerrainChunksRequestPackage.MaximumRequests,
                    "Invalid terrain chunk request count."
                );
                Require(
                    chunks.ChunkRequests.All(request =>
                        request.Allocation.Generation > 0 && request.KnownContentVersion >= 0
                    ),
                    "Invalid terrain chunk request metadata."
                );
                Require(
                    chunks.ChunkRequests.Select(request => request.Allocation.Coords).Distinct().Count() ==
                    chunks.ChunkRequests.Count,
                    "Duplicate terrain chunk request coordinates."
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
                        request.Allocation.Generation > 0 &&
                        request.ContentVersion > 0 &&
                        request.FragmentCount is > 0 and <= SubsystemTerrainPackage.MaximumRequestedFragmentCount &&
                        request.MissingFragmentIndices.Length is > 0
                            and <= SubsystemTerrainPackage.MaximumRequestedFragmentCount &&
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
            client.ConnectionEpoch != Guid.Empty &&
            client.ConnectionGeneration > 0 &&
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

    private static void Controllers(IReadOnlyList<CharacterSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Controller is { } controller)
            {
                Require(
                    controller.ConnectionId > 0 &&
                    controller.CharacterId != Guid.Empty &&
                    controller.ConnectionEpoch != Guid.Empty &&
                    controller.ConnectionGeneration > 0 &&
                    Enum.IsDefined(controller.State),
                    "Invalid entity controller lifetime."
                );
            }
        }
    }

    private static void CharacterEntities(IReadOnlyList<CharacterEntitySnapshot> values)
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
        Require(message.ToastStyle is not { } style || Enum.IsDefined(style.Tone), "Invalid toast tone.");
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

        foreach (var segment in message.Content.Segments)
        {
            Localization.LocalizedTextLimits.Validate(segment.Text);
            Require(message.Kind != GameMessageKind.Chat || segment.Text.IsLiteral, "Chat text must be literal.");
        }

        Require(
            message.Content.Segments.Count <= 64,
            "Too many message segments."
        );
    }
}
