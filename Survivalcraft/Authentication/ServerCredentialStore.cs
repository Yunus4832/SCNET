using System.Xml.Linq;

using Engine.Core.Security;

namespace Game.Authentication;

/// <summary>Instance-private shared joining passwords, scoped to the server's proven public identity.</summary>
public sealed class ServerCredentialStore
{
    private readonly string _path;

    private Dictionary<string, string> _passwords = new(StringComparer.Ordinal);

    public ServerCredentialStore(string path)
    {
        _path = Path.GetFullPath(path);
        if (AuthenticationPrivateFiles.Read(_path) is not { } root) { return; }

        if (root.Name != "ServerCredentials" ||
            (string?)root.Attribute("Version") != "1" ||
            root.Elements().Count() > 1024)
        {
            throw new InvalidDataException("Invalid server credential file.");
        }

        foreach (var entry in root.Elements())
        {
            var server = (string?)entry.Attribute("Server") ?? string.Empty;
            var password = (string?)entry.Attribute("Password") ?? string.Empty;
            Validate(server, password);
            if (entry.Name != "Credential" || !_passwords.TryAdd(server, password))
            {
                throw new InvalidDataException("Invalid server credential entry.");
            }
        }
    }

    public int Count => _passwords.Count;

    public string? Find(string server) => _passwords.GetValueOrDefault(server);

    public void Set(string server, string password)
    {
        Validate(server, password);
        var replacement = new Dictionary<string, string>(_passwords, StringComparer.Ordinal) { [server] = password };
        if (replacement.Count > 1024) { throw new InvalidOperationException("Server credential limit exceeded."); }

        Commit(replacement);
    }

    public void Remove(string server)
    {
        var replacement = new Dictionary<string, string>(_passwords, StringComparer.Ordinal);
        if (replacement.Remove(server)) { Commit(replacement); }
    }

    public void Clear()
    {
        if (_passwords.Count != 0) { Commit(new Dictionary<string, string>(StringComparer.Ordinal)); }
    }

    private void Commit(Dictionary<string, string> replacement)
    {
        var root = new XElement("ServerCredentials", new XAttribute("Version", 1));
        foreach (var entry in replacement.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            root.Add(
                new XElement("Credential", new XAttribute("Server", entry.Key), new XAttribute("Password", entry.Value))
            );
        }

        AuthenticationPrivateFiles.Save(_path, root);
        _passwords = replacement;
    }

    private static void Validate(string server, string password)
    {
        if (IdentityAddress.Normalize(server) != server ||
            password.Length is < 1 or > 1024 ||
            password.Any(char.IsControl))
        {
            throw new InvalidDataException("Invalid server credential.");
        }
    }
}
