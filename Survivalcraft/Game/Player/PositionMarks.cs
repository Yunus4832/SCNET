using EntitySystem.TemplatesDatabase;

namespace Game;

public sealed class PositionMarks
{
    private readonly Dictionary<string, Vector3> _positions = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> Names => _positions.Keys;

    public static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 64 &&
        name == name.Trim() && !name.Any(char.IsControl);

    public void Set(string name, Vector3 position) => _positions[name] = position;

    public void SetIfMissing(string name, Vector3 position) => _positions.TryAdd(name, position);

    public bool TryGet(string name, out Vector3 position) => _positions.TryGetValue(name, out position);

    public void Load(ValuesDictionary values)
    {
        _positions.Clear();
        foreach (var entry in values)
        {
            _positions.Add(entry.Key, (Vector3)entry.Value);
        }
    }

    public ValuesDictionary Save()
    {
        var values = new ValuesDictionary();
        foreach (var entry in _positions)
        {
            values.SetValue(entry.Key, entry.Value);
        }

        return values;
    }
}
