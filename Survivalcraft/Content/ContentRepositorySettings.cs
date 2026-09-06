using System.Xml.Linq;

namespace Game.Content;

public static class ContentRepositorySettings
{
    public static IReadOnlyList<ContentRepository> Read(XElement settings)
    {
        var container = settings.Element("ContentRepositories");
        if (container is null)
        {
            return Array.Empty<ContentRepository>();
        }

        return ContentRepository.NormalizeAll(container.Elements("Repository").Select(item => new ContentRepository
        {
            Id = Guid.Parse(Required(item, "Id")),
            Name = Required(item, "Name"),
            BaseUrl = Required(item, "BaseUrl"),
            IsEnabled = bool.Parse(Required(item, "IsEnabled")),
            Priority = int.Parse(Required(item, "Priority"), System.Globalization.CultureInfo.InvariantCulture)
        }));
    }

    public static void Write(XElement settings, IEnumerable<ContentRepository> repositories)
    {
        var normalized = ContentRepository.NormalizeAll(repositories);
        var container = new XElement("ContentRepositories", normalized.Select(item => new XElement("Repository",
            new XAttribute("Id", item.Id),
            new XAttribute("Name", item.Name),
            new XAttribute("BaseUrl", item.BaseUrl),
            new XAttribute("IsEnabled", item.IsEnabled),
            new XAttribute("Priority", item.Priority))));
        settings.Elements("ContentRepositories").Remove();
        settings.Add(container);
    }

    private static string Required(XElement item, string name)
    {
        return item.Attribute(name)?.Value ?? throw new FormatException($"Missing repository attribute '{name}'.");
    }
}
