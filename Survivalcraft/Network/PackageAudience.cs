namespace Game.Network;

public abstract record PackageAudience
{
    public static PackageAudience Global { get; } = new GlobalAudience();

    public static PackageAudience To(Client client) => new ClientAudience(client);

    public static PackageAudience To(IReadOnlyCollection<Client> clients) =>
        new ClientSetAudience(new HashSet<Client>(clients, ReferenceEqualityComparer.Instance));

    public static PackageAudience Except(Client client) => new ExceptClientAudience(client);

    internal abstract bool Includes(Client client);

    internal abstract bool HasSameRecipients(PackageAudience other);

    private sealed record GlobalAudience : PackageAudience
    {
        internal override bool Includes(Client client) => true;

        internal override bool HasSameRecipients(PackageAudience other) => other is GlobalAudience;
    }

    private sealed record ClientAudience(Client Client) : PackageAudience
    {
        internal override bool Includes(Client client) => ReferenceEquals(Client, client);

        internal override bool HasSameRecipients(PackageAudience other) =>
            other is ClientAudience audience && ReferenceEquals(Client, audience.Client);
    }

    private sealed record ClientSetAudience(HashSet<Client> Clients) : PackageAudience
    {
        internal override bool Includes(Client client) => Clients.Contains(client);

        internal override bool HasSameRecipients(PackageAudience other) =>
            other is ClientSetAudience audience && Clients.SetEquals(audience.Clients);
    }

    private sealed record ExceptClientAudience(Client Client) : PackageAudience
    {
        internal override bool Includes(Client client) => !ReferenceEquals(Client, client);

        internal override bool HasSameRecipients(PackageAudience other) =>
            other is ExceptClientAudience audience && ReferenceEquals(Client, audience.Client);
    }
}
