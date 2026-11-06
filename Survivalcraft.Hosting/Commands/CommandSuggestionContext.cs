using EntitySystem.Core;

namespace Game.Commands;

public sealed class CommandSuggestionContext(
    CommandRegistry registry,
    CommandPrincipal principal,
    CommandInvocationChannel channel,
    IReadOnlyList<string> completedTokens,
    Project? project
)
{
    public CommandRegistry Registry { get; } = registry;

    public CommandPrincipal Principal { get; } = principal;

    public CommandInvocationChannel Channel { get; } = channel;

    public IReadOnlyList<string> CompletedTokens { get; } = completedTokens;

    public Project? Project { get; } = project;
}
