using EntitySystem.Core;

using Game.Messaging;

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

        var messages = project.FindSubsystem<SubsystemGameWidgets>(true)!.Messages;
        var message = GameMessage.Command(result.Message, result.Success);
        if (result.Audience is CommandResultAudience.AllPlayers && !result.Sensitive)
        {
            message = message with
            {
                Presentation =
                    GameMessagePresentation.Default | GameMessagePresentation.Toast
            };
            messages.Publish(message, includePublisher: includeServer);
        }
        else if (requesterId.HasValue)
        {
            messages.Publish(message, [requesterId.Value], includePublisher: false);
        }
        else if (includeServer)
        {
            messages.DisplayLocal(message);
        }
    }
}
