using System.Globalization;
using System.Xml.Linq;

namespace Game.Servers;

public static class ServerDirectorySettings
{
    public static ServerDirectoryState Read(XElement settings, int defaultPort)
    {
        var container = settings.Element("ServerDirectory");
        if (container is null)
        {
            return new ServerDirectoryState();
        }

        return ServerDirectoryService.Normalize(new ServerDirectoryState
        {
            MyServers = ReadServers(container.Element("MyServers")),
            Favorites = ReadServers(container.Element("Favorites")),
            RecentServers = ReadServers(container.Element("RecentServers")),
            Subscriptions = ReadSubscriptions(container.Element("Subscriptions"))
        }, defaultPort);
    }

    public static void Write(XElement settings, ServerDirectoryState state, int defaultPort)
    {
        var normalized = ServerDirectoryService.Normalize(state, defaultPort);
        var container = new XElement("ServerDirectory",
            WriteServers("MyServers", normalized.MyServers),
            WriteServers("Favorites", normalized.Favorites),
            WriteServers("RecentServers", normalized.RecentServers),
            new XElement("Subscriptions", normalized.Subscriptions.Select(subscription =>
                new XElement("Subscription",
                    new XAttribute("Id", subscription.Id),
                    subscription.RegistrationId is null
                        ? null
                        : new XAttribute("RegistrationId", subscription.RegistrationId),
                    new XAttribute("Name", subscription.Name),
                    new XAttribute("ApiUrl", subscription.ApiUrl),
                    new XAttribute("IsEnabled", subscription.IsEnabled),
                    new XAttribute("Order", subscription.Order)))));
        settings.Elements("ServerDirectory").Remove();
        settings.Add(container);
    }

    private static IReadOnlyList<StoredServerEntry> ReadServers(XElement? container)
    {
        return container?.Elements("Server").Select(element => new StoredServerEntry
        {
            Id = Guid.Parse(Required(element, "Id")),
            Name = Required(element, "Name"),
            Address = Required(element, "Address"),
            Order = int.Parse(Required(element, "Order"), CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse(Required(element, "UpdatedAt"), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
        }).ToArray() ?? [];
    }

    private static IReadOnlyList<ServerSourceSubscription> ReadSubscriptions(XElement? container)
    {
        return container?.Elements("Subscription").Select(element => new ServerSourceSubscription
        {
            Id = Guid.Parse(Required(element, "Id")),
            RegistrationId = element.Attribute("RegistrationId")?.Value,
            Name = Required(element, "Name"),
            ApiUrl = Required(element, "ApiUrl"),
            IsEnabled = bool.Parse(Required(element, "IsEnabled")),
            Order = int.Parse(Required(element, "Order"), CultureInfo.InvariantCulture)
        }).ToArray() ?? [];
    }

    private static XElement WriteServers(string name, IEnumerable<StoredServerEntry> servers)
    {
        return new XElement(name, servers.Select(server => new XElement("Server",
            new XAttribute("Id", server.Id),
            new XAttribute("Name", server.Name),
            new XAttribute("Address", server.Address),
            new XAttribute("Order", server.Order),
            new XAttribute("UpdatedAt", server.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)))));
    }

    private static string Required(XElement element, string name)
    {
        return element.Attribute(name)?.Value ?? throw new FormatException($"Missing server directory attribute '{name}'.");
    }
}
