using Engine.Core;

using EntitySystem.TemplatesDatabase;

namespace TerritoryStoneMod;

public sealed class TerritoryStore
{
    private readonly Dictionary<Guid, Territory> _territories = new();

    public IReadOnlyCollection<Territory> Territories => _territories.Values;

    public bool TryGet(Guid owner, out Territory? territory) => _territories.TryGetValue(owner, out territory);

    public Territory? Find(int x, int z) => _territories.Values.FirstOrDefault(territory => territory.Contains(x, z));

    public bool CanCreate(Guid owner, Point3 point)
    {
        if (owner == Guid.Empty || _territories.ContainsKey(owner))
        {
            return false;
        }

        var candidate = new Territory(owner, point);
        return !_territories.Values.Any(candidate.Overlaps);
    }

    public bool TryCreate(Guid owner, Point3 point)
    {
        if (!CanCreate(owner, point))
        {
            return false;
        }

        _territories.Add(owner, new Territory(owner, point));
        return true;
    }

    public bool RemoveStone(Point3 point)
    {
        var territory = _territories.Values.FirstOrDefault(territory => territory.StonePoint == point);
        return territory != null && _territories.Remove(territory.Owner);
    }

    public bool Remove(Guid owner) => _territories.Remove(owner);

    public void ApplySnapshot(IEnumerable<Territory> territories)
    {
        var replacement = new TerritoryStore();
        foreach (var territory in territories)
        {
            if (!replacement.TryCreate(territory.Owner, territory.StonePoint))
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
            var owner = Guid.ParseExact(key, "N");
            var territory = new Territory(owner, data.GetValue<Point3>("StonePoint"))
            {
                ApplyToTeam = data.GetValue<bool>("ApplyToTeam"),
                ShowBoundary = data.GetValue<bool>("ShowBoundary"),
                RestrictEntry = data.GetValue<bool>("RestrictEntry")
            };
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
            data.SetValue("ApplyToTeam", territory.ApplyToTeam);
            data.SetValue("ShowBoundary", territory.ShowBoundary);
            data.SetValue("RestrictEntry", territory.RestrictEntry);
            values.SetValue(territory.Owner.ToString("N"), data);
        }

        return values;
    }
}
