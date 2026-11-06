using System.Xml.Linq;

namespace Game.Admission;

public static class AdmissionOverridesSerializer
{
    public static XElement Serialize(ServerAdmissionOverrides overrides)
    {
        AdmissionOverridesPolicy.Validate(overrides);
        var node = new XElement("AdmissionOverrides");
        if (overrides.Mode is { } mode) { node.Add(new XAttribute("Mode", mode)); }

        foreach (var (name, value) in new[]
                 {
                     ("PasswordFile", overrides.PasswordFile), ("AuthenticationUrl", overrides.AuthenticationUrl),
                     ("AuthenticationKey", overrides.AuthenticationKey),
                     ("AuthenticationName", overrides.AuthenticationName)
                 })
        {
            if (value is not null) { node.Add(new XAttribute(name, value)); }
        }

        return node;
    }

    public static ServerAdmissionOverrides Deserialize(XElement? node)
    {
        if (node is null) { return new ServerAdmissionOverrides(); }

        if (node.Name != "AdmissionOverrides" ||
            node.HasElements ||
            node.Attributes().Any(attribute =>
                attribute.Name.LocalName is not "Mode" and not "PasswordFile" and not "AuthenticationUrl"
                    and not "AuthenticationKey" and not "AuthenticationName"
            ))
        {
            throw new AdmissionConfigurationException("Invalid stored admission overrides.");
        }

        ServerAdmissionMode? mode = null;
        if (node.Attribute("Mode") is { } value)
        {
            if (!Enum.TryParse<ServerAdmissionMode>(value.Value, true, out var parsed) || !Enum.IsDefined(parsed))
            {
                throw new AdmissionConfigurationException("Invalid stored admission mode.");
            }

            mode = parsed;
        }

        var result = new ServerAdmissionOverrides
        {
            Mode = mode, PasswordFile = (string?)node.Attribute("PasswordFile"),
            AuthenticationUrl = (string?)node.Attribute("AuthenticationUrl"),
            AuthenticationKey = (string?)node.Attribute("AuthenticationKey"),
            AuthenticationName = (string?)node.Attribute("AuthenticationName")
        };
        AdmissionOverridesPolicy.Validate(result);
        return result;
    }
}
