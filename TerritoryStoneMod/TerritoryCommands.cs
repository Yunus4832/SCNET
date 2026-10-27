using Game.Commands;
using Game.Localization;
using Game.Modding;
using Game.Network;
using Game.Players;
using Game.Protocol;
using Game.Protocol.Identity;
using Game.Protocol.Localization;
using Game.Subsystems;

using MessagePack;

namespace TerritoryStoneMod;

[MessagePackObject]
public sealed record RequestTerritoryAccessCommand([property: Key(0)] string Owner) : IGameCommand;

[MessagePackObject]
public sealed record DecideTerritoryAccessCommand(
    [property: Key(0)] Guid Request,
    [property: Key(1)] string Permissions
) : IGameCommand;

[MessagePackObject]
public sealed record SetTerritoryGrantCommand(
    [property: Key(0)] PlayerAddress User,
    [property: Key(1)] string Permissions
) : IGameCommand;

public static class TerritoryCommands
{
    public static void Register(IModContext context)
    {
        context.Commands.Register(
            new ResourceId(context.Manifest.ModId, "access/request"),
            new CommandDefinition<RequestTerritoryAccessCommand>(
                (commandContext, command) => WithSubject(
                    commandContext,
                    (world, user) => world.RequestAccess(user, command.Owner)
                ),
                CommandDomain.World,
                Description("RequestDescription"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        context.Commands.Register(
            new ResourceId(context.Manifest.ModId, "access/decide"),
            new CommandDefinition<DecideTerritoryAccessCommand>(
                (commandContext, command) => WithSubject(
                    commandContext,
                    (world, user) => world.DecideAccess(user, command.Request, command.Permissions)
                ),
                CommandDomain.World,
                Description("DecideDescription"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        context.Commands.Register(
            new ResourceId(context.Manifest.ModId, "access/set"),
            new CommandDefinition<SetTerritoryGrantCommand>(
                (commandContext, command) => WithSubject(
                    commandContext,
                    (world, user) => world.ChangeGrant(user, command.User, command.Permissions)
                ),
                CommandDomain.World,
                Description("GrantDescription"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        context.Commands.Adapters.Register(
            new ResourceId(context.Manifest.ModId, "text/territory"),
            new TextCommand(
                "territory",
                Description("Description"),
                [
                    new CommandRoute(
                        [
                            new CommandLiteral("request"),
                            new CommandArgument("owner", SuggestionProvider: SuggestOwners)
                        ],
                        typeof(RequestTerritoryAccessCommand),
                        args => new RequestTerritoryAccessCommand(args.Get<string>("owner")),
                        Description("RequestDescription")
                    )
                ]
            )
        );
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestOwners(CommandSuggestionContext context)
    {
        if (context.Project?.FindSubsystem<TerritorySubsystem>() is not { } world ||
            context.Project.FindSubsystem<SubsystemCharacters>() is not { } players)
        {
            return [];
        }

        return world.Store.Territories.Where(territory =>
                territory.Owner != context.Principal.Subject?.Address &&
                (context.Principal.Subject is not { } applicant || !territory.Grants.ContainsKey(applicant.Address)) &&
                PlayerSessionQuery.Find(territory.Owner) is not null
            )
            .Select(territory => new CommandArgumentSuggestion(
                    territory.Owner.Value,
                    LocalizedText.Literal(world.OwnerName(territory.Owner))
                )
            );
    }

    public static CommandResult Result(string key, bool success, params string[] arguments)
    {
        var fallback = Description(key).Resolve();
        return success
            ? CommandResult.LocalizedOk("territory." + key, "Territory_" + key, fallback, arguments)
            : CommandResult.LocalizedFail("territory." + key, "Territory_" + key, fallback, arguments);
    }

    private static LocalizedText Description(string key) => new("Commands", "Territory_" + key, key);

    private static CommandResult WithSubject(
        CommandContext context,
        Func<TerritorySubsystem, WorldPlayer, CommandResult> action
    )
    {
        return context.Principal.Subject is { Ban: null } user &&
               context.Project?.FindSubsystem<TerritorySubsystem>() is { } world &&
               ReferenceEquals(
                   context.Project.FindSubsystem<SubsystemPlayers>()?.Store.Find(user.Address),
                   user
               ) &&
               PlayerSessionQuery.Find(user.Address) is not null
            ? action(world, user)
            : Result("Unavailable", false);
    }
}
