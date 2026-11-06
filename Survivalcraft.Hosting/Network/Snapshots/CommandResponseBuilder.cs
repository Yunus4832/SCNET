namespace Game.Network.Snapshots;

public static class CommandResponseBuilder
{
    public static CommandResponse Capture(CommandResult result) => new(
        result.Success,
        result.Code,
        result.Message,
        IsPending: result.State == CommandResultState.Pending,
        Data: result.Data?.DeepClone(),
        Presentation: CommandResultPublisher.ToGameMessagePresentation(result.Presentation),
        CloseInputOnSuccess: result.PanelBehavior == CommandPanelBehavior.CloseOnSuccess
    );

    public static CommandResult Restore(CommandResponse response) => new(
        response.Success,
        response.Code,
        response.Message,
        State: response.IsPending ? CommandResultState.Pending : CommandResultState.Completed,
        Presentation: RestorePresentation(response.Presentation),
        Data: response.Data,
        PanelBehavior: response.CloseInputOnSuccess
            ? CommandPanelBehavior.CloseOnSuccess
            : CommandPanelBehavior.ReturnToMessage
    );

    private static CommandResultPresentation RestorePresentation(GameMessagePresentation presentation)
    {
        var result = CommandResultPresentation.Silent;
        if ((presentation & GameMessagePresentation.History) != 0)
        {
            result |= CommandResultPresentation.History;
        }

        if ((presentation & GameMessagePresentation.Overlay) != 0)
        {
            result |= CommandResultPresentation.Overlay;
        }

        if ((presentation & GameMessagePresentation.Toast) != 0)
        {
            result |= CommandResultPresentation.Toast;
        }

        return result;
    }
}
