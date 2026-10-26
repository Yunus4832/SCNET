using System.Xml.Linq;

using Engine.Core.Security;

using SCNET.Authentication.Protocol.Http;

namespace Game.Authentication;

/// <summary>Dedicated private grant file. Credentials are bound to both the user key and a confirmed provider endpoint.</summary>
public sealed class AuthenticationGrantStore
{
    private readonly Lock _gate = new();

    private readonly string _path;

    private Dictionary<(string Subject, string Authority), StoredAuthenticationGrant> _grants = new();

    public AuthenticationGrantStore(string path)
    {
        _path = Path.GetFullPath(path);
        if (AuthenticationPrivateFiles.Read(_path) is not { } root) { return; }

        if (root.Name != "AuthenticationGrants" ||
            (string?)root.Attribute("Version") != "1" ||
            root.Elements().Count() > 1024)
        {
            throw new InvalidDataException("Invalid private authentication grant file.");
        }

        foreach (var item in root.Elements())
        {
            if (item.Name != "Grant") { throw new InvalidDataException("Invalid private authentication grant entry."); }

            var grant = new StoredAuthenticationGrant(
                Required(item, "Subject"),
                Required(item, "Authority"),
                Required(item, "ServiceUrl"),
                Required(item, "Secret"),
                (long?)item.Attribute("ExpiresAt") ?? 0
            );
            Validate(grant);
            if (!_grants.TryAdd((grant.Subject, grant.Authority), grant))
            {
                throw new InvalidDataException("Duplicate private authentication grant entry.");
            }
        }
    }

    public StoredAuthenticationGrant? Find(string subject, AuthenticationServiceEndpoint endpoint)
    {
        lock (_gate)
        {
            var grant = _grants.GetValueOrDefault((subject, endpoint.Identity.Address));
            return grant?.ServiceUrl == endpoint.BaseUri.AbsoluteUri ? grant : null;
        }
    }

    public void Set(string subject, AuthenticationServiceEndpoint endpoint, string secret, long expiresAt)
    {
        var grant = new StoredAuthenticationGrant(
            subject,
            endpoint.Identity.Address,
            endpoint.BaseUri.AbsoluteUri,
            secret,
            expiresAt
        );
        Validate(grant);
        lock (_gate)
        {
            var replacement = new Dictionary<(string Subject, string Authority), StoredAuthenticationGrant>(_grants)
            {
                [(subject, endpoint.Identity.Address)] = grant
            };
            if (replacement.Count > 1024)
            {
                throw new InvalidOperationException("Private grant store limit exceeded.");
            }

            Commit(replacement);
        }
    }

    public void Remove(string subject, string authority)
    {
        lock (_gate)
        {
            var replacement = new Dictionary<(string Subject, string Authority), StoredAuthenticationGrant>(_grants);
            if (!replacement.Remove((subject, authority))) { return; }

            Commit(replacement);
        }
    }

    public void RemoveAuthority(string authority)
    {
        lock (_gate)
        {
            var replacement = _grants.Where(entry => entry.Key.Authority != authority).ToDictionary();
            if (replacement.Count == _grants.Count) { return; }

            Commit(replacement);
        }
    }

    public void Reconcile(IReadOnlyList<InstalledAuthenticationService> installed)
    {
        var trusted = installed.ToDictionary(
            service => service.Endpoint.Identity.Address,
            service => service.Endpoint.BaseUri.AbsoluteUri
        );
        lock (_gate)
        {
            var replacement = _grants.Where(entry => trusted.TryGetValue(entry.Key.Authority, out var url) &&
                                                     url == entry.Value.ServiceUrl
            ).ToDictionary();
            if (replacement.Count != _grants.Count) { Commit(replacement); }
        }
    }

    private void Commit(Dictionary<(string Subject, string Authority), StoredAuthenticationGrant> replacement)
    {
        var root = new XElement("AuthenticationGrants", new XAttribute("Version", 1));
        foreach (var grant in replacement.Values.OrderBy(grant => grant.Subject, StringComparer.Ordinal)
                     .ThenBy(grant => grant.Authority, StringComparer.Ordinal))
        {
            root.Add(
                new XElement(
                    "Grant",
                    new XAttribute("Subject", grant.Subject),
                    new XAttribute("Authority", grant.Authority),
                    new XAttribute("ServiceUrl", grant.ServiceUrl),
                    new XAttribute("Secret", grant.Secret),
                    new XAttribute("ExpiresAt", grant.ExpiresAt)
                )
            );
        }

        AuthenticationPrivateFiles.Save(_path, root);
        _grants = replacement;
    }

    private static void Validate(StoredAuthenticationGrant grant)
    {
        if (IdentityAddress.Normalize(grant.Subject) != grant.Subject ||
            IdentityAddress.Normalize(grant.Authority) != grant.Authority ||
            grant.Secret.Length is < 1 or > 4096 ||
            grant.Secret.Any(char.IsControl) ||
            grant.ExpiresAt <= 0 ||
            grant.ExpiresAt > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            throw new InvalidDataException("Invalid private authentication grant.");
        }

        AuthenticationServiceEndpoint.ParseBaseUri(grant.ServiceUrl);
    }

    private static string Required(XElement element, string attribute)
    {
        return (string?)element.Attribute(attribute) ??
               throw new InvalidDataException("Missing private authentication grant attribute.");
    }
}
