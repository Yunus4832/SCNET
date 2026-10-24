using EntitySystem.TemplatesDatabase;

using Game.Protocol.Identity;

namespace Game;

public sealed class PositionMarkStore
{
    private readonly Dictionary<PlayerAddress, PositionMarks> _privateMarks = new();

    public PositionMarks PublicMarks { get; } = new();

    public PositionMarks GetPrivateMarks(PlayerAddress address)
    {
        if (!_privateMarks.TryGetValue(address, out var marks))
        {
            marks = new PositionMarks();
            _privateMarks.Add(address, marks);
        }

        return marks;
    }

    public void Load(ValuesDictionary values)
    {
        PublicMarks.Load(values.GetValue("Public", new ValuesDictionary()));
        _privateMarks.Clear();
        foreach (var entry in values.GetValue("Private", new ValuesDictionary()))
        {
            GetPrivateMarks(new PlayerAddress(entry.Key)).Load((ValuesDictionary)entry.Value);
        }
    }

    public ValuesDictionary Save()
    {
        var personal = new ValuesDictionary();
        foreach (var (address, marks) in _privateMarks)
        {
            personal.SetValue(address.Value, marks.Save());
        }

        var values = new ValuesDictionary();
        values.SetValue("Public", PublicMarks.Save());
        values.SetValue("Private", personal);
        return values;
    }
}
