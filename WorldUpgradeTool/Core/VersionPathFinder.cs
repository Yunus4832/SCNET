using Game.VersionConverts;

namespace WorldUpgradeTool.Core;

internal sealed class VersionPathFinder
{
    private readonly VersionConverter[] _converters;

    public VersionPathFinder()
    {
        _converters = typeof(WorldUpgradeManager).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(VersionConverter).IsAssignableFrom(t))
            .Select(t => (VersionConverter)Activator.CreateInstance(t)!)
            .OrderBy(c => c.SourceVersion, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<VersionConverter>? FindPath(string sourceVersion, string targetVersion) =>
        FindPath(sourceVersion, targetVersion, 0);

    private List<VersionConverter>? FindPath(string sourceVersion, string targetVersion, int depth)
    {
        if (depth > 100)
        {
            throw new InvalidOperationException(
                "Too deep recursion when searching for version converters. Check for possible loops in transforms.");
        }

        if (sourceVersion == targetVersion)
        {
            return [];
        }

        List<VersionConverter>? result = null;
        var bestLength = int.MaxValue;
        foreach (var converter in _converters)
        {
            if (converter.SourceVersion != sourceVersion)
            {
                continue;
            }

            var path = FindPath(converter.TargetVersion, targetVersion, depth + 1);
            if (path == null || path.Count >= bestLength)
            {
                continue;
            }

            bestLength = path.Count;
            path.Insert(0, converter);
            result = path;
        }

        return result;
    }
}
