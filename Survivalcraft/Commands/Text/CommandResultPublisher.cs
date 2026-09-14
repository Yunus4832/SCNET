using EntitySystem.Core;

using Game.Messaging;
using Game.Network;
using Game.Network.Packages;

namespace Game.Commands;

public static class CommandResultPublisher
{
    public static void Publish(
        Project project,
        CommandResult result,
        byte? requesterId = null,
        bool includeServer = true)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        if (result.Presentation is CommandResultPresentation.Silent)
        {
            return;
        }

        var messages = project.FindSubsystem<SubsystemGameWidgets>(true)!.Messages;
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
                includePublisher: includeServer);
        }
        else if (includeServer)
        {
            messages.DisplayLocal(message);
        }
    }

    public static void PublishRemote(
        Project project,
        CommandResult result,
        Client requester,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(requester);
        if (result.Presentation is CommandResultPresentation.Silent)
        {
            return;
        }

        var messages = project.FindSubsystem<SubsystemGameWidgets>(true)!.Messages;
        if (result.Audience is CommandResultAudience.AllPlayers &&
            !result.Sensitive)
        {
            var message = CreateMessage(result);
            messages.Relay(message, recipients: null, except: requester);
            messages.DisplayLocal(message);
        }

        var resultPackage = CommandPackage.CreateResult(result, correlationId);
        resultPackage.To = requester;
        CommonLib.Net.QueuePackage(resultPackage);
    }

    public static void DisplayLocal(
        Project project,
        CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        if (result.Presentation is CommandResultPresentation.Silent)
        {
            return;
        }

        project.FindSubsystem<SubsystemGameWidgets>(true)!
            .Messages.DisplayLocal(CreateMessage(result));
    }

    private static GameMessage CreateMessage(CommandResult result)
    {
        var presentation = result.Presentation switch
        {
            CommandResultPresentation.Toast => GameMessagePresentation.Toast,
            _ => GameMessagePresentation.Default
        };
        var message = GameMessage.Command(result.Message, result.Success) with
        {
            Presentation = presentation
        };
        if (string.IsNullOrWhiteSpace(result.MessageKey))
        {
            return message;
        }

        return message with
        {
            LocalizationSection = "Commands",
            LocalizationKey = result.MessageKey,
            LocalizationArguments = result.MessageArguments?.ToArray() ?? []
        };
    }
}
