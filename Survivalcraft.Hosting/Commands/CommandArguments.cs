namespace Game.Commands;

public sealed class CommandArguments(Dictionary<string, object> values)
{
    public T Get<T>(string name)
    {
        if (!values.TryGetValue(name, out var value))
        {
            throw new KeyNotFoundException($"Command argument \"{name}\" was not supplied.");
        }

        return (T)value;
    }

    public bool TryGet<T>(string name, out T? value)
    {
        if (values.TryGetValue(name, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }
}
