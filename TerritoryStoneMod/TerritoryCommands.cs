using Game;
using Game.Commands;
using Game.Localization;
using Game.Modding;
using Game.Subsystems;

namespace TerritoryStoneMod;

public sealed record RequestTerritoryAccessCommand(string Owner) : IGameCommand;
public sealed record DecideTerritoryAccessCommand(Guid Request, string Permissions) : IGameCommand;
public sealed record SetTerritoryGrantCommand(Guid Player, string Permissions) : IGameCommand;

public static class TerritoryCommands
{
    public static void Register(IModContext context)
    {
        context.Commands.Register(new ResourceId(context.Manifest.ModId, "access/request"),
            new CommandDefinition<RequestTerritoryAccessCommand>(
                (commandContext, command) => WithPlayer(commandContext,
                    (world, player) => world.RequestAccess(player, command.Owner)),
                CommandDomain.World, Description("RequestDescription"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                write: (writer, command) => writer.Write(command.Owner),
                read: reader => new RequestTerritoryAccessCommand(reader.ReadString())));
        context.Commands.Register(new ResourceId(context.Manifest.ModId, "access/decide"),
            new CommandDefinition<DecideTerritoryAccessCommand>(
                (commandContext, command) => WithPlayer(commandContext,
                    (world, player) => world.DecideAccess(player, command.Request, command.Permissions)),
                CommandDomain.World, Description("DecideDescription"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                write: (writer, command) => { writer.Write(command.Request); writer.Write(command.Permissions); },
                read: reader => new DecideTerritoryAccessCommand(reader.ReadGuid(), reader.ReadString())));
        context.Commands.Register(new ResourceId(context.Manifest.ModId, "access/set"),
            new CommandDefinition<SetTerritoryGrantCommand>(
                (commandContext, command) => WithPlayer(commandContext,
                    (world, player) => world.ChangeGrant(player, command.Player, command.Permissions)),
                CommandDomain.World, Description("GrantDescription"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                write: (writer, command) => { writer.Write(command.Player); writer.Write(command.Permissions); },
                read: reader => new SetTerritoryGrantCommand(reader.ReadGuid(), reader.ReadString())));
        context.Commands.Adapters.Register(new ResourceId(context.Manifest.ModId, "text/territory"),
            new TextCommand("territory", Description("Description"),
            [
                new CommandRoute([new CommandLiteral("request"),
                    new CommandArgument("owner", SuggestionProvider: SuggestOwners)], typeof(RequestTerritoryAccessCommand),
                    args => new RequestTerritoryAccessCommand(args.Get<string>("owner")),
                    Description("RequestDescription"))
            ]));
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestOwners(CommandSuggestionContext context)
    {
        if (context.Project?.FindSubsystem<TerritorySubsystem>() is not { } world ||
            context.Project.FindSubsystem<SubsystemPlayers>() is not { } players)
        {
            return [];
        }
        var owners = world.Store.Territories.Where(territory => territory.Owner != context.Principal.Player?.PlayerGUID &&
                (context.Principal.Player is not { } applicant || !territory.Grants.ContainsKey(applicant.PlayerGUID)))
            .Where(territory => players.PlayersData.Any(player => player.PlayerGUID == territory.Owner &&
                player.ComponentPlayer != null && (player.IsMainPlayer || player.Client?.IsConnected == true)))
            .Select(territory => (territory.Owner, Name: world.OwnerName(territory.Owner))).ToArray();
        return owners.Select(owner => new CommandArgumentSuggestion(
            owners.Count(other => string.Equals(other.Name, owner.Name, StringComparison.OrdinalIgnoreCase)) > 1
                ? owner.Owner.ToString("D") : owner.Name,
            LocalizedText.Literal(owner.Name)));
    }

    public static CommandResult Result(string key, bool success, params string[] arguments)
    {
        var fallback = Description(key).Resolve();
        return success
            ? CommandResult.LocalizedOk("territory." + key, "Territory_" + key, fallback, arguments)
            : CommandResult.LocalizedFail("territory." + key, "Territory_" + key, fallback, arguments);
    }

    private static LocalizedText Description(string key) => new("Commands", "Territory_" + key, key);

    private static CommandResult WithPlayer(CommandContext context, Func<TerritorySubsystem, PlayerData, CommandResult> action)
    {
        return context.Principal.Player is { } player &&
               context.Project?.FindSubsystem<TerritorySubsystem>() is { } world
            ? action(world, player)
            : Result("Unavailable", false);
    }
}
