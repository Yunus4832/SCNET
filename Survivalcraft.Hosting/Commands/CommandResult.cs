using System.Text.Json.Nodes;

namespace Game.Commands;

public sealed record CommandResult(
    bool Success,
    string Code,
    LocalizedText Message,
    bool Sensitive = false,
    CommandResultAudience Audience = CommandResultAudience.Requester,
    CommandResultState State = CommandResultState.Completed,
    CommandResultPresentation Presentation = CommandResultPresentation.Default,
    JsonNode? Data = null,
    CommandPanelBehavior PanelBehavior = CommandPanelBehavior.ReturnToMessage
)
{
    public static CommandResult Ok(string message, string code = "command.ok") =>
        new(true, code, LocalizedText.Literal(message));

    public static CommandResult PublicOk(string message, string code = "command.ok") =>
        new(true, code, LocalizedText.Literal(message), Audience: CommandResultAudience.AllPlayers);

    public static CommandResult SensitiveOk(string message, string code = "command.ok") =>
        new(true, code, LocalizedText.Literal(message), true);

    public static CommandResult Pending(string message, string code = "command.pending") =>
        new(true, code, LocalizedText.Literal(message), State: CommandResultState.Pending);

    public static CommandResult SilentOk(string code = "command.ok") =>
        new(true, code, LocalizedText.Empty, Presentation: CommandResultPresentation.Silent);

    public static CommandResult Fail(string code, string message) => new(false, code, LocalizedText.Literal(message));

    public static CommandResult LocalizedOk(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments
    ) =>
        new(
            true,
            code,
            CreateLocalized(messageKey, fallback, arguments)
        );

    public static CommandResult LocalizedHistoryOk(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments
    ) =>
        new(
            true,
            code,
            CreateLocalized(messageKey, fallback, arguments),
            Presentation: CommandResultPresentation.History
        );

    public static CommandResult LocalizedPublicOk(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments
    ) =>
        new(
            true,
            code,
            CreateLocalized(messageKey, fallback, arguments),
            Audience: CommandResultAudience.AllPlayers
        );

    public static CommandResult LocalizedPublicToast(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments
    ) =>
        new(
            true,
            code,
            CreateLocalized(messageKey, fallback, arguments),
            Audience: CommandResultAudience.AllPlayers,
            Presentation: CommandResultPresentation.Toast
        );

    public static CommandResult LocalizedSensitiveOk(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments
    ) =>
        new(
            true,
            code,
            CreateLocalized(messageKey, fallback, arguments),
            Sensitive: true
        );

    public static CommandResult LocalizedPending(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments
    ) =>
        new(
            true,
            code,
            CreateLocalized(messageKey, fallback, arguments),
            State: CommandResultState.Pending
        );

    public static CommandResult LocalizedFail(
        string code,
        string messageKey,
        string fallback,
        params string[] arguments
    ) =>
        new(
            false,
            code,
            CreateLocalized(messageKey, fallback, arguments)
        );

    private static LocalizedText CreateLocalized(string key, string fallback, string[] arguments)
    {
        var resource = new LocalizedText("Commands", key, fallback);
        return arguments.Length == 0
            ? resource
            : LocalizedText.Format(resource, arguments.Select(LocalizedText.Literal).ToArray());
    }
}
