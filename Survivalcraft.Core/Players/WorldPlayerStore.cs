using System.Globalization;

using EntitySystem.TemplatesDatabase;

using Game.Protocol.Identity;

namespace Game.Players;

/// <summary>World persistence and single-character ownership. Callers must independently authenticate sessions.</summary>
public sealed class WorldPlayerStore
{
    private readonly Dictionary<PlayerAddress, WorldPlayer> _players = new();

    public IReadOnlyCollection<WorldPlayer> Entries => _players.Values;

    public WorldPlayer? Find(PlayerAddress address) => _players.GetValueOrDefault(address);

    public WorldPlayer GetOrCreate(PlayerAddress address)
    {
        _ = address.Value;
        if (!_players.TryGetValue(address, out var player))
        {
            player = new WorldPlayer(address);
            _players.Add(address, player);
        }

        return player;
    }

    public void BindCharacter(PlayerAddress address, Guid characterId)
    {
        if (characterId == Guid.Empty)
        {
            throw new ArgumentException("A character identifier cannot be empty.", nameof(characterId));
        }

        var player = Find(address);
        if (player is { CharacterId: var existing } && existing != Guid.Empty && existing != characterId)
        {
            throw new InvalidOperationException("This player already owns a character.");
        }

        if (_players.Values.Any(other => other.Address != address && other.CharacterId == characterId))
        {
            throw new InvalidOperationException("This character already belongs to another player.");
        }

        GetOrCreate(address).CharacterId = characterId;
    }

    public void Ban(PlayerAddress address, string reason, PlayerAddress? actor, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > 512) { throw new ArgumentException("Ban reason is too long.", nameof(reason)); }

        if (actor is { } actorAddress) { _ = actorAddress.Value; }

        GetOrCreate(address).Ban = new PlayerBan(reason, actor, createdAt.ToUniversalTime());
    }

    public bool Unban(PlayerAddress address)
    {
        if (Find(address) is not { Ban: not null } player) { return false; }

        player.Ban = null;
        return true;
    }

    public ValuesDictionary Save()
    {
        var values = new ValuesDictionary();
        foreach (var player in _players.Values.OrderBy(player => player.Address.Value, StringComparer.Ordinal))
        {
            var record = new ValuesDictionary();
            record.SetValue("DisplayName", player.DisplayName);
            record.SetValue("CharacterId", player.CharacterId);
            record.SetValue("Permissions", player.Permissions.Save());
            record.SetValue("Muted", player.IsMuted);
            if (player.Ban is { } ban)
            {
                var restriction = new ValuesDictionary();
                restriction.SetValue("Reason", ban.Reason);
                restriction.SetValue("Actor", ban.Actor?.Value ?? string.Empty);
                restriction.SetValue("CreatedAt", ban.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
                record.SetValue("Ban", restriction);
            }

            values.SetValue(player.Address.Value, record);
        }

        return values;
    }

    public void Load(ValuesDictionary values)
    {
        // Validate the complete replacement before exposing it; a corrupt record cannot partially replace world players.
        var replacement = new WorldPlayerStore();
        foreach (var entry in values)
        {
            var address = new PlayerAddress(entry.Key);
            var record = entry.Value as ValuesDictionary ??
                         throw new InvalidDataException("Invalid world player record.");
            var player = replacement.GetOrCreate(address);
            player.DisplayName = record.GetValue<string>("DisplayName");
            player.Permissions.Load(record.GetValue<ValuesDictionary>("Permissions"));
            player.IsMuted = record.GetValue<bool>("Muted");
            var characterId = record.GetValue<Guid>("CharacterId");
            if (characterId != Guid.Empty) { replacement.BindCharacter(address, characterId); }

            if (record.ContainsKey("Ban"))
            {
                var restriction = record.GetValue<ValuesDictionary>("Ban");
                var actor = restriction.GetValue<string>("Actor");
                replacement.Ban(
                    address,
                    restriction.GetValue<string>("Reason"),
                    actor.Length == 0 ? null : new PlayerAddress(actor),
                    DateTimeOffset.ParseExact(
                        restriction.GetValue<string>("CreatedAt"),
                        "O",
                        CultureInfo.InvariantCulture
                    )
                );
            }
        }

        _players.Clear();
        foreach (var pair in replacement._players) { _players.Add(pair.Key, pair.Value); }
    }
}
