namespace Game.Modding;

public static class ModDependencyResolver
{
    public static IReadOnlyList<ModDescriptor> Resolve(IEnumerable<ModDescriptor> descriptors)
    {
        var mods = new Dictionary<ModId, ModDescriptor>();
        foreach (var descriptor in descriptors)
        {
            descriptor.Manifest.Validate();
            if (!mods.TryAdd(descriptor.Manifest.ModId, descriptor))
            {
                throw new ModDependencyException($"Duplicate mod id {descriptor.Manifest.ModId}.");
            }
        }

        var result = new List<ModDescriptor>(mods.Count);
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
            var descriptor = mods[id];
            foreach (var dependency in descriptor.Manifest.RequiredDependencies
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
                    (!global::Content.Packaging.SemanticVersion.TryParse(dependency.MinimumVersion,
                         out var minimumVersion) ||
                     dependencyMod.Manifest.ParsedVersion < minimumVersion))
                {
                    throw new ModDependencyException(
                        $"Mod {id} requires {dependencyId} version {dependency.MinimumVersion} or newer.");
                }

                Visit(dependencyId);
            }

            path.Pop();
            states[id] = VisitState.Visited;
            result.Add(descriptor);
        }
    }

    private enum VisitState
    {
        Visiting,
        Visited
    }
}

public sealed class ModDependencyException(string message) : Exception(message);
