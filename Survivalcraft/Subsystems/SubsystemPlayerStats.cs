using System.Globalization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemPlayerStats : Subsystem
{
    private readonly Dictionary<int, PlayerStats> _playerStats = new();

    public PlayerStats GetPlayerStats(int playerIndex)
    {
        if (_playerStats.TryGetValue(playerIndex, out var value))
        {
            return value;
        }

        value = new PlayerStats();
        _playerStats.Add(playerIndex, value);

        return value;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        foreach (var item in valuesDictionary.GetValue<ValuesDictionary>("Stats"))
        {
            var playerStats = new PlayerStats();
            playerStats.Load((ValuesDictionary)item.Value);
            _playerStats.Add(int.Parse(item.Key, CultureInfo.InvariantCulture), playerStats);
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Stats", valuesDictionary2);
        foreach (var playerStat in _playerStats)
        {
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary2.SetValue(playerStat.Key.ToString(CultureInfo.InvariantCulture), valuesDictionary3);
            playerStat.Value.Save(valuesDictionary3);
        }
    }
}
