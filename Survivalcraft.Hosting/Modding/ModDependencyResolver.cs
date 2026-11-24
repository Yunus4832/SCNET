namespace Game.Modding;

public static class ModDependencyResolver
{
    public static IReadOnlyList<ModDescriptor> Resolve(IEnumerable<ModDescriptor> descriptors)
    {
        return Resolve(descriptors, descriptor => descriptor.Manifest);
    }

    internal static IReadOnlyList<T> Resolve<T>(IEnumerable<T> contributions, Func<T, ModManifest> getManifest)
    {
        var mods = new Dictionary<ModId, (T Contribution, ModManifest Manifest)>();
        foreach (var contribution in contributions)
        {
            var manifest = getManifest(contribution);
            manifest.Validate();
            if (!mods.TryAdd(manifest.ModId, (contribution, manifest)))
            {
                throw new ModDependencyException($"Duplicate mod id {manifest.ModId}.");
            }
        }

        var result = new List<T>(mods.Count);
        var states = new Dictionary<ModId, VisitState>();
        var path = new Stack<ModId>();

        foreach (var id in mods.Keys.OrderBy(id => id.Value, StringComparer.Ordinal))
        {
            Visit(id);
        }

        return result;

        void Visit(ModId id)
        {
            if (states.TryGetValue(id, out var state))
            {
                if (state == VisitState.Visited)
                {
                    return;
                }

                if (state == VisitState.Visiting)
                {
                    var cycle = path.Reverse().SkipWhile(item => item != id).Append(id);
                    throw new ModDependencyException($"Circular mod dependency: {string.Join(" -> ", cycle)}.");
                }
            }

            states[id] = VisitState.Visiting;
            path.Push(id);
            var (contribution, manifest) = mods[id];
            foreach (var dependency in manifest.RequiredDependencies
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                var dependencyId = new ModId(dependency.Id);
                if (!mods.TryGetValue(dependencyId, out var dependencyMod))
                {
                    if (dependency.Optional)
                    {
                        continue;
                    }

                    throw new ModDependencyException($"Mod {id} requires missing mod {dependencyId}.");
                }

                if (dependency.MinimumVersion is not null &&
                    (!global::Content.Packaging.SemanticVersion.TryParse(
                         dependency.MinimumVersion,
                         out var minimumVersion
                     ) ||
                     dependencyMod.Manifest.ParsedVersion < minimumVersion))
                {
                    throw new ModDependencyException(
                        $"Mod {id} requires {dependencyId} version {dependency.MinimumVersion} or newer."
                    );
                }

                Visit(dependencyId);
            }

            path.Pop();
            states[id] = VisitState.Visited;
            result.Add(contribution);
        }
    }

    private enum VisitState
    {
        Visiting = 0,

        Visited = 1
    }
}

public sealed class ModDependencyException(string message) : Exception(message);
