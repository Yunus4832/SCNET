using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Commands;

internal static class TeleportCommandHandlers
{
    public static CommandResult Self(CommandContext context, TeleportSelfCommand command) =>
        Teleport(context, context.Principal.Player, command.Position, command.DestinationPlayer);

    public static CommandResult Player(CommandContext context, TeleportPlayerCommand command) =>
        Teleport(context, FindPlayer(context, command.Player), command.Position, command.DestinationPlayer);

    private static PlayerData? FindPlayer(CommandContext context, string name) =>
        context.Project?.FindSubsystem<SubsystemPlayers>(true)?.FindPlayerData(player =>
            Guid.TryParse(name, out var id) ? player.PlayerGUID == id :
                string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase));

    internal static bool IsValidPosition(Vector3 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z) &&
        Math.Abs(position.X) <= 1_000_000f && Math.Abs(position.Z) <= 1_000_000f &&
        position.Y is >= 0f and <= 255f;

    private static CommandResult Teleport(CommandContext context, PlayerData? target, Vector3? position, string? destination)
    {
        if (context.Project == null)
        {
            return CommandResult.LocalizedFail("teleport.no_world", "NoWorld_Message", "当前没有加载世界。");
        }

        if (target?.ComponentPlayer is not { } player || !target.IsReadyForPlaying ||
            !context.Project.FindSubsystem<SubsystemPlayers>(true)!.PlayersData.Contains(target) ||
            player.ComponentHealth.Health <= 0f)
        {
            return CommandResult.LocalizedFail("teleport.player_unavailable", "TeleportPlayerUnavailable_Message", "目标玩家必须在线、已加载且存活；控制台请指定玩家。");
        }

        if (position.HasValue == (destination != null))
        {
            return CommandResult.LocalizedFail("teleport.invalid_target", "TeleportInvalidTarget_Message", "请指定坐标或目标玩家，不能同时指定。");
        }

        if (destination != null)
        {
            var destinationPlayer = FindPlayer(context, destination);
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

        if (player.Entity.FindComponent<ComponentRider>()?.Mount != null)
        {
            return CommandResult.LocalizedFail("teleport.mounted", "TeleportMounted_Message", "请先离开坐骑再传送。");
        }

        player.ComponentInput.Navigation.Cancel("teleported");
        player.ComponentInput.ViewControl.Cancel("teleported");
        var body = player.ComponentBody;
        body.ApplyAuthoritativePosition(position.Value, Vector3.Zero);
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

        return CommandResult.LocalizedOk("teleport.completed", "TeleportCompleted_Message", "已传送 {0} 到 {1}。", target.Name, position.Value.ToString());
    }
}
