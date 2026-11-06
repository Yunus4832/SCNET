using EntitySystem.Core;

namespace Game.Commands;

public static class CommandResultPublisher
{
    public static void Publish(
        Project project,
        CommandResult result,
        byte? requesterId = null,
        bool includeServer = true,
        string? correlationId = null
    )
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        if (!WorldRuntimeContext.IsProjectActive(project)) { return; }

        if (includeServer)
        {
            PublishLocalCompletion(project, result, correlationId);
        }

        if (result.Presentation == CommandResultPresentation.Silent)
        {
            return;
        }

        var messages = project.FindSubsystem<SubsystemMessages>(true)!.Service;
        var message = CreateMessage(result);
        if (result.Audience is CommandResultAudience.AllPlayers && !result.Sensitive)
        {
            messages.Publish(message, includePublisher: includeServer);
        }
        else if (requesterId.HasValue)
        {
            messages.Publish(
                message,
                [requesterId.Value],
                includePublisher: includeServer
            );
        }
        else if (includeServer)
        {
            messages.DisplayLocal(message);
        }
    }

    public static void PublishRemote(
        Project project,
        CommandResult result,
        WorldConnection requester,
        string correlationId
    )
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        if (!WorldRuntimeContext.IsProjectActive(project)) { return; }

        ArgumentNullException.ThrowIfNull(requester);
        if (project.IsDisposed || WorldRuntimeContext.For(project).IsClosed) { return; }

        var messages = project.FindSubsystem<SubsystemMessages>(true)!.Service;
        if (result.Audience is CommandResultAudience.AllPlayers &&
            !result.Sensitive &&
            result.Presentation != CommandResultPresentation.Silent)
        {
            var message = CreateMessage(result);
            messages.Relay(message, recipients: null, except: requester);
            messages.DisplayLocal(message);
        }

        var resultPackage = CommandPackage.CreateResult(CommandResponseBuilder.Capture(result), correlationId);
        NetworkSender.Send(project, resultPackage, PackageAudience.To(requester));
    }

    public static void DisplayLocal(
        Project project,
        CommandResult result,
        string? correlationId = null
    )
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        if (!WorldRuntimeContext.IsProjectActive(project)) { return; }

        PublishLocalCompletion(project, result, correlationId);
        if (result.Presentation == CommandResultPresentation.Silent)
        {
            return;
        }

        project.FindSubsystem<SubsystemMessages>(true)!.Service.DisplayLocal(CreateMessage(result));
    }

    private static void PublishLocalCompletion(Project project, CommandResult result, string? correlationId)
    {
        if (correlationId is not null && WorldRuntimeContext.IsProjectActive(project))
        {
            WorldCommandResults.For(project).Publish(result, correlationId);
        }
    }

    private static GameMessage CreateMessage(CommandResult result)
    {
        var presentation = ToGameMessagePresentation(result.Presentation);
        return new GameMessage(
            GameMessageKind.Command,
            GameMessageChannel.Global,
            null,
            new MessageContent([new MessageSegment(result.Message)]),
            result.Success ? GameMessageTone.Success : GameMessageTone.Error,
            presentation
        );
    }

    internal static GameMessagePresentation ToGameMessagePresentation(CommandResultPresentation presentation)
    {
        var result = GameMessagePresentation.None;
        if ((presentation & CommandResultPresentation.History) != 0)
        {
            result |= GameMessagePresentation.History;
        }

        if ((presentation & CommandResultPresentation.Overlay) != 0)
        {
            result |= GameMessagePresentation.Overlay;
        }

        if ((presentation & CommandResultPresentation.Toast) != 0)
        {
            result |= GameMessagePresentation.Toast;
        }

        return result;
    }
}
