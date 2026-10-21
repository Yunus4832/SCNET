using EntitySystem.TemplatesDatabase;

namespace Game.Network.Snapshots;

public static class PlayerPackageBuilder
{
    public static ComponentPlayerPackage Capture(ComponentPlayer player, ComponentPlayerPackage.PlayerAction type) =>
        type switch
        {
            ComponentPlayerPackage.PlayerAction.BodyUpdate => CaptureMovement(player),
            ComponentPlayerPackage.PlayerAction.DigEvent => new PlayerDigPackage
            {
                FromPlayerId = player.PlayerData.ClientId,
                DigEvent = player.CurDigEventItem.DigEvent,
                DigRequestId = player.CurDigEventItem.RequestId,
                DigExpectedValue = player.CurDigEventItem.ExpectedValue,
                NetDigRay = player.CurDigEventItem.NetDigRay,
                NetDigRaycast = CaptureHit(player.CurDigEventItem.NetDigRaycast)
            },
            ComponentPlayerPackage.PlayerAction.AimEvent => new PlayerAimPackage
            {
                FromPlayerId = player.PlayerData.ClientId, AimEvent = player.CurAimEventItem.AimEvent,
                NetAimRay = player.CurAimEventItem.NetAim
            },
            ComponentPlayerPackage.PlayerAction.InteractEvent => new PlayerInteractionPackage
            {
                FromPlayerId = player.PlayerData.ClientId,
                InteractEvent = player.CurInteractEventItem.InteractEvent,
                PlacementRequestId = player.CurInteractEventItem.RequestId,
                PlacementCell = player.CurInteractEventItem.PlacementCell,
                PlacementExpectedValue = player.CurInteractEventItem.ExpectedValue,
                NetInteractRay = player.CurInteractEventItem.NetInteractRay,
                NetInteractRaycast = CaptureHit(player.CurInteractEventItem.NetPlaceRaycast)
            },
            ComponentPlayerPackage.PlayerAction.CreativeFlyChange => new PlayerFlightPackage
            {
                FromPlayerId = player.PlayerData.ClientId,
                IsCreativeFly = player.ComponentLocomotion.IsCreativeFlyEnabled
            },
            ComponentPlayerPackage.PlayerAction.SyncStat => CaptureStatistics(player),
            ComponentPlayerPackage.PlayerAction.PositionSet => new PlayerPositionPackage
            {
                FromPlayerId = player.PlayerData.ClientId, Position = player.ComponentBody.Position,
                Velocity = player.ComponentBody.Velocity
            },
            ComponentPlayerPackage.PlayerAction.IntoPlaying or ComponentPlayerPackage.PlayerAction.Restart
                or ComponentPlayerPackage.PlayerAction.Drop
                => Capture(player.PlayerData, type),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    private static PlayerStatisticsPackage CaptureStatistics(ComponentPlayer player)
    {
        var statistics = new ValuesDictionary();
        player.PlayerStats.Save(statistics);
        return new PlayerStatisticsPackage
            { FromPlayerId = player.PlayerData.ClientId, Stat = statistics.ToMessagePack() };
    }

    private static PlayerStatePackage CaptureMovement(ComponentPlayer player)
    {
        var body = player.ComponentBody.ParentBody;
        var flags = ComponentPlayerPackage.ChangFlag.PositionChange |
                    ComponentPlayerPackage.ChangFlag.RotationChange |
                    ComponentPlayerPackage.ChangFlag.VelocityChange;
        var lookAngles = Vector2.Zero;
        var childLookAngles = Vector2.Zero;
        var ladder = 0;
        var sneaking = false;
        if (body != null)
        {
            flags |= ComponentPlayerPackage.ChangFlag.ParentBodyChange |
                     ComponentPlayerPackage.ChangFlag.ChildLookAnglesChange;
            if (body.Locomotion is { } locomotion)
            {
                flags |= ComponentPlayerPackage.ChangFlag.LookAnglesChange;
                lookAngles = locomotion.LookAngles;
            }

            childLookAngles = player.ComponentLocomotion.LookAngles;
        }
        else
        {
            body = player.ComponentBody;
            flags |= ComponentPlayerPackage.ChangFlag.LookAnglesChange | ComponentPlayerPackage.ChangFlag.SneakChange;
            lookAngles = player.ComponentLocomotion.LookAngles;
            sneaking = body.TargetCrouchFactor.CloseTo(1f);
            if (player.ComponentLocomotion.LadderValue is { } value)
            {
                flags |= ComponentPlayerPackage.ChangFlag.LadderChange;
                ladder = value;
            }
        }

        // Every unreliable update remains self-contained, including unchanged movement state.
        return new PlayerStatePackage
        {
            FromPlayerId = player.PlayerData.ClientId,
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

    public static ComponentPlayerPackage Capture(PlayerData player, ComponentPlayerPackage.PlayerAction type) =>
        type switch
        {
            ComponentPlayerPackage.PlayerAction.IntoPlaying => new PlayerReadyPackage
                { FromPlayerId = player.ClientId },
            ComponentPlayerPackage.PlayerAction.Restart => new PlayerRestartPackage { FromPlayerId = player.ClientId },
            ComponentPlayerPackage.PlayerAction.Drop => new PlayerDropPackage { FromPlayerId = player.ClientId },
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    public static PlayerExperiencePackage Capture(PlayerData player, int count, bool playSound, float level) => new()
        { FromPlayerId = player.ClientId, Count = count, PlaySound = playSound, Level = level };

    public static PlayerDragDropPackage Capture(
        PlayerData player,
        int inventoryID,
        int slotIndex,
        Vector3 position,
        int count
    ) => new()
    {
        FromPlayerId = player.ClientId, InventoryID = inventoryID, ActiveSlot = slotIndex, HitPosition = position,
        Count = count
    };

    public static PlayerHitPackage Capture(
        ComponentPlayer player,
        ComponentBody body,
        Vector3 hitPosition,
        Vector3 hitDirection
    ) => new()
    {
        FromPlayerId = player.PlayerData.ClientId, BodyId = body.Entity.EntityId, HitPosition = hitPosition,
        HitDirection = hitDirection
    };

    public static PlayerTerrainHit? CaptureHit(TerrainRaycastResult? hit) => hit is { } value
        ? new PlayerTerrainHit(
            value.Ray,
            value.Value,
            value.CollisionBoxIndex,
            value.Distance,
            value.CellFace.Point,
            value.CellFace.Face
        )
        : null;

    public static TerrainRaycastResult? RestoreHit(PlayerTerrainHit? hit) => hit is { } value
        ? new TerrainRaycastResult
        {
            Ray = value.Ray,
            Value = value.Value,
            CollisionBoxIndex = value.CollisionBoxIndex,
            Distance = value.Distance,
            CellFace = new CellFace(value.Cell.X, value.Cell.Y, value.Cell.Z, value.Face)
        }
        : null;

    public static PlayerListPackage Capture(SubsystemPlayers players)
    {
        players.RefreshPlayerList();
        return new PlayerListPackage(players.PlayerList.Values);
    }

    public static PlayerDataPackage Capture(PlayerData player, PlayerDataPackage.DataType type) => new()
    {
        Type = type, PlayerName = player.Name, SkinName = player.CharacterSkinName, PlayerGuid = player.PlayerGUID,
        PlayerClass = ProtocolEnum.Encode(player.PlayerClass)
    };
}
