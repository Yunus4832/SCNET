using Game.Localization;

namespace Game.Commands;

/// <summary>
///     Declares textual names and routes for the text frontend. This is a binding,
///     not an executable command definition.
/// </summary>
public sealed class TextCommand : ICommandAdapterBinding
{
    public string Name { get; }

    public LocalizedText Description { get; }

    public IReadOnlyList<string> Aliases { get; }

    public IReadOnlyList<CommandRoute> Routes { get; }

    public TextCommand(
        string name,
        LocalizedText description,
        IEnumerable<CommandRoute> routes,
        IEnumerable<string>? aliases = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = ValidateName(name, nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Routes = routes?.ToArray() ?? throw new ArgumentNullException(nameof(routes));
        Aliases = aliases?.Select(alias => ValidateName(alias, nameof(aliases))).ToArray() ?? [];
        if (Routes.Count == 0)
        {
            throw new ArgumentException("A command must define at least one route.", nameof(routes));
        }

        foreach (var route in Routes)
        {
            var duplicateArgument = route.Segments
                .OfType<CommandArgument>()
                .GroupBy(argument => argument.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateArgument is not null)
            {
                throw new ArgumentException(
                    $"Command route contains duplicate argument \"{duplicateArgument.Key}\".",
                    nameof(routes));
            }

            foreach (var argument in route.Segments.OfType<CommandArgument>())
            {
                if (argument.Choices?.Any(choice =>
                        string.IsNullOrWhiteSpace(choice) || choice.Any(char.IsWhiteSpace)) == true)
                {
                    throw new ArgumentException(
                        $"Choices for argument \"{argument.Name}\" must be non-empty single tokens.",
                        nameof(routes));
                }
            }
        }
    }

    private static string ValidateName(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.StartsWith('/') || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "Command names and aliases must be single tokens without a leading slash.",
                parameterName);
        }

        return value;
    }
}
