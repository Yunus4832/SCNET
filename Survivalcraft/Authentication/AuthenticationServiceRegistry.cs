using System.Xml.Linq;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;

namespace Game.Authentication;

/// <summary>Instance-installed trust configuration, independent of Settings, grants, and world roles.</summary>
public sealed class AuthenticationServiceRegistry
{
    private readonly Lock _gate = new();

    private readonly string _path;

    private Dictionary<string, InstalledAuthenticationService> _services = new(StringComparer.Ordinal);

    public AuthenticationServiceRegistry(string path)
    {
        _path = Path.GetFullPath(path);
        if (AuthenticationPrivateFiles.Read(_path) is not { } root) { return; }

        if (root.Name != "AuthenticationServices" ||
            (string?)root.Attribute("Version") != "1" ||
            root.Elements().Count() > 128)
        {
            throw new InvalidDataException("Invalid authentication service registry.");
        }

        foreach (var item in root.Elements())
        {
            if (item.Name != "Service") { throw new InvalidDataException("Invalid authentication service entry."); }

            var endpoint = new AuthenticationServiceEndpoint(
                new AuthenticationServiceDescription(
                    AuthenticationProtocol.Version,
                    Required(item, "Name"),
                    Required(item, "Url"),
                    Required(item, "PublicKey")
                )
            );
            var enabled = bool.Parse(Required(item, "Enabled"));
            var source = (string?)item.Attribute("Source") ?? string.Empty;
            if (source.Length > 2048 ||
                source.Any(char.IsControl) ||
                !_services.TryAdd(
                    endpoint.Identity.Address,
                    new InstalledAuthenticationService(endpoint, enabled, source)
                ))
            {
                throw new InvalidDataException("Invalid or duplicate authentication service entry.");
            }
        }
    }

    public IReadOnlyList<InstalledAuthenticationService> Services
    {
        get
        {
            lock (_gate)
            {
                return _services.Values.OrderBy(
                    service => service.Endpoint.Description.DisplayName,
                    StringComparer.Ordinal
                ).ToArray();
            }
        }
    }

    public InstalledAuthenticationService? Find(string authority)
    {
        lock (_gate) { return _services.GetValueOrDefault(authority); }
    }

    public void AddConfirmed(AuthenticationServiceEndpoint endpoint, string source = "")
    {
        lock (_gate)
        {
            if (_services.ContainsKey(endpoint.Identity.Address))
            {
                throw new InvalidOperationException(
                    "Service is already installed; update its confirmed settings explicitly."
                );
            }

            if (_services.Count >= 128 || source.Length > 2048 || source.Any(char.IsControl))
            {
                throw new InvalidOperationException("Authentication service registry limit exceeded.");
            }

            var replacement = new Dictionary<string, InstalledAuthenticationService>(_services, StringComparer.Ordinal)
            {
                [endpoint.Identity.Address] = new(endpoint, true, source)
            };
            Commit(replacement);
        }
    }

    public void SetEnabled(string authority, bool enabled)
    {
        lock (_gate)
        {
            var current = Require(authority);
            var replacement = new Dictionary<string, InstalledAuthenticationService>(_services, StringComparer.Ordinal)
            {
                [authority] = current with { Enabled = enabled }
            };
            Commit(replacement);
        }
    }

    public void UpdateConfirmedEndpoint(string authority, AuthenticationServiceEndpoint endpoint)
    {
        lock (_gate)
        {
            var current = Require(authority);
            if (endpoint.Identity.Address != authority)
            {
                throw new InvalidOperationException(
                    "A different public key is a different service, not an endpoint update."
                );
            }

            var replacement = new Dictionary<string, InstalledAuthenticationService>(_services, StringComparer.Ordinal)
            {
                [authority] = current with { Endpoint = endpoint }
            };
            Commit(replacement);
        }
    }

    public void Remove(string authority)
    {
        lock (_gate)
        {
            var replacement = new Dictionary<string, InstalledAuthenticationService>(_services, StringComparer.Ordinal);
            if (!replacement.Remove(authority)) { return; }

            Commit(replacement);
        }
    }

    private InstalledAuthenticationService Require(string authority)
    {
        return _services.GetValueOrDefault(authority) ??
               throw new InvalidOperationException("Authentication service is not installed.");
    }

    private void Commit(Dictionary<string, InstalledAuthenticationService> replacement)
    {
        var root = new XElement("AuthenticationServices", new XAttribute("Version", 1));
        foreach (var service in replacement.Values.OrderBy(
                     service => service.Endpoint.Identity.Address,
                     StringComparer.Ordinal
                 ))
        {
            root.Add(
                new XElement(
                    "Service",
                    new XAttribute("Name", service.Endpoint.Description.DisplayName),
                    new XAttribute("Url", service.Endpoint.BaseUri.AbsoluteUri),
                    new XAttribute("PublicKey", service.Endpoint.Description.PublicKey),
                    new XAttribute("Enabled", service.Enabled),
                    new XAttribute("Source", service.Source)
                )
            );
        }

        AuthenticationPrivateFiles.Save(_path, root);
        _services = replacement;
    }

    private static string Required(XElement element, string attribute)
    {
        return (string?)element.Attribute(attribute) ??
               throw new InvalidDataException("Missing authentication service attribute.");
    }
}
