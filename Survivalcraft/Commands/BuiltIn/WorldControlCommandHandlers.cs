using EntitySystem.Core;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Commands;

internal static class WorldControlCommandHandlers
{
    public static bool IsCreativePlayer(
        CommandPrincipal principal,
        Project? project)
    {
        return principal.Player is not null &&
               project?.FindSubsystem<SubsystemGameInfo>(true)?
                   .WorldSettings.GameMode is GameMode.Creative;
    }

    public static CommandResult SetPrecipitation(
        CommandContext context,
        SetPrecipitationCommand command)
    {
        if (!TryGetWeather(context, out var weather, out var failure))
        {
            return failure;
        }

        if (command.Enabled)
        {
            weather.ManualPrecipitationStart();
        }
        else
        {
            weather.ManualPrecipitationEnd();
        }

        BroadcastWeather(weather);
        return CommandResult.LocalizedPublicOk(
            command.Enabled
                ? "world.weather.precipitation_started"
                : "world.weather.precipitation_stopped",
            command.Enabled
                ? "WeatherRainEnabled_Message"
                : "WeatherRainDisabled_Message",
            command.Enabled ? "已开启降水。" : "已停止降水。");
    }

    public static CommandResult SetFog(
        CommandContext context,
        SetFogCommand command)
    {
        if (!TryGetWeather(context, out var weather, out var failure))
        {
            return failure;
        }

        if (command.Enabled)
        {
            weather.ManualFogStart();
        }
        else
        {
            weather.ManualFogEnd();
        }

        BroadcastWeather(weather);
        return CommandResult.LocalizedPublicOk(
            command.Enabled
                ? "world.weather.fog_started"
                : "world.weather.fog_stopped",
            command.Enabled
                ? "WeatherFogEnabled_Message"
                : "WeatherFogDisabled_Message",
            command.Enabled ? "已开启雾气。" : "已关闭雾气。");
    }

    public static CommandResult TriggerLightning(
        CommandContext context,
        TriggerLightningCommand command)
    {
        if (!TryGetWeather(context, out var weather, out var failure))
        {
            return failure;
        }

        if (!IsFinite(command.Position) ||
            !IsFinite(command.Direction) ||
            command.Direction.LengthSquared() < 0.0001f)
        {
            return CommandResult.LocalizedFail(
                "world.weather.invalid_lightning_target",
                "LightningInvalid_Message",
                "闪电目标无效。");
        }

        weather.ManualLightingStrike(
            command.Position,
            Vector3.Normalize(command.Direction));
        return CommandResult.LocalizedPublicOk(
            "world.weather.lightning_triggered",
            "LightningTriggered_Message",
            "已触发闪电。");
    }

    public static CommandResult TriggerPlayerLightning(
        CommandContext context,
        TriggerPlayerLightningCommand command)
    {
        if (context.Principal.Player?.ComponentPlayer is not { } player)
        {
            return CommandResult.LocalizedFail(
                "world.weather.player_required",
                "LightningPlayerRequired_Message",
                "该闪电操作需要在线玩家。");
        }

        var matrix = Matrix.CreateFromQuaternion(
            player.ComponentCreatureModel.EyeRotation);
        return TriggerLightning(
            context,
            new TriggerLightningCommand(
                player.ComponentCreatureModel.EyePosition,
                matrix.Forward));
    }

    public static CommandResult SetSeason(
        CommandContext context,
        SetSeasonCommand command)
    {
        if (context.Project is null)
        {
            return CommandResult.LocalizedFail(
                "command.no_world",
                "NoWorld_Message",
                "当前没有加载世界。");
        }

        if (!Enum.IsDefined(command.Season) ||
            !float.IsFinite(command.Progress) ||
            command.Progress is < 0f or > 1f)
        {
            return CommandResult.LocalizedFail(
                "world.season.invalid",
                "SeasonInvalid_Message",
                "季节或季节进度无效。");
        }

        var start = command.Season switch
        {
            Season.Summer => SubsystemSeasons.SummerStart,
            Season.Autumn => SubsystemSeasons.AutumnStart,
            Season.Winter => SubsystemSeasons.WinterStart,
            Season.Spring => SubsystemSeasons.SpringStart,
            _ => throw new ArgumentOutOfRangeException(nameof(command.Season))
        };
        var gameInfo = context.Project.FindSubsystem<SubsystemGameInfo>(true)!;
        gameInfo.WorldSettings.TimeOfYear =
            IntervalUtils.Normalize(start + command.Progress * 0.25f);
        var seasons = context.Project.FindSubsystem<SubsystemSeasons>(true)!;
        seasons.Season = command.Season;
        seasons.TimeOfSeason = command.Progress;
        if (CommonLib.WorkType is WorkType.Server)
        {
            CommonLib.Net.QueuePackage(
                new SubsystemSeasonPackage(
                    (int)command.Season,
                    command.Progress));
        }

        return CommandResult.LocalizedPublicOk(
            "world.season.changed",
            "SeasonChanged_Message",
            "已将季节设置为 {0}。",
            command.Season.ToString());
    }

    private static bool TryGetWeather(
        CommandContext context,
        out SubsystemWeather weather,
        out CommandResult failure)
    {
        if (context.Project is null)
        {
            weather = null!;
            failure = CommandResult.LocalizedFail(
                "command.no_world",
                "NoWorld_Message",
                "当前没有加载世界。");
            return false;
        }

        weather = context.Project.FindSubsystem<SubsystemWeather>(true)!;
        failure = null!;
        return true;
    }

    private static void BroadcastWeather(SubsystemWeather weather)
    {
        if (CommonLib.WorkType is WorkType.Server)
        {
            CommonLib.Net.QueuePackage(
                SubsystemWeatherPackage.CreateSnapshot(weather));
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) &&
               float.IsFinite(value.Y) &&
               float.IsFinite(value.Z);
    }
}
