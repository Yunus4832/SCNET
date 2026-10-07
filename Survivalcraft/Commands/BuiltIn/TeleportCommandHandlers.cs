using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Commands;

internal static class TeleportCommandHandlers
{
    public static CommandResult Spawn(CommandContext context, TeleportSpawnCommand command)
    {
        if (ValidateTarget(context, context.Principal.Player) is { } unavailable)
        {
            return unavailable;
        }

        var player = context.Principal.Player!;
        var position = command.World ? player.SubsystemPlayers.GlobalSpawnPosition : player.SpawnPosition;
        if (position == Vector3.Zero)
        {
            return CommandResult.LocalizedFail("teleport.spawn_missing", "TeleportSpawnMissing_Message", "出生点尚未确定。");
        }

        return Teleport(context, player, command, position, null);
    }

    public static CommandResult MarkPrivate(CommandContext context, MarkPrivatePositionCommand command) =>
        MarkNamed(context, command.Name, false);

    public static CommandResult MarkPublic(CommandContext context, MarkPublicPositionCommand command) =>
        MarkNamed(context, command.Name, true);

    private static CommandResult MarkNamed(CommandContext context, string name, bool isPublic)
    {
        if (ValidateTarget(context, context.Principal.Player) is { } unavailable)
        {
            return unavailable;
        }

        if (!PositionMarks.IsValidName(name))
        {
            return CommandResult.LocalizedFail("mark.invalid_name", "MarkInvalidName_Message", "名称需为 1–64 个字符，不能包含控制字符或首尾空白。");
        }

        var player = context.Principal.Player!;
        var position = player.ComponentPlayer!.ComponentBody.Position;
        if (!IsValidPosition(position))
        {
            return CommandResult.LocalizedFail("teleport.invalid_position", "TeleportInvalidPosition_Message", "坐标必须有限，X/Z 在 ±1000000 内，Y 在 0–255 内。");
        }

        var marks = isPublic ? context.Project!.FindSubsystem<SubsystemPlayers>(true)!.PublicMarks : player.PrivateMarks;
        marks.Set(name, position);
        return CommandResult.LocalizedOk("mark.saved", "MarkSaved_Message", "已保存标记 {0}：{1}。", name, position.ToString());
    }

    public static CommandResult MarkTeleport(CommandContext context, TeleportMarkCommand command)
    {
        if (ValidateTarget(context, context.Principal.Player) is { } unavailable)
        {
            return unavailable;
        }

        var player = context.Principal.Player!;
        var marks = command.Public ? context.Project!.FindSubsystem<SubsystemPlayers>(true)!.PublicMarks : player.PrivateMarks;
        if (!marks.TryGet(command.Name, out var position))
        {
            return CommandResult.LocalizedFail("mark.missing", "MarkMissing_Message", "该作用域中没有标记 {0}。", command.Name);
        }

        return Teleport(context, player, command, position, null);
    }

    public static CommandResult Self(CommandContext context, TeleportSelfCommand command) =>
        Teleport(context, context.Principal.Player, command, command.Position, command.DestinationPlayer);

    public static CommandResult Player(CommandContext context, TeleportPlayerCommand command) =>
        Teleport(context, FindPlayer(context, command.Player), command, command.Position, command.DestinationPlayer);

    private static PlayerData? FindPlayer(CommandContext context, string name) =>
        context.Project?.FindSubsystem<SubsystemPlayers>(true)?.FindPlayerData(player =>
            MatchesPlayer(player, name));

    internal static bool MatchesPlayer(PlayerData player, string name) =>
        Guid.TryParse(name, out var id) ? player.PlayerGUID == id :
            string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase);

    internal static bool IsValidPosition(Vector3 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z) &&
        Math.Abs(position.X) <= 1_000_000f && Math.Abs(position.Z) <= 1_000_000f &&
        position.Y is >= 0f and <= 255f;

    internal static BoundingBox GetDestinationBox(Vector3 position, Vector3 size) =>
        new(position - new Vector3(size.X / 2f, 0f, size.Z / 2f) + new Vector3(0.01f),
            position + new Vector3(size.X / 2f, size.Y, size.Z / 2f) - new Vector3(0.01f));

    internal static bool IsDestinationTerrainReady(Terrain terrain, BoundingBox box)
    {
        for (var x = Terrain.ToCell(box.Min.X); x <= Terrain.ToCell(box.Max.X); x++)
        {
            for (var z = Terrain.ToCell(box.Min.Z); z <= Terrain.ToCell(box.Max.Z); z++)
            {
                if (terrain.GetChunkAtCell(x, z, false) is not
                    { MainThreadState: > TerrainChunkState.InvalidContents4 })
                {
                    return false;
                }
            }
        }

        return true;
    }

    internal static CommandResult? ValidateTarget(CommandContext context, PlayerData? target)
    {
        if (context.Project == null)
        {
            return CommandResult.LocalizedFail("teleport.no_world", "NoWorld_Message", "当前没有加载世界。");
        }

        if (target?.ComponentPlayer is not { } player || !target.IsReadyForPlaying ||
            !context.Project.FindSubsystem<SubsystemPlayers>(true)!.PlayersData.Contains(target) ||
            (CommonLib.WorkType == WorkType.Server && !target.IsMainPlayer && target.Client is not { IsConnected: true }) ||
            player.ComponentHealth.Health <= 0f)
        {
            return CommandResult.LocalizedFail("teleport.player_unavailable", "TeleportPlayerUnavailable_Message", "目标玩家必须在线、已加载且存活；控制台请指定玩家。");
        }

        return null;
    }

    private static CommandResult Teleport(CommandContext context, PlayerData? target, IGameCommand command,
        Vector3? position, string? destination)
    {
        if (ValidateTarget(context, target) is { } unavailable)
        {
            return unavailable;
        }

        if (position.HasValue == (destination != null))
        {
            return CommandResult.LocalizedFail("teleport.invalid_target", "TeleportInvalidTarget_Message", "请指定坐标或目标玩家，不能同时指定。");
        }

        if (destination != null)
        {
            var destinationPlayer = FindPlayer(context, destination);
            if (ReferenceEquals(destinationPlayer, target))
            {
                return CommandResult.LocalizedOk("teleport.unchanged", "TeleportUnchanged_Message", "目标玩家就是被传送玩家，未执行传送。");
            }

            if (destinationPlayer?.ComponentPlayer == null || !destinationPlayer.IsReadyForPlaying ||
                destinationPlayer.ComponentPlayer.ComponentHealth.Health <= 0f)
            {
                return CommandResult.LocalizedFail("teleport.destination_unavailable", "TeleportDestinationUnavailable_Message", "目的地玩家尚未进入世界。");
            }

            position = destinationPlayer.ComponentPlayer.ComponentBody.Position;
        }

        if (!IsValidPosition(position!.Value))
        {
            return CommandResult.LocalizedFail("teleport.invalid_position", "TeleportInvalidPosition_Message", "坐标必须有限，X/Z 在 ±1000000 内，Y 在 0–255 内。");
        }

        target!.PendingTeleport?.Cancel();
        if (TryComplete(context, target, position.Value, command is TeleportSpawnCommand { World: true }) is { } result)
        {
            return result;
        }

        target.PendingTeleport = new PlayerTeleportRequest(context, target, command, position.Value);
        return CommandResult.LocalizedPending("teleport.loading", "TeleportLoading_Message", "正在准备落点地形，加载完成后检查并传送。");
    }

    // Null means terrain preparation is still in progress, not a failed teleport.
    internal static CommandResult? TryComplete(CommandContext context, PlayerData target, Vector3 position, bool resolveWorldSpawn = false)
    {
        if (ValidateTarget(context, target) is { } unavailable)
        {
            return unavailable;
        }

        var player = target.ComponentPlayer!;
        if (player.Entity.FindComponent<ComponentRider>()?.Mount != null)
        {
            return CommandResult.LocalizedFail("teleport.mounted", "TeleportMounted_Message", "请先离开坐骑再传送。");
        }

        var body = player.ComponentBody;
        var box = GetDestinationBox(position, body.StanceBoxSize);
        var subsystemTerrain = context.Project!.FindSubsystem<SubsystemTerrain>(true)!;
        var preparationBox = resolveWorldSpawn ? new BoundingBox(position - new Vector3(8f), position + new Vector3(8f)) : box;
        if (!IsDestinationTerrainReady(subsystemTerrain.Terrain, preparationBox))
        {
            return null;
        }

        if (resolveWorldSpawn)
        {
            // GlobalSpawnPosition is a coarse spawn anchor, not necessarily a valid standing position.
            position = target.FindNoIntroSpawnPosition(position, false);
            box = GetDestinationBox(position, body.StanceBoxSize);
        }

        DynamicArray<ComponentBody.CollisionBox> collisions = [];
        body.FindTerrainCollisionBoxes(box, collisions);
        body.FindMovingBlocksCollisionBoxes(position, collisions);
        if (body.IsColliding(box, collisions))
        {
            return CommandResult.LocalizedFail("teleport.blocked", "TeleportBlocked_Message", "落点空间不足，玩家会卡在方块中，未执行传送。");
        }

        player.ComponentInput.Navigation.Cancel("teleported");
        player.ComponentInput.ViewControl.Cancel("teleported");
        target.PrivateMarks.Set("previous", ApplyPosition(body, position));

        // Hand off the prepared chunks to the player's normal interest before releasing the temporary location.
        var updater = subsystemTerrain.TerrainUpdater;
        updater.UpdateLocations.TryGetValue(target.PlayerIndex, out var location);
        location.Center = position.XZ;
        location.VisibilityDistance = Math.Max(32f, location.VisibilityDistance);
        location.ContentDistance = Math.Max(32f, location.ContentDistance);
        if (CommonLib.WorkType == WorkType.Server && target.Client is { } client)
        {
            context.Project.FindSubsystem<SubsystemNetworkInterest>(true)!.SetRequestedLocation(client, target, location);
        }
        else
        {
            updater.SetUpdateLocation(target.PlayerIndex, location.Center, location.VisibilityDistance, location.ContentDistance);
        }
        if (CommonLib.WorkType == WorkType.Server)
        {
            if (target.Client != null)
            {
                CommonLib.Net.QueuePackage(
                    new ComponentPlayerPackage(player, ComponentPlayerPackage.PlayerAction.PositionSet),
                    PackageAudience.To(target.Client));
            }

            NetworkSender.SendToObservers(player.Entity,
                new ComponentPlayerPackage(player, ComponentPlayerPackage.PlayerAction.PositionSet), target.Client);
        }

        return CommandResult.LocalizedOk("teleport.completed", "TeleportCompleted_Message", "已传送 {0} 到 {1}。", target.Name, position.ToString());
    }

    internal static Vector3 ApplyPosition(ComponentBody body, Vector3 position)
    {
        var previous = body.Position;
        body.ApplyAuthoritativePosition(position, Vector3.Zero);
        return previous;
    }
}
