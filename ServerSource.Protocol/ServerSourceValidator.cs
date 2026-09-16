using System.Globalization;

namespace ServerSource.Protocol;

public static class ServerSourceValidator
{
    public static ServerSourceValidationResult Validate(ServerSourcePage? page)
    {
        var issues = new List<ServerSourceValidationIssue>();
        if (page is null)
        {
            Add(issues, ServerSourceValidationCode.InvalidSource, "$", "Response body is null.");
            return new ServerSourceValidationResult(issues);
        }

        if (page.ProtocolVersion != ServerSourceProtocol.CurrentVersion)
        {
            Add(issues, ServerSourceValidationCode.UnsupportedProtocolVersion, "protocolVersion",
                $"Protocol version {page.ProtocolVersion} is not supported.");
        }

        ValidateSource(page.Source, issues);
        ValidateServers(page.Servers, issues);
        ValidateCursor(page.NextCursor, issues);
        return new ServerSourceValidationResult(issues);
    }

    public static bool IsValidServerAddress(string? address)
    {
        if (!IsTextValid(address, ServerSourceProtocol.MaximumAddressLength) ||
            !string.Equals(address, address!.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (!Uri.TryCreate($"tcp://{address}", UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host) || uri.Port is < 1 or > 65535)
        {
            return false;
        }

        return string.IsNullOrEmpty(uri.UserInfo) &&
               string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal) &&
               string.IsNullOrEmpty(uri.Query) &&
               string.IsNullOrEmpty(uri.Fragment);
    }

    private static void ValidateSource(ServerSourceDescriptor? source,
        ICollection<ServerSourceValidationIssue> issues)
    {
        if (source is null)
        {
            Add(issues, ServerSourceValidationCode.InvalidSource, "source", "Source descriptor is required.");
            return;
        }

        if (!IsIdentifierValid(source.Id, ServerSourceProtocol.MaximumSourceIdLength))
        {
            Add(issues, ServerSourceValidationCode.InvalidSourceId, "source.id", "Source id is invalid.");
        }

        if (!IsTextValid(source.Name, ServerSourceProtocol.MaximumNameLength))
        {
            Add(issues, ServerSourceValidationCode.InvalidSourceName, "source.name", "Source name is invalid.");
        }
    }

    private static void ValidateServers(IReadOnlyList<ServerSourceEntry>? servers,
        ICollection<ServerSourceValidationIssue> issues)
    {
        if (servers is null)
        {
            Add(issues, ServerSourceValidationCode.InvalidServerCollection, "servers",
                "Server collection is required.");
            return;
        }

        if (servers.Count > ServerSourceProtocol.MaximumPageSize)
        {
            Add(issues, ServerSourceValidationCode.TooManyEntries, "servers",
                $"A page cannot contain more than {ServerSourceProtocol.MaximumPageSize} entries.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < servers.Count; index++)
        {
            var entry = servers[index];
            var path = $"servers[{index.ToString(CultureInfo.InvariantCulture)}]";
            if (entry is null)
            {
                Add(issues, ServerSourceValidationCode.InvalidEntry, path, "Server entry is required.");
                continue;
            }

            if (!IsIdentifierValid(entry.Id, ServerSourceProtocol.MaximumEntryIdLength))
            {
                Add(issues, ServerSourceValidationCode.InvalidEntryId, $"{path}.id", "Server entry id is invalid.");
            }
            else if (!ids.Add(entry.Id))
            {
                Add(issues, ServerSourceValidationCode.DuplicateEntryId, $"{path}.id",
                    "Server entry id is duplicated in this page.");
            }

            if (!IsTextValid(entry.Name, ServerSourceProtocol.MaximumNameLength))
            {
                Add(issues, ServerSourceValidationCode.InvalidEntryName, $"{path}.name",
                    "Server entry name is invalid.");
            }

            if (!IsValidServerAddress(entry.Address))
            {
                Add(issues, ServerSourceValidationCode.InvalidServerAddress, $"{path}.address",
                    "Server address must contain a host and an explicit port.");
            }

            if (entry.Description is { Length: > ServerSourceProtocol.MaximumDescriptionLength } ||
                ContainsControlCharacters(entry.Description))
            {
                Add(issues, ServerSourceValidationCode.DescriptionTooLong, $"{path}.description",
                    "Server description is invalid.");
            }

            ValidateTags(entry.Tags, path, issues);
        }
    }

    private static void ValidateTags(IReadOnlyList<string>? tags, string path,
        ICollection<ServerSourceValidationIssue> issues)
    {
        if (tags is null)
        {
            Add(issues, ServerSourceValidationCode.InvalidTag, $"{path}.tags", "Tag collection is required.");
            return;
        }

        if (tags.Count > ServerSourceProtocol.MaximumTagCount)
        {
            Add(issues, ServerSourceValidationCode.TooManyTags, $"{path}.tags",
                $"A server cannot have more than {ServerSourceProtocol.MaximumTagCount} tags.");
        }

        var uniqueTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < tags.Count; index++)
        {
            var tag = tags[index];
            if (!IsTextValid(tag, ServerSourceProtocol.MaximumTagLength) ||
                !string.Equals(tag, tag.Trim(), StringComparison.Ordinal) ||
                !uniqueTags.Add(tag))
            {
                Add(issues, ServerSourceValidationCode.InvalidTag,
                    $"{path}.tags[{index.ToString(CultureInfo.InvariantCulture)}]", "Tag is invalid or duplicated.");
            }
        }
    }

    private static void ValidateCursor(string? cursor, ICollection<ServerSourceValidationIssue> issues)
    {
        if (cursor is null)
        {
            return;
        }

        if (cursor.Length == 0 || cursor.Length > ServerSourceProtocol.MaximumCursorLength ||
            ContainsControlCharacters(cursor))
        {
            Add(issues, ServerSourceValidationCode.InvalidCursor, "nextCursor", "Next cursor is invalid.");
        }
    }

    private static bool IsIdentifierValid(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maximumLength ||
            !char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        return value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
    }

    private static bool IsTextValid(string? value, int maximumLength)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength &&
               string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);
    }

    private static bool ContainsControlCharacters(string? value)
    {
        return value?.Any(character => char.IsControl(character) && character is not '\n' and not '\r' and not '\t')
               == true;
    }

    private static void Add(ICollection<ServerSourceValidationIssue> issues, ServerSourceValidationCode code,
        string path, string message)
    {
        issues.Add(new ServerSourceValidationIssue(code, path, message));
    }
}
