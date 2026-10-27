using EntitySystem.TemplatesDatabase;

namespace Game.Network.Snapshots;

public static class CharacterPackageBuilder
{
    public static ComponentCharacterPackage Capture(
        ComponentCharacter player,
        ComponentCharacterPackage.CharacterAction type
    ) =>
        type switch
        {
            ComponentCharacterPackage.CharacterAction.BodyUpdate => CaptureMovement(player),
            ComponentCharacterPackage.CharacterAction.DigEvent => new CharacterDigPackage
            {
                ControllerSessionId = player.CharacterData.ClientId,
                DigEvent = player.CurDigEventItem.DigEvent,
                DigRequestId = player.CurDigEventItem.RequestId,
                DigExpectedValue = player.CurDigEventItem.ExpectedValue,
                NetDigRay = player.CurDigEventItem.NetDigRay,
                NetDigRaycast = CaptureHit(player.CurDigEventItem.NetDigRaycast)
            },
            ComponentCharacterPackage.CharacterAction.AimEvent => new CharacterAimPackage
            {
                ControllerSessionId = player.CharacterData.ClientId, AimEvent = player.CurAimEventItem.AimEvent,
                NetAimRay = player.CurAimEventItem.NetAim
            },
            ComponentCharacterPackage.CharacterAction.InteractEvent => (player.CurInteractEventItem.InteractEvent switch
            {
                InteractionMessageKind.Use => new
                    CharacterUsePackage
                    {
                        ControllerSessionId = player.CharacterData.ClientId,
                        NetInteractRay = player.CurInteractEventItem.NetInteractRay,
                        NetInteractRaycast = CaptureHit(player.CurInteractEventItem.NetPlaceRaycast)
                    },
                InteractionMessageKind.Place => new CharacterPlacePackage
                {
                    ControllerSessionId = player.CharacterData.ClientId,
                    PlacementRequestId = player.CurInteractEventItem.RequestId,
                    PlacementCell = player.CurInteractEventItem.PlacementCell,
                    PlacementExpectedValue = player.CurInteractEventItem.ExpectedValue,
                    NetInteractRay = player.CurInteractEventItem.NetInteractRay,
                    NetInteractRaycast = CaptureHit(player.CurInteractEventItem.NetPlaceRaycast)
                },
                _ => throw new InvalidOperationException("Unsupported message operation.")
            }),
            ComponentCharacterPackage.CharacterAction.CreativeFlyChange => new CharacterFlightPackage
            {
                ControllerSessionId = player.CharacterData.ClientId,
                IsCreativeFly = player.ComponentLocomotion.IsCreativeFlyEnabled
            },
            ComponentCharacterPackage.CharacterAction.SyncStat => CaptureStatistics(player),
            ComponentCharacterPackage.CharacterAction.PositionSet => new CharacterPositionPackage
            {
                ControllerSessionId = player.CharacterData.ClientId, Position = player.ComponentBody.Position,
                Velocity = player.ComponentBody.Velocity
            },
            ComponentCharacterPackage.CharacterAction.IntoPlaying or ComponentCharacterPackage.CharacterAction.Restart
                or ComponentCharacterPackage.CharacterAction.Drop
                => Capture(player.CharacterData, type),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    private static CharacterStatisticsPackage CaptureStatistics(ComponentCharacter player)
    {
        var statistics = new ValuesDictionary();
        player.CharacterStats.Save(statistics);
        return new CharacterStatisticsPackage
            { ControllerSessionId = player.CharacterData.ClientId, Stat = statistics.ToMessagePack() };
    }

    private static CharacterStatePackage CaptureMovement(ComponentCharacter player)
    {
        var body = player.ComponentBody.ParentBody;
        var flags = ComponentCharacterPackage.ChangFlag.PositionChange |
                    ComponentCharacterPackage.ChangFlag.RotationChange |
                    ComponentCharacterPackage.ChangFlag.VelocityChange;
        var lookAngles = Vector2.Zero;
        var childLookAngles = Vector2.Zero;
        var ladder = 0;
        var sneaking = false;
        if (body != null)
        {
            flags |= ComponentCharacterPackage.ChangFlag.ParentBodyChange |
                     ComponentCharacterPackage.ChangFlag.ChildLookAnglesChange;
            if (body.Locomotion is { } locomotion)
            {
                flags |= ComponentCharacterPackage.ChangFlag.LookAnglesChange;
                lookAngles = locomotion.LookAngles;
            }

            childLookAngles = player.ComponentLocomotion.LookAngles;
        }
        else
        {
            body = player.ComponentBody;
            flags |= ComponentCharacterPackage.ChangFlag.LookAnglesChange |
                     ComponentCharacterPackage.ChangFlag.SneakChange;
            lookAngles = player.ComponentLocomotion.LookAngles;
            sneaking = body.TargetCrouchFactor.CloseTo(1f);
            if (player.ComponentLocomotion.LadderValue is { } value)
            {
                flags |= ComponentCharacterPackage.ChangFlag.LadderChange;
                ladder = value;
            }
        }

        // Every unreliable update remains self-contained, including unchanged movement state.
        return new CharacterStatePackage
        {
            ControllerSessionId = player.CharacterData.ClientId,
            PackageChangeFlag = flags,
            LookAngles = lookAngles,
            ChildLookAngles = childLookAngles,
            LadderValue = ladder,
            Sneaking = sneaking,
            Position = body.Position,
            Rotation = body.Rotation,
            Velocity = body.Velocity
        };
    }

    public static ComponentCharacterPackage Capture(
        CharacterData player,
        ComponentCharacterPackage.CharacterAction type
    ) =>
        type switch
        {
            ComponentCharacterPackage.CharacterAction.IntoPlaying => new CharacterReadyPackage
                { ControllerSessionId = player.ClientId },
            ComponentCharacterPackage.CharacterAction.Restart => new CharacterRestartPackage
                { ControllerSessionId = player.ClientId },
            ComponentCharacterPackage.CharacterAction.Drop => new CharacterDropPackage
                { ControllerSessionId = player.ClientId },
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    public static CharacterExperiencePackage Capture(CharacterData player, int count, bool playSound, float level) =>
        new()
            { ControllerSessionId = player.ClientId, Count = count, PlaySound = playSound, Level = level };

    public static CharacterDragDropPackage Capture(
        CharacterData player,
        int inventoryID,
        int slotIndex,
        Vector3 position,
        int count
    ) => new()
    {
        ControllerSessionId = player.ClientId, InventoryID = inventoryID, ActiveSlot = slotIndex,
        HitPosition = position,
        Count = count
    };

    public static CharacterHitPackage Capture(
        ComponentCharacter player,
        ComponentBody body,
        Vector3 hitPosition,
        Vector3 hitDirection
    ) => new()
    {
        ControllerSessionId = player.CharacterData.ClientId, BodyId = body.Entity.EntityId, HitPosition = hitPosition,
        HitDirection = hitDirection
    };

    public static CharacterTerrainHit? CaptureHit(TerrainRaycastResult? hit) => hit is { } value
        ? new CharacterTerrainHit(
            value.Ray,
            value.Value,
            value.CollisionBoxIndex,
            value.Distance,
            value.CellFace.Point,
            value.CellFace.Face
        )
        : null;

    public static TerrainRaycastResult? RestoreHit(CharacterTerrainHit? hit) => hit is { } value
        ? new TerrainRaycastResult
        {
            Ray = value.Ray,
            Value = value.Value,
            CollisionBoxIndex = value.CollisionBoxIndex,
            Distance = value.Distance,
            CellFace = new CellFace(value.Cell.X, value.Cell.Y, value.Cell.Z, value.Face)
        }
        : null;

    public static CharacterListPackage Capture(SubsystemCharacters players)
    {
        players.RefreshCharacterList();
        return new CharacterListPackage(players.CharacterList.Values);
    }

    public static CharacterProfilePackage CaptureProfile(CharacterData player) => new()
    {
        CharacterName = player.Name, SkinName = player.CharacterSkinName, CharacterId = player.CharacterId,
        CharacterSex = ProtocolEnum.Encode(player.CharacterSex)
    };

    public static CharacterCreateRequestPackage CaptureCreation(CharacterData profile, Guid requestId) => new()
    {
        CreationRequestId = requestId, CharacterName = profile.Name, SkinName = profile.CharacterSkinName,
        CharacterSex = ProtocolEnum.Encode(profile.CharacterSex)
    };
}
