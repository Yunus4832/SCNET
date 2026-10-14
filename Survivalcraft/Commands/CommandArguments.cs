namespace Game.Commands;

public sealed class CommandArguments
{
    private readonly Dictionary<string, object> _values;

    internal CommandArguments(Dictionary<string, object> values)
    {
        _values = values;
    }

    public T Get<T>(string name)
    {
        if (!_values.TryGetValue(name, out var value))
        {
            throw new KeyNotFoundException($"Command argument \"{name}\" was not supplied.");
        }

        return (T)value;
    }

    public bool TryGet<T>(string name, out T? value)
    {
        if (_values.TryGetValue(name, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }
}
