using EntitySystem.Core;

namespace Game.Commands;

public sealed class CommandContext(
    CommandInvocationChannel channel,
    CommandPrincipal principal,
    Project? project,
    string? correlationId = null
)
{
    public CommandInvocationChannel Channel { get; } = channel;

    public CommandPrincipal Principal { get; } = principal ?? throw new ArgumentNullException(nameof(principal));

    public Project? Project { get; } = project;

    public string CorrelationId { get; } = string.IsNullOrWhiteSpace(correlationId)
        ? Guid.NewGuid().ToString("N")
        : correlationId;

    public CommandRegistry Registry { get; internal set; } = null!;
}
