using System.Xml.Linq;

using Engine.Network;

using Game.Admission;
using Game.Servers;

namespace Game.Startup;

public static class SessionInfoSerializer
{
    public static bool IsValidSessionId(string? sessionId)
    {
        return !string.IsNullOrWhiteSpace(sessionId) && Guid.TryParse(sessionId, out _);
    }

    public static XElement? FindSessionElement(XElement root, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        if (string.Equals(root.Name.LocalName, "SessionInfo", StringComparison.Ordinal))
        {
            var storedSessionId = NormalizeSessionId(root.Attribute(nameof(SessionInfo.SessionId))?.Value);
            return string.Equals(storedSessionId, sessionId, StringComparison.Ordinal) ? root : null;
        }

        return root.Elements("SessionInfo").FirstOrDefault(element =>
            string.Equals(
                NormalizeSessionId(element.Attribute(nameof(SessionInfo.SessionId))?.Value),
                sessionId,
                StringComparison.Ordinal
            )
        );
    }

    public static XElement? FindSessionElementByName(XElement root, string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            return null;
        }

        if (string.Equals(root.Name.LocalName, "SessionInfo", StringComparison.Ordinal))
        {
            var storedSessionName = NormalizeSessionName(root.Attribute(nameof(SessionInfo.Name))?.Value);
            return string.Equals(storedSessionName, sessionName, StringComparison.Ordinal) ? root : null;
        }

        return root.Elements("SessionInfo").FirstOrDefault(element =>
            string.Equals(
                NormalizeSessionName(element.Attribute(nameof(SessionInfo.Name))?.Value),
                sessionName,
                StringComparison.Ordinal
            )
        );
    }

    public static XElement Serialize(SessionInfo sessionInfo)
    {
        var element = new XElement(
            "SessionInfo",
            new XAttribute(nameof(SessionInfo.SessionId), sessionInfo.SessionId),
            new XAttribute(nameof(SessionInfo.Name), sessionInfo.Name),
            new XAttribute(nameof(SessionInfo.Target), sessionInfo.Target),
            new XAttribute(nameof(SessionInfo.World), sessionInfo.World),
            new XAttribute(nameof(SessionInfo.Seed), sessionInfo.Seed),
            new XAttribute(nameof(SessionInfo.ServerPort), sessionInfo.ServerPort),
            new XAttribute(nameof(SessionInfo.BroadcastPort), sessionInfo.BroadcastPort)
        );
        if (sessionInfo.CreateWorldIfMissing)
        {
            element.SetAttributeValue(nameof(SessionInfo.CreateWorldIfMissing), true);
        }

        if (sessionInfo.ForceWorldRunServer)
        {
            element.SetAttributeValue(nameof(SessionInfo.ForceWorldRunServer), true);
        }

        if (sessionInfo.ServerIdentityAddress is not null)
        {
            element.SetAttributeValue(nameof(SessionInfo.ServerIdentityAddress), sessionInfo.ServerIdentityAddress);
        }

        if (!sessionInfo.AdmissionOverrides.IsEmpty)
        {
            element.Add(AdmissionOverridesSerializer.Serialize(sessionInfo.AdmissionOverrides));
        }

        if (sessionInfo.RemoteTarget is not null)
        {
            element.Add(CreateRemoteTargetElement(sessionInfo.RemoteTarget));
        }

        if (sessionInfo.GameMode is { } gameMode)
        {
            element.Add(new XAttribute(nameof(SessionInfo.GameMode), gameMode));
        }

        if (sessionInfo.TerrainGenerationMode is { } terrain)
        {
            element.Add(new XAttribute(nameof(SessionInfo.TerrainGenerationMode), terrain));
        }

        if (sessionInfo.TerrainLevel is { } level)
        {
            element.Add(new XAttribute(nameof(SessionInfo.TerrainLevel), level));
        }

        var p2p = new XElement(nameof(SessionInfo.P2POverrides));
        if (sessionInfo.P2POverrides.Enabled is { } enabled)
        {
            p2p.Add(new XAttribute(nameof(P2PRoomOverrides.Enabled), enabled));
        }

        if (sessionInfo.P2POverrides.ControlUrl is { } controlUrl)
        {
            p2p.Add(new XAttribute(nameof(P2PRoomOverrides.ControlUrl), controlUrl));
        }

        if (sessionInfo.P2POverrides.Name is { } name)
        {
            p2p.Add(new XAttribute(nameof(P2PRoomOverrides.Name), name));
        }

        if (sessionInfo.P2POverrides.Description is { } description)
        {
            p2p.Add(new XAttribute(nameof(P2PRoomOverrides.Description), description));
        }

        if (sessionInfo.P2POverrides.IsPublic is { } isPublic)
        {
            p2p.Add(new XAttribute(nameof(P2PRoomOverrides.IsPublic), isPublic));
        }

        if (p2p.HasAttributes)
        {
            element.Add(p2p);
        }

        if (sessionInfo.HttpCommandEnabled is { } httpCommandEnabled)
        {
            element.Add(new XAttribute(nameof(SessionInfo.HttpCommandEnabled), httpCommandEnabled));
        }

        if (sessionInfo.HttpCommandPort is { } httpCommandPort)
        {
            element.Add(new XAttribute(nameof(SessionInfo.HttpCommandPort), httpCommandPort));
        }

        if (!string.IsNullOrWhiteSpace(sessionInfo.AutoJoinCharacterName))
        {
            element.Add(
                new XAttribute(
                    nameof(SessionInfo.AutoJoinCharacterName),
                    sessionInfo.AutoJoinCharacterName
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(sessionInfo.HttpCommandAccessToken))
        {
            element.Add(
                new XAttribute(
                    nameof(SessionInfo.HttpCommandAccessToken),
                    sessionInfo.HttpCommandAccessToken
                )
            );
        }

        return element;
    }

    public static void Populate(SessionInfo sessionInfo, XElement element)
    {
        sessionInfo.ServerIdentityAddress = element.Attribute(nameof(SessionInfo.ServerIdentityAddress))?.Value;
        if (sessionInfo.ServerIdentityAddress is not null)
        {
            try
            {
                sessionInfo.ServerIdentityAddress =
                    IdentityAddress.Normalize(sessionInfo.ServerIdentityAddress);
            }
            catch (FormatException error)
            {
                throw new InvalidDataException(
                    "The startup session contains an invalid server identity address.",
                    error
                );
            }
        }

        sessionInfo.AdmissionOverrides =
            AdmissionOverridesSerializer.Deserialize(element.Element("AdmissionOverrides"));
        sessionInfo.SessionId = NormalizeSessionId(element.Attribute(nameof(SessionInfo.SessionId))?.Value);
        sessionInfo.Name = NormalizeSessionName(element.Attribute(nameof(SessionInfo.Name))?.Value);
        sessionInfo.Target = ParseTarget(element.Attribute(nameof(SessionInfo.Target))?.Value);
        sessionInfo.World = NormalizeWorld(element.Attribute(nameof(SessionInfo.World))?.Value);
        sessionInfo.CreateWorldIfMissing = (bool?)element.Attribute(nameof(SessionInfo.CreateWorldIfMissing)) ?? false;
        sessionInfo.ForceWorldRunServer = (bool?)element.Attribute(nameof(SessionInfo.ForceWorldRunServer)) ?? false;
        sessionInfo.Seed = element.Attribute(nameof(SessionInfo.Seed))?.Value ?? string.Empty;
        sessionInfo.GameMode = ParseGameMode(element.Attribute(nameof(SessionInfo.GameMode))?.Value);
        sessionInfo.TerrainGenerationMode = element.Attribute(nameof(SessionInfo.TerrainGenerationMode)) is { } terrain
            ? Enum.Parse<TerrainGenerationMode>(terrain.Value, true)
            : null;
        sessionInfo.TerrainLevel = element.Attribute(nameof(SessionInfo.TerrainLevel)) is { } level
            ? int.Parse(level.Value)
            : null;
        var p2p = element.Element(nameof(SessionInfo.P2POverrides));
        sessionInfo.P2POverrides = new P2PRoomOverrides
        {
            Enabled = (bool?)p2p?.Attribute(nameof(P2PRoomOverrides.Enabled)),
            ControlUrl = (string?)p2p?.Attribute(nameof(P2PRoomOverrides.ControlUrl)),
            Name = (string?)p2p?.Attribute(nameof(P2PRoomOverrides.Name)),
            Description = (string?)p2p?.Attribute(nameof(P2PRoomOverrides.Description)),
            IsPublic = (bool?)p2p?.Attribute(nameof(P2PRoomOverrides.IsPublic))
        };
        sessionInfo.RemoteTarget = element.Element("RemoteTarget") is { } remote ? ReadRemoteTarget(remote) : null;
        sessionInfo.ServerPort = ParseServerPort(element.Attribute(nameof(SessionInfo.ServerPort))?.Value);
        sessionInfo.BroadcastPort = ParseServerPort(element.Attribute(nameof(SessionInfo.BroadcastPort))?.Value);
        sessionInfo.HttpCommandEnabled = ParseOptionalBoolean(
            element.Attribute(nameof(SessionInfo.HttpCommandEnabled))?.Value
        );
        sessionInfo.HttpCommandPort = ParseOptionalPort(element.Attribute(nameof(SessionInfo.HttpCommandPort))?.Value);
        sessionInfo.AutoJoinCharacterName = NormalizeOptionalText(
            element.Attribute(nameof(SessionInfo.AutoJoinCharacterName))?.Value
        );
        sessionInfo.HttpCommandAccessToken =
            element.Attribute(nameof(SessionInfo.HttpCommandAccessToken))?.Value;
    }

    public static SessionInfo CreateDefault(string sessionId)
    {
        return new SessionInfo
        {
            SessionId = sessionId,
            Name = string.Empty,
            Target = SessionTarget.MainMenu,
            World = "World",
            Seed = string.Empty,
            RemoteTarget = null
        };
    }

    public static void Normalize(SessionInfo sessionInfo)
    {
        AdmissionOverridesPolicy.Validate(sessionInfo.AdmissionOverrides);
        if (sessionInfo.ServerIdentityAddress is not null)
        {
            sessionInfo.ServerIdentityAddress =
                IdentityAddress.Normalize(sessionInfo.ServerIdentityAddress);
        }

        if (sessionInfo.Target != SessionTarget.RemoteServer)
        {
            sessionInfo.RemoteTarget = null;
        }

        if (sessionInfo.TerrainGenerationMode is { } terrain &&
            (!Enum.IsDefined(terrain) || TerrainGenerationModes.IsLegacy(terrain)))
        {
            throw new ArgumentException("Session terrain must use a current generation mode.");
        }

        if (sessionInfo.TerrainLevel is < 2 or > 252)
        {
            throw new ArgumentException("Session terrain level must be between 2 and 252.");
        }

        sessionInfo.SessionId = !IsValidSessionId(sessionInfo.SessionId)
            ? Guid.NewGuid().ToString("N")
            : NormalizeSessionId(sessionInfo.SessionId);
        sessionInfo.Name = NormalizeSessionName(sessionInfo.Name);
        sessionInfo.World = NormalizeWorld(sessionInfo.World);
        sessionInfo.AutoJoinCharacterName = NormalizeOptionalText(sessionInfo.AutoJoinCharacterName);
        if (sessionInfo.ServerPort < 0)
        {
            sessionInfo.ServerPort = 0;
        }
    }

    private static SessionTarget ParseTarget(string? value)
    {
        return Enum.TryParse(value, true, out SessionTarget target)
            ? target
            : SessionTarget.MainMenu;
    }

    private static int ParseServerPort(string? value)
    {
        return int.TryParse(value, out var port) ? port : 0;
    }

    private static int? ParseOptionalPort(string? value)
    {
        return int.TryParse(value, out var port) ? port : null;
    }

    private static bool? ParseOptionalBoolean(string? value)
    {
        return bool.TryParse(value, out var result) ? result : null;
    }

    private static GameMode? ParseGameMode(string? value)
    {
        return Enum.TryParse<GameMode>(value, true, out var gameMode) &&
               Enum.IsDefined(gameMode)
            ? gameMode
            : null;
    }

    public static string NormalizeSessionId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public static string NormalizeWorld(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "World" : value;
    }

    public static string NormalizeSessionName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static XElement CreateRemoteTargetElement(NetworkTarget target) => target switch
    {
        AddressNetworkTarget address => new XElement(
            "RemoteTarget",
            new XAttribute("Kind", "Address"),
            new XAttribute("Host", address.Host),
            new XAttribute("Port", address.Port)
        ),
        InvitationCodeNetworkTarget code => new XElement(
            "RemoteTarget",
            new XAttribute("Kind", "InvitationCode"),
            new XAttribute("ControlAddress", code.ControlAddress),
            new XAttribute("Code", code.JoinCode),
            new XAttribute("RelayPolicy", code.RelayPolicy)
        ),
        InvitationSessionNetworkTarget session => new XElement(
            "RemoteTarget",
            new XAttribute("Kind", "InvitationSession"),
            new XAttribute("ControlAddress", session.ControlAddress),
            new XAttribute("SessionId", session.SessionId),
            new XAttribute("RelayPolicy", session.RelayPolicy)
        ),
        _ => throw new InvalidOperationException("Unknown remote target.")
    };

    private static NetworkTarget ReadRemoteTarget(XElement element)
    {
        string Required(string name) => element.Attribute(name)?.Value ??
                                        throw new FormatException($"Missing remote target {name}.");

        return Required("Kind") switch
        {
            "Address" => new AddressNetworkTarget(Required("Host"), int.Parse(Required("Port"))),
            "InvitationCode" => new InvitationCodeNetworkTarget(
                new Uri(Required("ControlAddress")),
                Required("Code"),
                Enum.Parse<RelayPolicy>(Required("RelayPolicy"))
            ),
            "InvitationSession" => new InvitationSessionNetworkTarget(
                new Uri(Required("ControlAddress")),
                Guid.Parse(Required("SessionId")),
                Enum.Parse<RelayPolicy>(Required("RelayPolicy"))
            ),
            _ => throw new FormatException("Unknown remote target kind.")
        };
    }

    public static SessionInfo Clone(SessionInfo source)
    {
        return new SessionInfo
        {
            SessionId = NormalizeSessionId(source.SessionId),
            Name = source.Name,
            Target = source.Target,
            World = source.World,
            CreateWorldIfMissing = source.CreateWorldIfMissing,
            ForceWorldRunServer = source.ForceWorldRunServer,
            Seed = source.Seed,
            GameMode = source.GameMode,
            TerrainGenerationMode = source.TerrainGenerationMode,
            TerrainLevel = source.TerrainLevel,
            RemoteTarget = source.RemoteTarget,
            P2POverrides = source.P2POverrides,
            AdmissionOverrides = source.AdmissionOverrides,
            ServerIdentityAddress = source.ServerIdentityAddress,
            ServerPort = source.ServerPort,
            BroadcastPort = source.BroadcastPort,
            AutoJoinCharacterName = source.AutoJoinCharacterName,
            HttpCommandEnabled = source.HttpCommandEnabled,
            HttpCommandPort = source.HttpCommandPort,
            HttpCommandAccessToken = source.HttpCommandAccessToken
        };
    }
}
