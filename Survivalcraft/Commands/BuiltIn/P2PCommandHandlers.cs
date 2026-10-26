using System.Text.Json;
using System.Text.Json.Serialization;

using Engine.Network;

using Game.Localization;
using Game.Network;
using Game.Servers;

using Rendezvous.Protocol;

namespace Game.Commands;

internal static class P2PCommandHandlers
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<P2PRoomState>() }
    };

    public static CommandResult Start(CommandContext context, StartP2PCommand command)
    {
        var node = CommonLib.Net;
        var status = node.GetP2PStatus();
        if (!status.ListenerReady)
        {
            return Fail("server_not_listening", "P2PListenerRequired_Message", "游戏服务器尚未开始监听。");
        }

        if (!status.WorldReady)
        {
            return Fail("world_not_ready", "P2PWorldRequired_Message", "服务器世界尚未加载完成。");
        }

        if (status.State is P2PRoomState.Stopping)
        {
            return Fail("stopping", "P2PStopping_Message", "P2P 房间正在停止，请稍后再试。");
        }

        if (ValidateConfiguration(node.P2PConfiguration, out var configuration) is { } failure)
        {
            return failure;
        }

        if (!status.DesiredEnabled)
        {
            node.StartP2P(configuration);
        }

        status = node.GetP2PStatus();
        var result = status.State is P2PRoomState.Active
            ? CommandResult.LocalizedSensitiveOk(
                "p2p.active",
                "P2PActive_Message",
                "P2P 房间已启用，联机码：{0}。",
                status.JoinCode ?? "--"
            )
            : CommandResult.LocalizedSensitiveOk(
                    status.State is P2PRoomState.RetryWaiting ? "p2p.retry_waiting" : "p2p.registering",
                    "P2PRequested_Message",
                    "P2P 请求已受理，当前状态：{0}。",
                    StateText(status.State)
                )
                with
                {
                    State = CommandResultState.Pending
                };
        return WithStatus(result, status);
    }

    public static CommandResult Stop(CommandContext context, StopP2PCommand command)
    {
        var node = CommonLib.Net;
        if (!node.GetP2PStatus().ListenerReady)
        {
            return Fail("server_not_listening", "P2PListenerRequired_Message", "游戏服务器尚未开始监听。");
        }

        node.StopP2P();
        var status = node.GetP2PStatus();
        return WithStatus(
            CommandResult.LocalizedSensitiveOk(
                    status.State is P2PRoomState.Disabled ? "p2p.disabled" : "p2p.stopping",
                    "P2PStopRequested_Message",
                    "P2P 停止请求已受理，当前状态：{0}。",
                    StateText(status.State)
                ) with
                {
                    State = status.State is P2PRoomState.Disabled
                        ? CommandResultState.Completed
                        : CommandResultState.Pending
                },
            status
        );
    }

    public static CommandResult Status(CommandContext context, GetP2PStatusCommand command)
    {
        var status = CommonLib.Net.GetP2PStatus();
        return WithStatus(
            CommandResult.LocalizedSensitiveOk(
                "p2p.status",
                "P2PStatus_Message",
                "状态：{0}\n信令：{1}\n房间：{2}\n联机码：{3}\n错误：{4}",
                StateText(status.State),
                status.ControlUrl,
                status.Name,
                status.JoinCode ?? "--",
                status.LastError
            ),
            status
        );
    }

    internal static CommandResult? ValidateConfiguration(P2PRoomSettings current, out P2PRoomSettings configuration)
    {
        configuration = current;
        if (string.IsNullOrWhiteSpace(configuration.ControlUrl))
        {
            return Fail("service_required", "P2PServiceRequired_Message", "请先在存档服务器设置中配置信令服务。");
        }

        try
        {
            configuration = configuration with
            {
                ControlUrl = RendezvousAddress.Normalize(new Uri(configuration.ControlUrl)).AbsoluteUri
            };
            if (!new RoomPublication(configuration.Name, configuration.Description, configuration.IsPublic).IsValid)
            {
                return Fail("invalid_configuration", "P2PInvalid_Message", "P2P 配置无效，请检查信令地址和房间信息。");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            return Fail("invalid_configuration", "P2PInvalid_Message", "P2P 配置无效，请检查信令地址和房间信息。");
        }

        return null;
    }

    private static CommandResult WithStatus(CommandResult result, P2PRoomStatus status) =>
        result with { Data = JsonSerializer.SerializeToNode(status, _jsonOptions) };

    private static string StateText(P2PRoomState state) => Text($"P2PState_{state}", state.ToString()).Resolve();

    private static CommandResult Fail(string code, string key, string fallback) =>
        CommandResult.LocalizedFail($"p2p.{code}", key, fallback);

    private static LocalizedText Text(string key, string fallback) => new("Commands", key, fallback);
}
