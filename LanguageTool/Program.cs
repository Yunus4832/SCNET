using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

var app = new LanguageToolApp();
return app.Run(args);

internal sealed class LanguageToolApp
{
    private static readonly string[] DefaultCultures = ["zh-CN", "en-US", "pt-PT", "ru-RU"];

    private readonly JsonSerializerOptions _readOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var root = FindRepositoryRoot(Environment.CurrentDirectory);
            var command = args[0];
            var commandArgs = args.Skip(1).ToArray();
            return command switch
            {
                "check" => Check(root, commandArgs),
                "list" => List(root, commandArgs),
                "children" => Children(root, commandArgs),
                "show" => Show(root, commandArgs),
                "search" => Search(root, commandArgs),
                "rules" => Rules(commandArgs),
                "get" => Get(root, commandArgs),
                "set" => Set(root, commandArgs),
                "remove" or "delete" => Remove(root, commandArgs),
                "rename" => Rename(root, commandArgs),
                _ => Fail($"Unknown command '{command}'. Run with --help.")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    private int Check(DirectoryInfo root, string[] args)
    {
        EnsureNoUnexpectedArgs(args);
        var documents = LoadLanguages(root);
        var maps = documents.ToDictionary(pair => pair.Key, pair => CollectPaths(pair.Value.Root));
        var allPaths = maps.Values.SelectMany(map => map.Keys).Distinct().Order(StringComparer.Ordinal).ToArray();
        var hasDifferences = false;

        foreach (var culture in DefaultCultures)
        {
            var missing = allPaths.Where(path => !maps[culture].ContainsKey(path)).ToArray();
            if (missing.Length == 0)
            {
                continue;
            }

            hasDifferences = true;
            Console.WriteLine();
            Console.WriteLine($"{culture}: missing {missing.Length} path(s)");
            foreach (var path in missing)
            {
                var expectedKinds = maps.Values
                    .Where(map => map.TryGetValue(path, out _))
                    .Select(map => map[path])
                    .Distinct()
                    .Order(StringComparer.Ordinal);
                Console.WriteLine($"  - {path} ({string.Join('|', expectedKinds)})");
            }
        }

        var typeMismatches = allPaths
            .Select(path => new
            {
                Path = path,
                ByKind = maps
                    .Where(pair => pair.Value.TryGetValue(path, out _))
                    .GroupBy(pair => pair.Value[path], pair => pair.Key)
                    .ToDictionary(group => group.Key, group => group.Order(StringComparer.Ordinal).ToArray())
            })
            .Where(item => item.ByKind.Count > 1)
            .ToArray();

        if (typeMismatches.Length > 0)
        {
            hasDifferences = true;
            Console.WriteLine();
            Console.WriteLine($"type mismatches: {typeMismatches.Length} path(s)");
            foreach (var item in typeMismatches)
            {
                Console.WriteLine($"  - {item.Path}");
                foreach (var kind in item.ByKind.Keys.Order(StringComparer.Ordinal))
                {
                    Console.WriteLine($"      {kind}: {string.Join(", ", item.ByKind[kind])}");
                }
            }
        }

        if (!hasDifferences)
        {
            Console.WriteLine("Language key sets are consistent.");
        }

        return hasDifferences ? 1 : 0;
    }

    private int List(DirectoryInfo root, string[] args)
    {
        var options = ParseOptions(args, allowPositionals: false);
        var culture = options.GetValueOrDefault("culture") ?? "zh-CN";
        var prefix = options.GetValueOrDefault("prefix") ?? string.Empty;
        var documents = LoadLanguages(root);
        if (!documents.TryGetValue(culture, out var document))
        {
            return Fail($"Unknown culture '{culture}'. Expected one of: {string.Join(", ", DefaultCultures)}");
        }

        foreach (var path in CollectPaths(document.Root).Keys.Order(StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(prefix) || path.StartsWith(prefix, StringComparison.Ordinal))
            {
                Console.WriteLine(path);
            }
        }

        return 0;
    }

    private int Children(DirectoryInfo root, string[] args)
    {
        var path = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal) ? args[0] : string.Empty;
        var options = ParseOptions(args.Skip(string.IsNullOrEmpty(path) ? 0 : 1).ToArray(), allowPositionals: false);
        var culture = options.GetValueOrDefault("culture") ?? "zh-CN";
        var documents = LoadLanguages(root);
        if (!documents.TryGetValue(culture, out var document))
        {
            return Fail($"Unknown culture '{culture}'. Expected one of: {string.Join(", ", DefaultCultures)}");
        }

        var node = string.IsNullOrEmpty(path) ? document.Root : TryGetNode(document.Root, ParsePath(path));
        if (node is null)
        {
            return Fail($"Path '{path}' does not exist in {culture}.");
        }

        foreach (var child in GetChildren(path, node))
        {
            Console.WriteLine($"{child.Path}\t{child.Kind}");
        }

        return 0;
    }

    private int Show(DirectoryInfo root, string[] args)
    {
        if (args.Length < 1)
        {
            return Fail("Usage: show <path> [--culture zh-CN] [--depth 1] [--limit 80]");
        }

        var path = args[0];
        var options = ParseOptions(args.Skip(1).ToArray(), allowPositionals: false);
        var culture = options.GetValueOrDefault("culture") ?? "zh-CN";
        var depth = ParseIntOption(options, "depth", 1);
        var limit = ParseIntOption(options, "limit", 80);
        var documents = LoadLanguages(root);
        if (!documents.TryGetValue(culture, out var document))
        {
            return Fail($"Unknown culture '{culture}'. Expected one of: {string.Join(", ", DefaultCultures)}");
        }

        var node = TryGetNode(document.Root, ParsePath(path));
        if (node is null)
        {
            return Fail($"Path '{path}' does not exist in {culture}.");
        }

        foreach (var item in EnumeratePreview(path, node, depth).Take(limit))
        {
            Console.WriteLine(item);
        }

        return 0;
    }

    private int Search(DirectoryInfo root, string[] args)
    {
        if (args.Length < 1)
        {
            return Fail("Usage: search <text> [--culture all|zh-CN] [--in path|value|all] [--prefix ContentWidgets] [--limit 50]");
        }

        var query = args[0];
        var options = ParseOptions(args.Skip(1).ToArray(), allowPositionals: false);
        var culture = options.GetValueOrDefault("culture") ?? "all";
        var target = options.GetValueOrDefault("in") ?? "all";
        var prefix = options.GetValueOrDefault("prefix") ?? string.Empty;
        var limit = ParseIntOption(options, "limit", 50);
        var documents = LoadLanguages(root);
        IEnumerable<LanguageDocument> selected = culture == "all"
            ? documents.Values
            : documents.TryGetValue(culture, out var selectedDocument)
                ? [selectedDocument]
                : throw new ArgumentException($"Unknown culture '{culture}'. Expected all or one of: {string.Join(", ", DefaultCultures)}");

        var count = 0;
        foreach (var document in selected)
        {
            foreach (var item in EnumerateLeaves(document.Root))
            {
                if (!string.IsNullOrEmpty(prefix) && !item.Path.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var pathMatches = target is "path" or "all" &&
                                  item.Path.Contains(query, StringComparison.OrdinalIgnoreCase);
                var valueMatches = target is "value" or "all" &&
                                   item.Value.Contains(query, StringComparison.OrdinalIgnoreCase);
                if (!pathMatches && !valueMatches)
                {
                    continue;
                }

                Console.WriteLine($"{document.Culture}\t{item.Path}\t{Truncate(item.Value, 120)}");
                count++;
                if (count >= limit)
                {
                    return 0;
                }
            }
        }

        return 0;
    }

    private static int Rules(string[] args)
    {
        EnsureNoUnexpectedArgs(args);
        Console.WriteLine("""
        SCNET language key rules:
        - Language files live in Content/Assets/Lang/{culture}.json.
        - All four language files must expose the same JSON key paths and value kinds.
        - Runtime LanguageManager.Get(section, key) reads JSON path: {section}.{key}.
        - Runtime LanguageManager.GetContentWidgets(name, key) reads JSON path: ContentWidgets.{name}.{key}.
        - XML text like [ScreenName:Key] is resolved by LabelWidget through ContentWidgets.ScreenName.Key.
        - Numeric keys are ordinary JSON object keys, not array indexes, unless the path explicitly uses [index].
        - Prefer screen/dialog/widget class names as ContentWidgets section names.
        - Avoid dots inside real JSON key names. Use underscores for compound flat keys, for example Strings.GameMode_Creative_Description.
        - Existing dotted key names are still addressable with escaped dots, but new keys should not need this.
        """);
        return 0;
    }

    private int Get(DirectoryInfo root, string[] args)
    {
        if (args.Length < 1)
        {
            return Fail("Usage: get <path> [--culture zh-CN]");
        }

        var path = args[0];
        var options = ParseOptions(args.Skip(1).ToArray(), allowPositionals: false);
        var culture = options.GetValueOrDefault("culture") ?? "zh-CN";
        var documents = LoadLanguages(root);
        if (!documents.TryGetValue(culture, out var document))
        {
            return Fail($"Unknown culture '{culture}'. Expected one of: {string.Join(", ", DefaultCultures)}");
        }

        var node = TryGetNode(document.Root, ParsePath(path));
        if (node is null)
        {
            return Fail($"Path '{path}' does not exist in {culture}.");
        }

        Console.WriteLine(node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : node.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return 0;
    }

    private int Set(DirectoryInfo root, string[] args)
    {
        if (args.Length < 1)
        {
            return Fail("Usage: set <path> --zh-CN value --en-US value --pt-PT value --ru-RU value [--allow-partial]");
        }

        var path = args[0];
        var options = ParseOptions(args.Skip(1).ToArray(), allowPositionals: false, flagNames: ["allow-partial"]);
        var allowPartial = options.ContainsKey("allow-partial");
        var documents = LoadLanguages(root);
        var suppliedCultures = DefaultCultures.Where(options.ContainsKey).ToArray();
        if (!allowPartial && suppliedCultures.Length != DefaultCultures.Length)
        {
            var missing = DefaultCultures.Where(culture => !options.ContainsKey(culture));
            return Fail($"Set requires all cultures. Missing: {string.Join(", ", missing)}. Use --allow-partial only for intentional one-language edits.");
        }

        if (suppliedCultures.Length == 0)
        {
            return Fail($"No language values supplied. Expected one or more of: {string.Join(", ", DefaultCultures.Select(culture => "--" + culture))}");
        }

        var segments = ParsePath(path);
        foreach (var culture in suppliedCultures)
        {
            SetNode(documents[culture].Root, segments, JsonValue.Create(options[culture])!);
        }

        SaveLanguages(documents);
        Console.WriteLine($"Updated '{path}' in {suppliedCultures.Length} language file(s).");
        return Check(root, []);
    }

    private int Remove(DirectoryInfo root, string[] args)
    {
        if (args.Length != 1)
        {
            return Fail("Usage: remove <path>");
        }

        var documents = LoadLanguages(root);
        var path = args[0];
        var segments = ParsePath(path);
        var removed = 0;
        foreach (var document in documents.Values)
        {
            if (RemoveNode(document.Root, segments))
            {
                removed++;
            }
        }

        SaveLanguages(documents);
        Console.WriteLine($"Removed '{path}' from {removed} language file(s).");
        return Check(root, []);
    }

    private int Rename(DirectoryInfo root, string[] args)
    {
        if (args.Length != 2)
        {
            return Fail("Usage: rename <old-path> <new-path>");
        }

        var oldPath = args[0];
        var newPath = args[1];
        var oldSegments = ParsePath(oldPath);
        var newSegments = ParsePath(newPath);
        var documents = LoadLanguages(root);
        var renamed = 0;
        foreach (var document in documents.Values)
        {
            var node = TryGetNode(document.Root, oldSegments);
            if (node is null)
            {
                continue;
            }

            var clone = node.DeepClone();
            SetNode(document.Root, newSegments, clone);
            if (!RemoveNode(document.Root, oldSegments))
            {
                throw new InvalidOperationException($"Failed removing old path '{oldPath}'.");
            }

            renamed++;
        }

        SaveLanguages(documents);
        Console.WriteLine($"Renamed '{oldPath}' to '{newPath}' in {renamed} language file(s).");
        return Check(root, []);
    }

    private Dictionary<string, LanguageDocument> LoadLanguages(DirectoryInfo root)
    {
        var documents = new Dictionary<string, LanguageDocument>(StringComparer.Ordinal);
        foreach (var culture in DefaultCultures)
        {
            var file = Path.Combine(root.FullName, "Content", "Assets", "Lang", $"{culture}.json");
            if (!File.Exists(file))
            {
                throw new FileNotFoundException($"Language file not found: {file}");
            }

            var json = File.ReadAllText(file, Encoding.UTF8);
            var node = JsonNode.Parse(json, nodeOptions: null, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = _readOptions.AllowTrailingCommas,
                CommentHandling = _readOptions.ReadCommentHandling
            }) as JsonObject ?? throw new InvalidOperationException($"Language root must be a JSON object: {file}");
            documents.Add(culture, new LanguageDocument(culture, file, node));
        }

        return documents;
    }

    private static void SaveLanguages(Dictionary<string, LanguageDocument> documents)
    {
        foreach (var document in documents.Values)
        {
            File.WriteAllText(document.FilePath, JsonFormatter.Format(document.Root), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static Dictionary<string, string> CollectPaths(JsonNode node, string prefix = "")
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(prefix))
        {
            paths[prefix] = GetKind(node);
        }

        if (node is JsonObject jsonObject)
        {
            foreach (var pair in jsonObject)
            {
                if (pair.Value is not null)
                {
                    foreach (var child in CollectPaths(pair.Value, JoinPath(prefix, pair.Key)))
                    {
                        paths[child.Key] = child.Value;
                    }
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (var i = 0; i < jsonArray.Count; i++)
            {
                if (jsonArray[i] is not null)
                {
                    foreach (var child in CollectPaths(jsonArray[i]!, $"{prefix}[{i}]"))
                    {
                        paths[child.Key] = child.Value;
                    }
                }
            }
        }

        return paths;
    }

    private static IEnumerable<(string Path, string Kind)> GetChildren(string prefix, JsonNode node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var pair in jsonObject.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (pair.Value is not null)
                {
                    yield return (JoinPath(prefix, pair.Key), GetKind(pair.Value));
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (var i = 0; i < jsonArray.Count; i++)
            {
                if (jsonArray[i] is not null)
                {
                    yield return ($"{prefix}[{i}]", GetKind(jsonArray[i]!));
                }
            }
        }
    }

    private static IEnumerable<string> EnumeratePreview(string path, JsonNode node, int depth)
    {
        if (depth <= 0 || node is JsonValue)
        {
            yield return $"{path}\t{GetKind(node)}\t{PreviewValue(node)}";
            yield break;
        }

        yield return $"{path}\t{GetKind(node)}";
        foreach (var child in GetChildren(path, node))
        {
            var childNode = TryGetNodeFromNode(node, RelativeChildSegment(path, child.Path));
            if (childNode is null)
            {
                continue;
            }

            foreach (var line in EnumeratePreview(child.Path, childNode, depth - 1))
            {
                yield return line;
            }
        }
    }

    private static IEnumerable<(string Path, string Value)> EnumerateLeaves(JsonNode node, string prefix = "")
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var pair in jsonObject)
            {
                if (pair.Value is not null)
                {
                    foreach (var child in EnumerateLeaves(pair.Value, JoinPath(prefix, pair.Key)))
                    {
                        yield return child;
                    }
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (var i = 0; i < jsonArray.Count; i++)
            {
                if (jsonArray[i] is not null)
                {
                    foreach (var child in EnumerateLeaves(jsonArray[i]!, $"{prefix}[{i}]"))
                    {
                        yield return child;
                    }
                }
            }
        }
        else
        {
            yield return (prefix, PreviewValue(node));
        }
    }

    private static JsonNode? TryGetNodeFromNode(JsonNode node, PathSegment segment)
    {
        return segment.IsIndex
            ? node is JsonArray array && segment.Index < array.Count ? array[segment.Index] : null
            : node is JsonObject obj ? obj[segment.Key] : null;
    }

    private static PathSegment RelativeChildSegment(string parentPath, string childPath)
    {
        if (childPath.StartsWith(parentPath + ".", StringComparison.Ordinal))
        {
            return ParsePath(childPath[(parentPath.Length + 1)..]).Single();
        }

        if (childPath.StartsWith(parentPath + "[", StringComparison.Ordinal))
        {
            var indexText = childPath[(parentPath.Length + 1)..^1];
            return new PathSegment(int.Parse(indexText));
        }

        return new PathSegment(childPath);
    }

    private static string GetKind(JsonNode node)
    {
        return node switch
        {
            JsonObject => "object",
            JsonArray => "array",
            JsonValue value when value.TryGetValue<string>(out _) => "string",
            JsonValue value when value.TryGetValue<bool>(out _) => "bool",
            JsonValue value when value.TryGetValue<int>(out _) => "number",
            JsonValue value when value.TryGetValue<long>(out _) => "number",
            JsonValue value when value.TryGetValue<double>(out _) => "number",
            JsonValue => "value",
            _ => "null"
        };
    }

    private static string JoinPath(string prefix, string key)
    {
        var escapedKey = EscapePathKey(key);
        return string.IsNullOrEmpty(prefix) ? escapedKey : $"{prefix}.{escapedKey}";
    }

    private static PathSegment[] ParsePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty.");
        }

        var segments = new List<PathSegment>();
        var key = new StringBuilder();
        for (var i = 0; i < path.Length; i++)
        {
            var ch = path[i];
            if (ch == '\\')
            {
                if (++i >= path.Length)
                {
                    throw new ArgumentException($"Invalid trailing escape in path '{path}'.");
                }

                key.Append(path[i]);
                continue;
            }

            if (ch == '.')
            {
                FlushKeySegment(segments, key, path);
                continue;
            }

            if (ch == '[')
            {
                if (key.Length > 0)
                {
                    FlushKeySegment(segments, key, path);
                }

                var end = path.IndexOf(']', i + 1);
                if (end < 0)
                {
                    throw new ArgumentException($"Invalid array segment in path '{path}'.");
                }

                var indexText = path[(i + 1)..end];
                if (!int.TryParse(indexText, out var index) || index < 0)
                {
                    throw new ArgumentException($"Invalid array index '{indexText}'.");
                }

                segments.Add(new PathSegment(index));
                i = end;
                continue;
            }

            key.Append(ch);
        }

        if (key.Length > 0)
        {
            FlushKeySegment(segments, key, path);
        }

        if (segments.Count == 0)
        {
            throw new ArgumentException($"Invalid path '{path}'.");
        }

        return segments.ToArray();
    }

    private static void FlushKeySegment(List<PathSegment> segments, StringBuilder key, string path)
    {
        var value = key.ToString().Trim();
        if (value.Length == 0)
        {
            throw new ArgumentException($"Invalid empty path segment in '{path}'.");
        }

        segments.Add(new PathSegment(value));
        key.Clear();
    }

    private static string EscapePathKey(string key)
    {
        return key.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(".", "\\.", StringComparison.Ordinal);
    }

    private static JsonNode? TryGetNode(JsonObject root, IReadOnlyList<PathSegment> segments)
    {
        JsonNode? current = root;
        foreach (var segment in segments)
        {
            current = segment.IsIndex
                ? current is JsonArray array && segment.Index < array.Count ? array[segment.Index] : null
                : current is JsonObject obj ? obj[segment.Key] : null;
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static void SetNode(JsonObject root, IReadOnlyList<PathSegment> segments, JsonNode value)
    {
        JsonNode current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var segment = segments[i];
            var nextSegment = segments[i + 1];
            if (segment.IsIndex)
            {
                if (current is not JsonArray array)
                {
                    throw new InvalidOperationException("Cannot create an array segment under a non-array node.");
                }

                while (array.Count <= segment.Index)
                {
                    array.Add(null);
                }

                array[segment.Index] ??= nextSegment.IsIndex ? new JsonArray() : new JsonObject();
                current = array[segment.Index]!;
            }
            else
            {
                if (current is not JsonObject obj)
                {
                    throw new InvalidOperationException($"Cannot create object key '{segment.Key}' under a non-object node.");
                }

                obj[segment.Key] ??= nextSegment.IsIndex ? new JsonArray() : new JsonObject();
                current = obj[segment.Key]!;
            }
        }

        var last = segments[^1];
        if (last.IsIndex)
        {
            if (current is not JsonArray array)
            {
                throw new InvalidOperationException("Cannot set an array item under a non-array node.");
            }

            while (array.Count <= last.Index)
            {
                array.Add(null);
            }

            array[last.Index] = value;
        }
        else
        {
            if (current is not JsonObject obj)
            {
                throw new InvalidOperationException($"Cannot set object key '{last.Key}' under a non-object node.");
            }

            obj[last.Key] = value;
        }
    }

    private static bool RemoveNode(JsonObject root, IReadOnlyList<PathSegment> segments)
    {
        if (segments.Count == 0)
        {
            return false;
        }

        JsonNode current = root;
        var parents = new List<(JsonNode Parent, PathSegment Segment, JsonNode Child)>();
        foreach (var segment in segments.Take(segments.Count - 1))
        {
            var next = segment.IsIndex
                ? current is JsonArray array && segment.Index < array.Count ? array[segment.Index] : null
                : current is JsonObject obj ? obj[segment.Key] : null;
            if (next is null)
            {
                return false;
            }

            parents.Add((current, segment, next));
            current = next;
        }

        var last = segments[^1];
        var removed = false;
        if (last.IsIndex)
        {
            if (current is not JsonArray array || last.Index >= array.Count)
            {
                return false;
            }

            array.RemoveAt(last.Index);
            removed = true;
        }
        else if (current is JsonObject parent)
        {
            removed = parent.Remove(last.Key);
        }

        if (removed)
        {
            PruneEmptyParents(parents);
        }

        return removed;
    }

    private static void PruneEmptyParents(List<(JsonNode Parent, PathSegment Segment, JsonNode Child)> parents)
    {
        for (var i = parents.Count - 1; i >= 0; i--)
        {
            var (parent, segment, child) = parents[i];
            var childIsEmpty = child switch
            {
                JsonObject obj => obj.Count == 0,
                JsonArray array => array.Count == 0,
                _ => false
            };
            if (!childIsEmpty)
            {
                break;
            }

            if (segment.IsIndex)
            {
                if (parent is JsonArray array && segment.Index < array.Count)
                {
                    array.RemoveAt(segment.Index);
                }
            }
            else if (parent is JsonObject obj)
            {
                obj.Remove(segment.Key);
            }
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args, bool allowPositionals, string[]? flagNames = null)
    {
        var flags = new HashSet<string>(flagNames ?? [], StringComparer.Ordinal);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (allowPositionals)
                {
                    continue;
                }

                throw new ArgumentException($"Unexpected argument '{arg}'.");
            }

            var name = arg[2..];
            if (flags.Contains(name))
            {
                options[name] = "true";
                continue;
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for option '{arg}'.");
            }

            options[name] = args[++i];
        }

        return options;
    }

    private static int ParseIntOption(Dictionary<string, string> options, string name, int defaultValue)
    {
        if (!options.TryGetValue(name, out var value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, out var result) || result < 0)
        {
            throw new ArgumentException($"Option '--{name}' must be a non-negative integer.");
        }

        return result;
    }

    private static string PreviewValue(JsonNode node)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var text)
            ? Truncate(text.ReplaceLineEndings("\\n"), 120)
            : Truncate(node.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), 120);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..Math.Max(0, maxLength - 1)] + "…";
    }

    private static void EnsureNoUnexpectedArgs(string[] args)
    {
        if (args.Length > 0)
        {
            throw new ArgumentException($"Unexpected argument '{args[0]}'.");
        }
    }

    private static DirectoryInfo FindRepositoryRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SCNET.slnx")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Content", "Assets", "Lang")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Cannot find SCNET repository root.");
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
        SCNET language management tool

        Commands:
          check
              Validate that zh-CN/en-US/pt-PT/ru-RU expose the same key paths and value kinds.

          rules
              Print SCNET localization key mapping rules.

          list [--culture zh-CN] [--prefix ContentWidgets.ModManagementScreen]
              List recursive key paths.

          children [path] [--culture zh-CN]
              List direct children under a JSON path.

          show <path> [--culture zh-CN] [--depth 1] [--limit 80]
              Preview a small subtree without opening the full language file.

          search <text> [--culture all|zh-CN] [--in path|value|all] [--prefix ContentWidgets] [--limit 50]
              Search key paths and/or localized values.

          get <path> [--culture zh-CN]
              Print one localized value.

          set <path> --zh-CN value --en-US value --pt-PT value --ru-RU value
              Add or update one key in all language files.

          set <path> --zh-CN value --allow-partial
              Update only supplied cultures. Use sparingly, then run check.

          remove <path>
              Remove one key path from all language files.

          rename <old-path> <new-path>
              Rename one key path in all language files.

        Examples:
          dotnet run --project LanguageTool -- check
          dotnet run --project LanguageTool -- rules
          dotnet run --project LanguageTool -- children ContentWidgets.PlayScreen
          dotnet run --project LanguageTool -- search restart --culture en-US --prefix ContentWidgets --limit 20
          dotnet run --project LanguageTool -- get ContentWidgets.ModManagementScreen.Refresh --culture en-US
          dotnet run --project LanguageTool -- get Strings.CharacterSkin_Description --culture en-US
          dotnet run --project LanguageTool -- set ContentWidgets.ContentScreen.13 --zh-CN 模组 --en-US Mods --pt-PT Mods --ru-RU Моды
        """);
    }
}

internal sealed record LanguageDocument(string Culture, string FilePath, JsonObject Root);

internal readonly record struct PathSegment
{
    public PathSegment(string key)
    {
        Key = key;
        Index = -1;
        IsIndex = false;
    }

    public PathSegment(int index)
    {
        Key = string.Empty;
        Index = index;
        IsIndex = true;
    }

    public string Key { get; }
    public int Index { get; }
    public bool IsIndex { get; }
}

internal static class JsonFormatter
{
    public static string Format(JsonNode node)
    {
        var builder = new StringBuilder();
        WriteNode(builder, node, 0);
        builder.AppendLine();
        return builder.ToString();
    }

    private static void WriteNode(StringBuilder builder, JsonNode? node, int depth)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                break;
            case JsonObject jsonObject:
                WriteObject(builder, jsonObject, depth);
                break;
            case JsonArray jsonArray:
                WriteArray(builder, jsonArray, depth);
                break;
            default:
                builder.Append(node.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                break;
        }
    }

    private static void WriteObject(StringBuilder builder, JsonObject jsonObject, int depth)
    {
        builder.Append('{');
        if (jsonObject.Count > 0)
        {
            builder.AppendLine();
            var index = 0;
            foreach (var pair in jsonObject)
            {
                Indent(builder, depth + 1);
                builder.Append(JsonSerializer.Serialize(pair.Key, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                builder.Append(": ");
                WriteNode(builder, pair.Value, depth + 1);
                if (++index < jsonObject.Count)
                {
                    builder.Append(',');
                }

                builder.AppendLine();
            }

            Indent(builder, depth);
        }

        builder.Append('}');
    }

    private static void WriteArray(StringBuilder builder, JsonArray jsonArray, int depth)
    {
        builder.Append('[');
        if (jsonArray.Count > 0)
        {
            builder.AppendLine();
            for (var i = 0; i < jsonArray.Count; i++)
            {
                Indent(builder, depth + 1);
                WriteNode(builder, jsonArray[i], depth + 1);
                if (i + 1 < jsonArray.Count)
                {
                    builder.Append(',');
                }

                builder.AppendLine();
            }

            Indent(builder, depth);
        }

        builder.Append(']');
    }

    private static void Indent(StringBuilder builder, int depth)
    {
        builder.Append(' ', depth * 4);
    }

}
