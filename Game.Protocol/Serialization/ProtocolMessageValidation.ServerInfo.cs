using Game.Protocol.Connection;

namespace Game.Protocol.Serialization;

internal static partial class ProtocolMessageValidation
{
    private static void ValidateProfile(RequiredModProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id) ||
            profile.Id.Length > ProtocolLimits.MaximumModProfileIdLength ||
            profile.Packages.Count > ProtocolLimits.MaximumRequiredMods)
        {
            throw new InvalidDataException("Invalid required mod profile.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var package in profile.Packages)
        {
            if (string.IsNullOrWhiteSpace(package.ModId) ||
                package.ModId.Length > ProtocolLimits.MaximumModIdLength ||
                !ids.Add(package.ModId) ||
                string.IsNullOrWhiteSpace(package.Version) ||
                package.Version.Length > ProtocolLimits.MaximumModVersionLength ||
                package.PackageHash.Length != 64 ||
                package.PackageHash.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            {
                throw new InvalidDataException("Invalid required mod package.");
            }
        }
    }

    private static IReadOnlyList<ContentRepositoryDescriptor> ValidateRepositories(
        IEnumerable<ContentRepositoryDescriptor> repositories
    )
    {
        var values = repositories.ToArray();
        if (values.Length > ProtocolLimits.MaximumTemporaryRepositories)
        {
            throw new InvalidDataException("Too many temporary repositories.");
        }

        var ids = new HashSet<Guid>();
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (value.Id == Guid.Empty ||
                !ids.Add(value.Id) ||
                value.BaseUrl.Length > ProtocolLimits.MaximumRepositoryUrlLength ||
                value.Priority is < ProtocolLimits.MinimumRepositoryPriority
                    or > ProtocolLimits.MaximumRepositoryPriority ||
                !Uri.TryCreate(value.BaseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                uri.UserInfo.Length != 0 ||
                uri.Query.Length != 0 ||
                uri.Fragment.Length != 0 ||
                !addresses.Add(uri.AbsoluteUri.TrimEnd('/')))
            {
                throw new InvalidDataException("Invalid temporary repository.");
            }
        }

        return values;
    }
}
