using System.Text.Json.Nodes;

namespace Game.Commands;

public sealed record CommandResult(
    bool Success,
    string Code,
    string Message,
    bool Sensitive = false,
    CommandResultAudience Audience = CommandResultAudience.Requester,
    CommandResultState State = CommandResultState.Completed,
    CommandResultPresentation Presentation = CommandResultPresentation.Default,
    string MessageKey = "",
    IReadOnlyList<string>? MessageArguments = null,
    JsonNode? Data = null,
    CommandPanelBehavior PanelBehavior = CommandPanelBehavior.ReturnToMessage)
{
    public static CommandResult Ok(string message, string code = "command.ok") => new(true, code, message);

    public static CommandResult PublicOk(string message, string code = "command.ok") =>
        new(true, code, message, Audience: CommandResultAudience.AllPlayers);

    public static CommandResult SensitiveOk(string message, string code = "command.ok") =>
        new(true, code, message, true);

    public static CommandResult Pending(string message, string code = "command.pending") =>
        new(true, code, message, State: CommandResultState.Pending);

    public static CommandResult SilentOk(string code = "command.ok") =>
        new(true, code, string.Empty, Presentation: CommandResultPresentation.Silent);

    public static CommandResult Fail(string code, string message) => new(false, code, message);

    public static CommandResult LocalizedOk(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            true,
            code,
            FormatFallback(fallback, arguments),
            MessageKey: messageKey,
            MessageArguments: arguments);

    public static CommandResult LocalizedHistoryOk(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            true,
            code,
            FormatFallback(fallback, arguments),
            Presentation: CommandResultPresentation.History,
            MessageKey: messageKey,
            MessageArguments: arguments);

    public static CommandResult LocalizedPublicOk(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            true,
            code,
            FormatFallback(fallback, arguments),
            Audience: CommandResultAudience.AllPlayers,
            MessageKey: messageKey,
            MessageArguments: arguments);

    public static CommandResult LocalizedPublicToast(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            true,
            code,
            FormatFallback(fallback, arguments),
            Audience: CommandResultAudience.AllPlayers,
            Presentation: CommandResultPresentation.Toast,
            MessageKey: messageKey,
            MessageArguments: arguments);

    public static CommandResult LocalizedSensitiveOk(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            true,
            code,
            FormatFallback(fallback, arguments),
            Sensitive: true,
            MessageKey: messageKey,
            MessageArguments: arguments);

    public static CommandResult LocalizedPending(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            true,
            code,
            FormatFallback(fallback, arguments),
            State: CommandResultState.Pending,
            MessageKey: messageKey,
            MessageArguments: arguments);

    public static CommandResult LocalizedFail(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments) =>
        new(
            false,
            code,
            FormatFallback(fallback, arguments),
            MessageKey: messageKey,
            MessageArguments: arguments);

    private static string FormatFallback(string fallback, string[] arguments)
    {
        return arguments.Length == 0
            ? fallback
            : string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                fallback,
                arguments);
    }
}
