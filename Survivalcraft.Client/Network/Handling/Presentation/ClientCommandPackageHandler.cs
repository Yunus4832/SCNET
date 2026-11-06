using Game.Application;
using Game.Commands;
using Game.Localization;
using Game.Network.Transport;

namespace Game.Network.Handling.Presentation;

public sealed class ClientCommandPackageHandler(CommandRegistry commands) : CommandPackageHandler(commands)
{
    public override void Handle(CommandPackage package, PackageReceiveContext context)
    {
        base.Handle(package, context);
        if (!IsAuthorityReply(context) || context.Project is not { } project) { return; }

        if (package is CommandSuggestionsPackage completion)
        {
            if (!context.Runtime.HasPresentation) { return; }

            (context.Runtime.HasPresentation
                    ? context.Project?.FindSubsystem<SubsystemCharacters>()?.LocalCharacter
                    : null)?.Entity.FindComponent<ComponentCharacterPresentation>(true)?.GameWidget
                .MessagePanel?.ReceiveSuggestions(
                    completion.Input,
                    completion.CorrelationId,
                    completion.Suggestions.Select(value => value with
                        {
                            Description = value.DescriptionSource is { } text
                                ? ClientLanguage.Resolver.Resolve(text)
                                : value.Description
                        }
                    ).ToArray()
                );
            return;
        }

        if (package is CommandResultPackage { Result: { } result })
        {
            var restored = CommandResponseBuilder.Restore(result);
            if (ClientApplication.PlayerCommands?.TryReceive(package.CorrelationId, restored) != true &&
                context.Runtime.HasPresentation)
            {
                DialogsManager.HideLoadingDialogs();
                CommandResultPublisher.DisplayLocal(project, restored, package.CorrelationId);
            }

            return;
        }

        if (package is CommandPermissionSnapshotPackage permissions &&
            context.Runtime.HasPresentation &&
            permissions.PlayerAddress is { } address &&
            context.Node!.Self?.PlayerAddress == address)
        {
            project.FindSubsystem<SubsystemMessages>(true)!.Service.DisplayLocal(
                GameMessage.Command(CommandText.Get("PermissionUpdated_Message", "你的指令权限已更新。"), success: true)
            );
        }
    }
}
