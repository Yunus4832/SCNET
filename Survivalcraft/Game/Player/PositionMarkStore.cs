using EntitySystem.TemplatesDatabase;

namespace Game;

public sealed class PositionMarkStore
{
    private readonly Dictionary<Guid, PositionMarks> _privateMarks = new();

    public PositionMarks PublicMarks { get; } = new();

    public PositionMarks GetPrivateMarks(Guid playerGuid)
    {
        if (!_privateMarks.TryGetValue(playerGuid, out var marks))
        {
            marks = new PositionMarks();
            _privateMarks.Add(playerGuid, marks);
        }

        return marks;
    }

    public void Load(ValuesDictionary values)
    {
        PublicMarks.Load(values.GetValue("Public", new ValuesDictionary()));
        _privateMarks.Clear();
        foreach (var entry in values.GetValue("Private", new ValuesDictionary()))
        {
            GetPrivateMarks(Guid.ParseExact(entry.Key, "N")).Load((ValuesDictionary)entry.Value);
        }
    }

    public ValuesDictionary Save()
    {
        var personal = new ValuesDictionary();
        foreach (var (playerGuid, marks) in _privateMarks)
        {
            personal.SetValue(playerGuid.ToString("N"), marks.Save());
        }

        var values = new ValuesDictionary();
        values.SetValue("Public", PublicMarks.Save());
        values.SetValue("Private", personal);
        return values;
    }
}
