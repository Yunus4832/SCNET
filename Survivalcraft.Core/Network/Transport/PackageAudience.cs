namespace Game.Network.Transport;

public abstract record PackageAudience
{
    public static PackageAudience Global { get; } = new GlobalAudience();

    public static PackageAudience To(WorldConnection client) => new ClientAudience(client);

    public static PackageAudience To(IReadOnlyCollection<WorldConnection> clients) =>
        new ClientSetAudience(new HashSet<WorldConnection>(clients, ReferenceEqualityComparer.Instance));

    public static PackageAudience Except(WorldConnection client) => new ExceptClientAudience(client);

    public WorldConnection? SingleRecipient => this switch
    {
        ClientAudience single => single.WorldConnection,
        ClientSetAudience { Clients.Count: 1 } set => set.Clients.First(),
        _ => null
    };

    public abstract bool Includes(WorldConnection client);

    public abstract bool HasSameRecipients(PackageAudience other);

    private sealed record GlobalAudience : PackageAudience
    {
        public override bool Includes(WorldConnection client) => true;

        public override bool HasSameRecipients(PackageAudience other) => other is GlobalAudience;
    }

    private sealed record ClientAudience(WorldConnection WorldConnection) : PackageAudience
    {
        public override bool Includes(WorldConnection client) => ReferenceEquals(WorldConnection, client);

        public override bool HasSameRecipients(PackageAudience other) =>
            other is ClientAudience audience && ReferenceEquals(WorldConnection, audience.WorldConnection);
    }

    private sealed record ClientSetAudience(HashSet<WorldConnection> Clients) : PackageAudience
    {
        public override bool Includes(WorldConnection client) => Clients.Contains(client);

        public override bool HasSameRecipients(PackageAudience other) =>
            other is ClientSetAudience audience && Clients.SetEquals(audience.Clients);
    }

    private sealed record ExceptClientAudience(WorldConnection WorldConnection) : PackageAudience
    {
        public override bool Includes(WorldConnection client) => !ReferenceEquals(WorldConnection, client);

        public override bool HasSameRecipients(PackageAudience other) =>
            other is ExceptClientAudience audience && ReferenceEquals(WorldConnection, audience.WorldConnection);
    }
}
