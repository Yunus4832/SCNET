using System.Globalization;
using System.Text;
using EntitySystem.TemplatesDatabase;

namespace Game;

public class PlayerStats
{
    [Stat] public long AirCreatureKills;

    [Stat] public long BlocksDug;

    [Stat] public long BlocksInteracted;

    [Stat] public long BlocksPlaced;

    [Stat] public string DeathRecordsString = string.Empty;

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
        from f in typeof(PlayerStats).GetRuntimeFields()
        where f.GetCustomAttribute<StatAttribute>() != null
        select f;

    public ReadOnlyList<DeathRecord> DeathRecords => new(_deathRecords);

    public void AddDeathRecord(DeathRecord deathRecord)
    {
        _deathRecords.Add(deathRecord);
    }

    public void Load(ValuesDictionary valuesDictionary)
    {
        DeathRecordsString = string.Empty;
        _deathRecords.Clear();
        foreach (var stat in Stats)
        {
            if (valuesDictionary.ContainsKey(stat.Name))
            {
                var value = valuesDictionary.GetValue<object>(stat.Name);
                stat.SetValue(this, value);
            }
        }

        if (string.IsNullOrEmpty(DeathRecordsString))
        {
            return;
        }

        var array = DeathRecordsString.Split([';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var s in array)
        {
            DeathRecord item = default;
            item.Load(s);
            _deathRecords.Add(item);
        }
    }

    public void Save(ValuesDictionary valuesDictionary)
    {
        var stringBuilder = new StringBuilder();
        foreach (var deathRecord in _deathRecords)
        {
            stringBuilder.Append(deathRecord.Save());
            stringBuilder.Append(';');
        }

        DeathRecordsString = stringBuilder.ToString();
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

        public string Cause;

        public void Load(string s)
        {
            var array = s.Split([','], StringSplitOptions.RemoveEmptyEntries);
            if (array.Length != 5)
            {
                throw new InvalidOperationException("Invalid death record.");
            }

            Day = double.Parse(array[0], CultureInfo.InvariantCulture);
            Location.X = float.Parse(array[1], CultureInfo.InvariantCulture);
            Location.Y = float.Parse(array[2], CultureInfo.InvariantCulture);
            Location.Z = float.Parse(array[3], CultureInfo.InvariantCulture);
            Cause = array[4];
        }

        public string Save()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(Day.ToString("R", CultureInfo.InvariantCulture));
            stringBuilder.Append(',');
            stringBuilder.Append(Location.X.ToString("R", CultureInfo.InvariantCulture));
            stringBuilder.Append(',');
            stringBuilder.Append(Location.Y.ToString("R", CultureInfo.InvariantCulture));
            stringBuilder.Append(',');
            stringBuilder.Append(Location.Z.ToString("R", CultureInfo.InvariantCulture));
            stringBuilder.Append(',');
            stringBuilder.Append(Cause);
            return stringBuilder.ToString();
        }
    }
}
