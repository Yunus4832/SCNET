using Game.Localization;

namespace Game.Commands;

/// <summary>
///     Describes one textual route and converts parsed arguments into a typed command.
/// </summary>
public sealed class CommandRoute(
    IEnumerable<CommandSegment> segments,
    Type commandType,
    Func<CommandArguments, IGameCommand> createCommand,
    LocalizedText? description = null
)
{
    public IReadOnlyList<CommandSegment> Segments { get; } =
        segments?.ToArray() ?? throw new ArgumentNullException(nameof(segments));

    public LocalizedText Description { get; } =
        description ?? LocalizedText.Empty;

    public Type CommandType { get; } =
        commandType ?? throw new ArgumentNullException(nameof(commandType));

    public Func<CommandArguments, IGameCommand> CreateCommand { get; } =
        createCommand ?? throw new ArgumentNullException(nameof(createCommand));
}
