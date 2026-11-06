using System.Globalization;

using EntitySystem.TemplatesDatabase;

namespace Game.Characters;

public class CharacterStats
{
    [Stat] public long AirCreatureKills;

    [Stat] public long BlocksDug;

    [Stat] public long BlocksInteracted;

    [Stat] public long BlocksPlaced;


    [Stat] public double DeepestDive;

    [Stat] public double DistanceClimbed;

    [Stat] public double DistanceFallen;

    [Stat] public double DistanceFlown;

    [Stat] public double DistanceRidden;

    [Stat] public double DistanceSwam;

    [Stat] public double DistanceTravelled;

    [Stat] public double DistanceWalked;

    [Stat] public GameMode EasiestModeUsed = GameMode.Creative;

    [Stat] public long FoodItemsEaten;

    [Stat] public long FurnitureItemsMade;

    [Stat] public double HighestAltitude = int.MinValue;

    [Stat] public float HighestLevel;

    [Stat] public long HitsReceived;

    [Stat] public long ItemsCrafted;

    [Stat] public long Jumps;

    [Stat] public long LandCreatureKills;

    [Stat] public double LowestAltitude = int.MaxValue;

    private readonly List<DeathRecord> _deathRecords = [];

    [Stat] public long MeleeAttacks;

    [Stat] public long MeleeHits;

    [Stat] public long PlayerKills;

    [Stat] public long RangedAttacks;

    [Stat] public long RangedHits;

    [Stat] public long StruckByLightning;

    [Stat] public long TimesHadFlu;

    [Stat] public double TimeSlept;

    [Stat] public long TimesPuked;

    [Stat] public long TimesWasSick;

    [Stat] public long TimesWentToSleep;

    [Stat] public double TotalHealthLost;

    [Stat] public long WaterCreatureKills;

    public IEnumerable<FieldInfo> Stats =>
        from f in typeof(CharacterStats).GetRuntimeFields()
        where f.GetCustomAttribute<StatAttribute>() != null
        select f;

    public ReadOnlyList<DeathRecord> DeathRecords => new(_deathRecords);

    public void AddDeathRecord(DeathRecord deathRecord)
    {
        _deathRecords.Add(deathRecord);
    }

    public void Load(ValuesDictionary valuesDictionary)
    {
        _deathRecords.Clear();
        ApplySnapshot(valuesDictionary);
    }

    public void ApplySnapshot(ValuesDictionary valuesDictionary)
    {
        foreach (var stat in Stats)
        {
            if (valuesDictionary.ContainsKey(stat.Name))
            {
                var value = valuesDictionary.GetValue<object>(stat.Name);
                stat.SetValue(this, value);
            }
        }

        if (!valuesDictionary.ContainsKey("DeathRecords")) { return; }

        _deathRecords.Clear();
        var records = valuesDictionary.GetValue<ValuesDictionary>("DeathRecords");
        for (var i = 0; i < records.Count; i++)
        {
            var data = records.GetValue<ValuesDictionary>(i.ToString(CultureInfo.InvariantCulture));
            _deathRecords.Add(
                new DeathRecord
                {
                    Day = data.GetValue<double>("Day"), Location = data.GetValue<Vector3>("Location"),
                    Cause = Localization.LocalizedTextStorage.Load(data.GetValue<ValuesDictionary>("Cause"))
                }
            );
        }
    }

    private ValuesDictionary SaveDeathRecords()
    {
        var result = new ValuesDictionary();
        for (var i = 0; i < _deathRecords.Count; i++)
        {
            var record = _deathRecords[i];
            result.SetValue(
                i.ToString(CultureInfo.InvariantCulture),
                new ValuesDictionary
                {
                    { "Day", record.Day }, { "Location", record.Location },
                    { "Cause", Localization.LocalizedTextStorage.Save(record.Cause) }
                }
            );
        }

        return result;
    }

    public void SaveCombat(ValuesDictionary valuesDictionary)
    {
        valuesDictionary.SetValue(nameof(AirCreatureKills), AirCreatureKills);
        valuesDictionary.SetValue("DeathRecords", SaveDeathRecords());
        valuesDictionary.SetValue(nameof(HitsReceived), HitsReceived);
        valuesDictionary.SetValue(nameof(MeleeAttacks), MeleeAttacks);
        valuesDictionary.SetValue(nameof(MeleeHits), MeleeHits);
        valuesDictionary.SetValue(nameof(LandCreatureKills), LandCreatureKills);
        valuesDictionary.SetValue(nameof(PlayerKills), PlayerKills);
        valuesDictionary.SetValue(nameof(TotalHealthLost), TotalHealthLost);
        valuesDictionary.SetValue(nameof(WaterCreatureKills), WaterCreatureKills);
    }

    public void SaveSurvival(ValuesDictionary valuesDictionary)
    {
        valuesDictionary.SetValue(nameof(FoodItemsEaten), FoodItemsEaten);
        valuesDictionary.SetValue(nameof(TimesWasSick), TimesWasSick);
        valuesDictionary.SetValue(nameof(TimesPuked), TimesPuked);
        valuesDictionary.SetValue(nameof(TimesHadFlu), TimesHadFlu);
        valuesDictionary.SetValue(nameof(TimesWentToSleep), TimesWentToSleep);
        valuesDictionary.SetValue(nameof(TimeSlept), TimeSlept);
    }

    public void Save(ValuesDictionary valuesDictionary)
    {
        valuesDictionary.SetValue("DeathRecords", SaveDeathRecords());
        foreach (var stat in Stats)
        {
            var value = stat.GetValue(this);
            if (value is null)
            {
                continue;
            }

            valuesDictionary.SetValue(stat.Name, value);
        }
    }

    public class StatAttribute : Attribute
    {
    }

    public struct DeathRecord
    {
        public double Day;

        public Vector3 Location;

        public LocalizedText Cause;
    }
}
