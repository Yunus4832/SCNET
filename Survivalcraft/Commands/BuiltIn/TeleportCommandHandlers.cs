using Game.Network;
using Game.Network.Transport;

namespace Game.Commands;

internal static class TeleportCommandHandlers
{
    public static CommandResult MarkPrivate(CommandContext context, MarkPrivatePositionCommand command) =>
        MarkNamed(context, command.Name, false);

    public static CommandResult MarkPublic(CommandContext context, MarkPublicPositionCommand command) =>
        MarkNamed(context, command.Name, true);

    private static CommandResult MarkNamed(CommandContext context, string name, bool isPublic)
    {
        if (ValidateTarget(context, context.Principal.Character) is { } unavailable)
        {
            return unavailable;
        }

        if (!PositionMarks.IsValidName(name))
        {
            return CommandResult.LocalizedFail(
                "mark.invalid_name",
                "MarkInvalidName_Message",
                "名称需为 1–64 个字符，不能包含控制字符或首尾空白。"
            );
        }

        var player = context.Principal.Character!;
        var position = player.ComponentCharacter!.ComponentBody.Position;
        if (!IsValidPosition(position))
        {
            return CommandResult.LocalizedFail(
                "teleport.invalid_position",
                "TeleportInvalidPosition_Message",
                "坐标必须有限，X/Z 在 ±1000000 内，Y 在 0–255 内。"
            );
        }

        var marks = isPublic
            ? context.Project!.FindSubsystem<SubsystemPositionMarks>(true)!.PublicMarks
            : context.Principal.Subject is { } subject
                ? context.Project!.FindSubsystem<SubsystemPositionMarks>(true)!.GetPrivateMarks(subject.Address)
                : null;
        if (marks is null) { return MissingSubject(); }

        marks.Set(name, position);
        return CommandResult.LocalizedOk(
            "mark.saved",
            "MarkSaved_Message",
            "已保存标记 {0}：{1}。",
            name,
            position.ToString()
        );
    }

    public static CommandResult MarkTeleport(CommandContext context, TeleportMarkCommand command) =>
        MarkTeleport(context, context.Principal.Character, command, command.Name, command.Public);

    public static CommandResult PlayerMarkTeleport(CommandContext context, TeleportPlayerMarkCommand command) =>
        MarkTeleport(context, FindPlayer(context, command.Player), command, command.Name, command.Public);

    private static CommandResult MarkTeleport(
        CommandContext context,
        CharacterData? player,
        IGameCommand command,
        string name,
        bool isPublic
    )
    {
        if (ValidateTarget(context, player) is { } unavailable)
        {
            return unavailable;
        }

        var marks = isPublic
            ? context.Project!.FindSubsystem<SubsystemPositionMarks>(true)!.PublicMarks
            : TargetPrivateMarks(context, player!);
        if (marks is null) { return MissingSubject(); }

        if (!marks.TryGet(name, out var position))
        {
            return CommandResult.LocalizedFail("mark.missing", "MarkMissing_Message", "该作用域中没有标记 {0}。", name);
        }

        return Teleport(context, player, command, position, null);
    }

    private static PositionMarks? TargetPrivateMarks(CommandContext context, CharacterData target)
    {
        var address = ReferenceEquals(context.Principal.Character, target) ? context.Principal.Subject?.Address : null;
        address ??= PlayerSessionQuery.GetController(target.CharacterId);
        return address is { } subject
            ? context.Project!.FindSubsystem<SubsystemPositionMarks>(true)!.GetPrivateMarks(subject)
            : null;
    }

    private static CommandResult MissingSubject()
    {
        return CommandResult.LocalizedFail("mark.subject_required", "MarkSubjectRequired_Message", "私人标记需要已进入存档的主体。");
    }

    public static CommandResult Self(CommandContext context, TeleportSelfCommand command) =>
        Teleport(context, context.Principal.Character, command, command.Position, command.DestinationPlayer);

    public static CommandResult Player(CommandContext context, TeleportPlayerCommand command) =>
        Teleport(context, FindPlayer(context, command.Player), command, command.Position, command.DestinationPlayer);

    private static CharacterData? FindPlayer(CommandContext context, string name) =>
        context.Project?.FindSubsystem<SubsystemCharacters>(true)?.FindCharacterData(player =>
            MatchesPlayer(player, name)
        );

    internal static bool MatchesPlayer(CharacterData player, string name) =>
        Guid.TryParse(name, out var id)
            ? player.CharacterId == id
            : string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase);

    internal static bool IsValidPosition(Vector3 position) =>
        float.IsFinite(position.X) &&
        float.IsFinite(position.Y) &&
        float.IsFinite(position.Z) &&
        Math.Abs(position.X) <= 1_000_000f &&
        Math.Abs(position.Z) <= 1_000_000f &&
        position.Y is >= 0f and <= 255f;

    internal static BoundingBox GetDestinationBox(Vector3 position, Vector3 size) =>
        new(
            position - new Vector3(size.X / 2f, 0f, size.Z / 2f) + new Vector3(0.01f),
            position + new Vector3(size.X / 2f, size.Y, size.Z / 2f) - new Vector3(0.01f)
        );

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

    internal static CommandResult? ValidateTarget(CommandContext context, CharacterData? target)
    {
        if (context.Project == null)
        {
            return CommandResult.LocalizedFail("teleport.no_world", "NoWorld_Message", "当前没有加载世界。");
        }

        if (target?.ComponentCharacter is not { } player ||
            !target.IsReadyForPlaying ||
            !context.Project.FindSubsystem<SubsystemCharacters>(true)!.Characters.Contains(target) ||
            (CommonLib.WorkType == WorkType.Server &&
             !target.IsLocalCharacter &&
             target.Client is not { IsConnected: true }) ||
            player.ComponentHealth.Health <= 0f)
        {
            return CommandResult.LocalizedFail(
                "teleport.player_unavailable",
                "TeleportPlayerUnavailable_Message",
                "目标玩家必须在线、已加载且存活；控制台请指定玩家。"
            );
        }

        return null;
    }

    private static CommandResult Teleport(
        CommandContext context,
        CharacterData? target,
        IGameCommand command,
        Vector3? position,
        string? destination
    )
    {
        if (ValidateTarget(context, target) is { } unavailable)
        {
            return unavailable;
        }

        if (position.HasValue == (destination != null))
        {
            return CommandResult.LocalizedFail(
                "teleport.invalid_target",
                "TeleportInvalidTarget_Message",
                "请指定坐标或目标玩家，不能同时指定。"
            );
        }

        if (destination != null)
        {
            var destinationPlayer = FindPlayer(context, destination);
            if (ReferenceEquals(destinationPlayer, target))
            {
                return CommandResult.LocalizedOk(
                    "teleport.unchanged",
                    "TeleportUnchanged_Message",
                    "目标玩家就是被传送玩家，未执行传送。"
                );
            }

            if (destinationPlayer?.ComponentCharacter == null ||
                !destinationPlayer.IsReadyForPlaying ||
                destinationPlayer.ComponentCharacter.ComponentHealth.Health <= 0f)
            {
                return CommandResult.LocalizedFail(
                    "teleport.destination_unavailable",
                    "TeleportDestinationUnavailable_Message",
                    "目的地玩家尚未进入世界。"
                );
            }

            position = destinationPlayer.ComponentCharacter.ComponentBody.Position;
        }

        if (!IsValidPosition(position!.Value))
        {
            return CommandResult.LocalizedFail(
                "teleport.invalid_position",
                "TeleportInvalidPosition_Message",
                "坐标必须有限，X/Z 在 ±1000000 内，Y 在 0–255 内。"
            );
        }

        target!.PendingTeleport?.Cancel();
        if (TryComplete(context, target, position.Value) is { } result)
        {
            return result;
        }

        target.PendingTeleport = new PlayerTeleportRequest(context, target, command, position.Value);
        return CommandResult.LocalizedPending("teleport.loading", "TeleportLoading_Message", "正在准备落点地形，加载完成后检查并传送。");
    }

    // Null means terrain preparation is still in progress, not a failed teleport.
    internal static CommandResult? TryComplete(CommandContext context, CharacterData target, Vector3 position)
    {
        if (ValidateTarget(context, target) is { } unavailable)
        {
            return unavailable;
        }

        var player = target.ComponentCharacter!;
        if (player.Entity.FindComponent<ComponentRider>()?.Mount != null)
        {
            return CommandResult.LocalizedFail("teleport.mounted", "TeleportMounted_Message", "请先离开坐骑再传送。");
        }

        var body = player.ComponentBody;
        var box = GetDestinationBox(position, body.StanceBoxSize);
        var subsystemTerrain = context.Project!.FindSubsystem<SubsystemTerrain>(true)!;
        if (!IsDestinationTerrainReady(subsystemTerrain.Terrain, box))
        {
            return null;
        }

        DynamicArray<ComponentBody.CollisionBox> collisions = [];
        body.FindTerrainCollisionBoxes(box, collisions);
        body.FindMovingBlocksCollisionBoxes(position, collisions);
        if (body.IsColliding(box, collisions))
        {
            return CommandResult.LocalizedFail("teleport.blocked", "TeleportBlocked_Message", "落点空间不足，玩家会卡在方块中，未执行传送。");
        }

        var marks = TargetPrivateMarks(context, target);
        if (marks is null) { return MissingSubject(); }

        player.ComponentInput.Navigation.Cancel("teleported");
        player.ComponentInput.ViewControl.Cancel("teleported");
        marks.Set("previous", ApplyPosition(body, position));

        // Hand off the prepared chunks to the player's normal interest before releasing the temporary location.
        var updater = subsystemTerrain.TerrainUpdater;
        updater.UpdateLocations.TryGetValue(target.CharacterIndex, out var location);
        location.Center = position.XZ;
        location.VisibilityDistance = Math.Max(32f, location.VisibilityDistance);
        location.ContentDistance = Math.Max(32f, location.ContentDistance);
        if (CommonLib.WorkType == WorkType.Server && target.Client is { } client)
        {
            context.Project.FindSubsystem<SubsystemNetworkInterest>(true)!.SetRequestedLocation(
                client,
                target,
                location
            );
        }
        else
        {
            updater.SetUpdateLocation(
                target.CharacterIndex,
                location.Center,
                location.VisibilityDistance,
                location.ContentDistance
            );
        }

        if (CommonLib.WorkType == WorkType.Server)
        {
            if (target.Client != null)
            {
                CommonLib.Net.QueuePackage(
                    CharacterPackageBuilder.Capture(player, ComponentCharacterPackage.CharacterAction.PositionSet),
                    PackageAudience.To(target.Client)
                );
            }

            NetworkSender.SendToObservers(
                player.Entity,
                CharacterPackageBuilder.Capture(player, ComponentCharacterPackage.CharacterAction.PositionSet),
                target.Client
            );
        }

        return CommandResult.LocalizedOk(
            "teleport.completed",
            "TeleportCompleted_Message",
            "已传送 {0} 到 {1}。",
            target.Name,
            position.ToString()
        );
    }

    internal static Vector3 ApplyPosition(ComponentBody body, Vector3 position)
    {
        var previous = body.Position;
        body.ApplyAuthoritativePosition(position, Vector3.Zero);
        return previous;
    }
}
