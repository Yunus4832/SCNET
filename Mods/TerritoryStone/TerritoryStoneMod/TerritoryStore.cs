using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Protocol.Identity;

namespace TerritoryStoneMod;

public sealed class TerritoryStore
{
    private readonly Dictionary<PlayerAddress, Territory> _territories = new();

    public IReadOnlyCollection<Territory> Territories => _territories.Values;

    public bool TryGet(PlayerAddress owner, out Territory? territory) => _territories.TryGetValue(owner, out territory);

    public Territory? Find(int x, int z) => _territories.Values.FirstOrDefault(territory => territory.Contains(x, z));

    public bool CanCreate(PlayerAddress owner, Point3 point)
    {
        if (owner == default || _territories.ContainsKey(owner))
        {
            return false;
        }

        return CanAdd(new Territory(owner, point));
    }

    public bool TryCreate(PlayerAddress owner, Point3 point)
    {
        if (!CanCreate(owner, point))
        {
            return false;
        }

        _territories.Add(owner, new Territory(owner, point));
        return true;
    }

    private bool CanAdd(Territory candidate) => candidate.Owner != default &&
                                                !_territories.ContainsKey(candidate.Owner) &&
                                                !_territories.Values.Any(candidate.Overlaps);

    public bool TryChangeSettings(PlayerAddress owner, TerritorySettings settings, out string error)
    {
        if (!settings.IsValid)
        {
            error = "InvalidSize";
            return false;
        }

        if (!_territories.TryGetValue(owner, out var territory))
        {
            error = "MissingTerritory";
            return false;
        }

        var candidate = new Territory(owner, territory.StonePoint, settings);
        if (_territories.Values.Any(other => other.Owner != owner && candidate.Overlaps(other)))
        {
            error = "Overlaps";
            return false;
        }

        territory.ApplySettings(settings);
        error = string.Empty;
        return true;
    }

    public bool RemoveStone(Point3 point)
    {
        var territory = _territories.Values.FirstOrDefault(territory => territory.StonePoint == point);
        return territory != null && _territories.Remove(territory.Owner);
    }

    public bool Remove(PlayerAddress owner) => _territories.Remove(owner);

    public void ApplySnapshot(IEnumerable<Territory> territories)
    {
        var replacement = new TerritoryStore();
        foreach (var territory in territories)
        {
            if (!replacement.CanAdd(territory))
            {
                throw new InvalidDataException("Invalid or overlapping territory snapshot.");
            }

            replacement._territories[territory.Owner] = territory;
        }

        _territories.Clear();
        foreach (var (owner, territory) in replacement._territories)
        {
            _territories.Add(owner, territory);
        }
    }

    public void ApplyUpdate(Territory territory)
    {
        ApplySnapshot(_territories.Values.Where(existing => existing.Owner != territory.Owner).Append(territory));
    }

    public void Load(ValuesDictionary values)
    {
        var territories = new List<Territory>();
        foreach (var (key, value) in values)
        {
            var data = (ValuesDictionary)value;
            var owner = new PlayerAddress(key);
            var settings = new TerritorySettings(
                data.GetValue<int>("Size"),
                data.GetValue<bool>("AllowEntry"),
                data.GetValue<bool>("AllowExit"),
                data.GetValue<bool>("AllowDigging"),
                data.GetValue<bool>("AllowPlacing"),
                data.GetValue<bool>("ShowBoundary")
            );
            var territory = new Territory(owner, data.GetValue<Point3>("StonePoint"), settings);
            foreach (var (user, grantData) in data.GetValue<ValuesDictionary>("Grants"))
            {
                var grant = (ValuesDictionary)grantData;
                territory.SetGrant(
                    new PlayerAddress(user),
                    grant.GetValue<string>("Name"),
                    (TerritoryPermission)grant.GetValue<int>("Permissions")
                );
            }

            territories.Add(territory);
        }

        ApplySnapshot(territories);
    }

    public ValuesDictionary Save()
    {
        var values = new ValuesDictionary();
        foreach (var territory in _territories.Values)
        {
            var data = new ValuesDictionary();
            data.SetValue("StonePoint", territory.StonePoint);
            var settings = territory.Settings;
            data.SetValue("Size", settings.Size);
            data.SetValue("AllowEntry", settings.AllowEntry);
            data.SetValue("AllowExit", settings.AllowExit);
            data.SetValue("AllowDigging", settings.AllowDigging);
            data.SetValue("AllowPlacing", settings.AllowPlacing);
            var grants = new ValuesDictionary();
            foreach (var (user, grant) in territory.Grants)
            {
                var entry = new ValuesDictionary();
                entry.SetValue("Name", grant.Name);
                entry.SetValue("Permissions", (int)grant.Permissions);
                grants.SetValue(user.Value, entry);
            }

            data.SetValue("Grants", grants);
            data.SetValue("ShowBoundary", settings.ShowBoundary);
            values.SetValue(territory.Owner.Value, data);
        }

        return values;
    }
}
