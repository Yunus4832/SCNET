using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Core;

public sealed class ProjectExtensionData
{
    private readonly ValuesDictionary _values = new();

    public ValuesDictionary Get(string owner, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var datasets = GetOrCreate(_values, owner);
        return GetOrCreate(datasets, key);
    }

    public void Load(ValuesDictionary values)
    {
        _values.Clear();
        _values.ApplyOverrides(values);
    }

    public ValuesDictionary Save()
    {
        var snapshot = new ValuesDictionary();
        snapshot.ApplyOverrides(_values);
        return snapshot;
    }

    private static ValuesDictionary GetOrCreate(ValuesDictionary parent, string key)
    {
        if (parent.ContainsKey(key))
        {
            return parent.GetValue<ValuesDictionary>(key);
        }

        var values = new ValuesDictionary();
        parent.SetValue(key, values);
        return values;
    }
}
