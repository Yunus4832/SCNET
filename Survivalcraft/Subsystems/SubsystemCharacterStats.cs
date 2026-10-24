using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemCharacterStats : Subsystem
{
    private readonly Dictionary<Guid, CharacterStats> _characterStats = new();

    public CharacterStats GetCharacterStats(Guid characterId)
    {
        if (characterId == Guid.Empty)
        {
            throw new ArgumentException("A character identifier is required.", nameof(characterId));
        }

        if (!_characterStats.TryGetValue(characterId, out var stats))
        {
            stats = new CharacterStats();
            _characterStats.Add(characterId, stats);
        }

        return stats;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        var replacement = new Dictionary<Guid, CharacterStats>();
        foreach (var item in valuesDictionary.GetValue<ValuesDictionary>("Stats"))
        {
            if (!Guid.TryParseExact(item.Key, "N", out var characterId) ||
                characterId == Guid.Empty ||
                replacement.ContainsKey(characterId) ||
                item.Value is not ValuesDictionary values)
            {
                throw new InvalidDataException("Invalid character statistics record.");
            }

            var stats = new CharacterStats();
            stats.Load(values);
            replacement.Add(characterId, stats);
        }

        _characterStats.Clear();
        foreach (var pair in replacement) { _characterStats.Add(pair.Key, pair.Value); }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var records = new ValuesDictionary();
        foreach (var pair in _characterStats.OrderBy(pair => pair.Key))
        {
            var values = new ValuesDictionary();
            pair.Value.Save(values);
            records.SetValue(pair.Key.ToString("N"), values);
        }

        valuesDictionary.SetValue("Stats", records);
    }
}
