using System.Globalization;
using System.Xml.Linq;

using Engine.Network;

using Game.Servers;

namespace Game.Managers;

public sealed class RendezvousManager
{
    private readonly Lock _gate = new();

    private readonly Action<RendezvousSettings> _save;

    private IReadOnlyList<InstalledRendezvousService> _services;

    public static RendezvousManager Current { get; private set; } = new(new RendezvousSettings(), _ => { });

    public RendezvousManager(RendezvousSettings settings, Action<RendezvousSettings> save)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(save);
        _services = Normalize(settings).Services;
        _save = save;
    }

    public static void Initialize()
    {
        try
        {
            Current = new RendezvousManager(Load(), Save);
        }
        catch (Exception exception)
        {
            ExceptionManager.ReportExceptionToUser("Loading signaling services failed.", exception);
            Current = new RendezvousManager(new RendezvousSettings(), Save);
        }
    }

    public IReadOnlyList<InstalledRendezvousService> Snapshot()
    {
        lock (_gate)
        {
            return _services.ToArray();
        }
    }

    public void Add(InstalledRendezvousService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        lock (_gate)
        {
            Commit(_services.Append(service));
        }
    }

    public void Edit(InstalledRendezvousService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        lock (_gate)
        {
            EnsureExists(service.Id);
            Commit(_services.Select(item => item.Id == service.Id ? service : item));
        }
    }

    public void Delete(Guid id)
    {
        lock (_gate)
        {
            EnsureExists(id);
            Commit(_services.Where(item => item.Id != id));
        }
    }

    public void SetOrder(IReadOnlyList<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_gate)
        {
            if (ids.Count != _services.Count ||
                ids.Distinct().Count() != ids.Count ||
                ids.Any(id => _services.All(item => item.Id != id)))
            {
                throw new ArgumentException("Order must contain every installed signaling service once.", nameof(ids));
            }

            var byId = _services.ToDictionary(item => item.Id);
            Commit(ids.Select((id, order) => byId[id] with { Order = order }));
        }
    }

    private void EnsureExists(Guid id)
    {
        if (_services.All(item => item.Id != id))
        {
            throw new KeyNotFoundException("The signaling service does not exist.");
        }
    }

    private void Commit(IEnumerable<InstalledRendezvousService> services)
    {
        var settings = Normalize(new RendezvousSettings { Services = services.ToArray() });
        _save(settings);
        _services = settings.Services;
    }

    public static RendezvousSettings Load()
    {
        if (!Storage.FileExists(GamePaths.RendezvousFile))
        {
            return new RendezvousSettings();
        }

        using var stream = Storage.OpenFile(GamePaths.RendezvousFile, OpenFileMode.Read);
        return Read(XElement.Load(stream));
    }

    public static void Save(RendezvousSettings settings)
    {
        if (!Storage.DirectoryExists(GamePaths.Config))
        {
            Storage.CreateDirectory(GamePaths.Config);
        }

        using var stream = Storage.OpenFile(GamePaths.RendezvousFile, OpenFileMode.Create);
        Write(settings).Save(stream);
    }

    public static XElement Write(RendezvousSettings settings)
    {
        var normalized = Normalize(settings);
        return new XElement(
            "RendezvousServices",
            normalized.Services.Select(service => new XElement(
                    "Service",
                    new XAttribute("Id", service.Id),
                    service.RegistrationId is null ? null : new XAttribute("RegistrationId", service.RegistrationId),
                    new XAttribute("Name", service.Name),
                    new XAttribute("ControlUrl", service.ControlUrl),
                    new XAttribute("IsEnabled", service.IsEnabled),
                    new XAttribute("Order", service.Order)
                )
            )
        );
    }

    public static RendezvousSettings Read(XElement document)
    {
        if (document.Name != "RendezvousServices")
        {
            throw new FormatException("Invalid signaling services root element.");
        }

        return Normalize(
            new RendezvousSettings
            {
                Services = document.Elements("Service").Select(item => new InstalledRendezvousService
                    {
                        Id = Guid.Parse(Required(item, "Id")),
                        Name = Required(item, "Name"),
                        ControlUrl = Required(item, "ControlUrl"),
                        IsEnabled = bool.Parse(Required(item, "IsEnabled")),
                        Order = int.Parse(Required(item, "Order"), CultureInfo.InvariantCulture),
                        RegistrationId = item.Attribute("RegistrationId")?.Value
                    }
                ).ToArray()
            }
        );
    }

    private static string Required(XElement item, string name) =>
        item.Attribute(name)?.Value ?? throw new FormatException($"Missing signaling service attribute '{name}'.");

    private static RendezvousSettings Normalize(RendezvousSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var services = settings.Services.Select(service =>
            {
                if (service.Id == Guid.Empty ||
                    string.IsNullOrWhiteSpace(service.Name) ||
                    service.Name.Length > 100 ||
                    service.ControlUrl.Length > 2048)
                {
                    throw new ArgumentException("Invalid installed signaling service.");
                }

                return service with
                {
                    Name = service.Name.Trim(),
                    ControlUrl = RendezvousAddress.Normalize(new Uri(service.ControlUrl.Trim())).AbsoluteUri
                };
            }
        ).OrderBy(service => service.Order).ThenBy(service => service.Id).ToArray();
        if (services.Select(service => service.Id).Distinct().Count() != services.Length ||
            services.Select(service => service.ControlUrl).Distinct(StringComparer.Ordinal).Count() != services.Length)
        {
            throw new ArgumentException(
                "Installed signaling services must have unique identities and control addresses."
            );
        }

        return settings with { Services = services };
    }
}
