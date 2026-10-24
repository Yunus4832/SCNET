using System.Collections.ObjectModel;

using Game.Protocol.Identity;

namespace Game.Teams;

public sealed class Team
{
    private readonly HashSet<PlayerAddress> _members;

    public Guid Id { get; }

    public string Name { get; }

    public PlayerAddress Leader { get; }

    public IReadOnlySet<PlayerAddress> Members { get; }

    internal Team(Guid id, string name, PlayerAddress leader, IEnumerable<PlayerAddress> members)
    {
        Id = id;
        Name = name;
        Leader = leader;
        var values = members.ToArray();
        _members = new HashSet<PlayerAddress>(values);
        if (_members.Count != values.Length) { throw new InvalidDataException("Duplicate team member."); }

        foreach (var member in values) { _ = member.Value; }

        Members = new ReadOnlySet<PlayerAddress>(_members);
    }

    internal void AddMember(PlayerAddress player) => _members.Add(player);

    internal void RemoveMember(PlayerAddress player) => _members.Remove(player);
}
