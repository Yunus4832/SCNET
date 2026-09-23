using System.Globalization;
using System.Xml.Linq;

internal static class ButtonComposer
{
    private static readonly XNamespace _svgNamespace = "http://www.w3.org/2000/svg";

    public static void Compose(string buttonType, string iconPath, string resourceName, string[] arguments)
    {
        ValidateResourceName(resourceName);
        var options = ParseOptions(arguments);
        var definition = GetDefinition(buttonType);
        var templateDirectory = FindTemplateDirectory();
        var assetDirectory = Path.GetFullPath(options.AssetDirectory ?? FindDefaultAssetDirectory());
        var normalPath = Path.Combine(assetDirectory, $"{resourceName}.svg");
        var pressedPath = Path.Combine(assetDirectory, $"{resourceName}_Pressed.svg");
        EnsureOutputsAvailable(normalPath, pressedPath, options.Overwrite);

        var icon = LoadIcon(iconPath);
        var scale = Math.Min(definition.SafeWidth / icon.ViewBox.Width,
            definition.SafeHeight / icon.ViewBox.Height) * options.Scale;
        var translateX = definition.CenterX + options.OffsetX -
                         (icon.ViewBox.X + icon.ViewBox.Width / 2f) * scale;
        var translateY = definition.CenterY + options.OffsetY -
                         (icon.ViewBox.Y + icon.ViewBox.Height / 2f) * scale;
        var transform = string.Create(CultureInfo.InvariantCulture,
            $"translate({translateX} {translateY}) scale({scale})");

        var normal = ComposeDocument(Path.Combine(templateDirectory, definition.NormalTemplate), icon, transform);
        var pressed = ComposeDocument(Path.Combine(templateDirectory, definition.PressedTemplate), icon, transform);
        WritePair(normal, normalPath, pressed, pressedPath, options.Overwrite);
        Console.WriteLine($"Created button sources: {normalPath}, {pressedPath}");
    }

    private static ComposeOptions ParseOptions(string[] arguments)
    {
        var scale = 1f;
        var offsetX = 0f;
        var offsetY = 0f;
        string? assetDirectory = null;
        var overwrite = false;
        for (var i = 0; i < arguments.Length; i++)
        {
            switch (arguments[i])
            {
                case "--scale":
                    scale = ReadPositiveFloat(arguments, ref i, "--scale");
                    break;
                case "--offset-x":
                    offsetX = ReadFloat(arguments, ref i, "--offset-x");
                    break;
                case "--offset-y":
                    offsetY = ReadFloat(arguments, ref i, "--offset-y");
                    break;
                case "--asset-directory":
                    assetDirectory = ReadValue(arguments, ref i, "--asset-directory");
                    break;
                case "--overwrite":
                    overwrite = true;
                    break;
                default:
                    throw new InvalidDataException($"Unsupported compose-game-button option: {arguments[i]}");
            }
        }

        return new ComposeOptions(scale, offsetX, offsetY, assetDirectory, overwrite);
    }

    private static float ReadPositiveFloat(string[] arguments, ref int index, string option)
    {
        var value = ReadFloat(arguments, ref index, option);
        if (value <= 0f)
        {
            throw new InvalidDataException($"{option} must be greater than zero.");
        }

        return value;
    }

    private static float ReadFloat(string[] arguments, ref int index, string option)
    {
        var text = ReadValue(arguments, ref index, option);
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !float.IsFinite(value))
        {
            throw new InvalidDataException($"{option} requires a finite number.");
        }

        return value;
    }

    private static string ReadValue(string[] arguments, ref int index, string option)
    {
        index++;
        if (index >= arguments.Length || arguments[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{option} requires a value.");
        }

        return arguments[index];
    }

    private static ButtonDefinition GetDefinition(string buttonType)
    {
        return buttonType switch
        {
            "floating" => new ButtonDefinition("Floating.svg", "FloatingPressed.svg", 24f, 22f, 16f, 15f),
            "left-attached" =>
                new ButtonDefinition("LeftAttached.svg", "LeftAttachedPressed.svg", 24f, 24f, 16f, 15f),
            "right-attached" =>
                new ButtonDefinition("RightAttached.svg", "RightAttachedPressed.svg", 24f, 24f, 16f, 15f),
            _ => throw new InvalidDataException($"Unsupported button type: {buttonType}")
        };
    }

    private static string FindTemplateDirectory()
    {
        var sourceDirectory = Path.GetFullPath(Path.Combine("GuiAtlasTool", "Templates", "Buttons"));
        if (Directory.Exists(sourceDirectory))
        {
            return sourceDirectory;
        }

        var deployedDirectory = Path.Combine(AppContext.BaseDirectory, "Templates", "Buttons");
        if (Directory.Exists(deployedDirectory))
        {
            return deployedDirectory;
        }

        throw new InvalidDataException("Button template directory was not found.");
    }

    private static string FindDefaultAssetDirectory()
    {
        var directory = Path.GetFullPath(Path.Combine("GuiAtlasTool", "Assets"));
        if (!Directory.Exists(directory))
        {
            throw new InvalidDataException(
                "Default asset directory was not found; pass --asset-directory explicitly.");
        }

        return directory;
    }

    private static IconDocument LoadIcon(string iconPath)
    {
        var fullPath = Path.GetFullPath(iconPath);
        var document = XDocument.Load(fullPath, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidDataException($"SVG has no root element: {fullPath}");
        if (root.Name.LocalName != "svg")
        {
            throw new InvalidDataException($"File is not an SVG document: {fullPath}");
        }

        var viewBox = ParseViewBox(root.Attribute("viewBox")?.Value, fullPath);
        RejectExternalResources(root, fullPath);
        var content = root.Nodes().Where(node => node is not XElement element ||
                                                 element.Name.LocalName is not "namedview" and not "metadata")
            .Select(CloneNode).ToArray();
        PrefixIds(content);
        return new IconDocument(content, viewBox);
    }

    private static ViewBox ParseViewBox(string? value, string path)
    {
        var parts = value?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts?.Length != 4 ||
            !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var height) ||
            width <= 0f || height <= 0f)
        {
            throw new InvalidDataException($"Icon SVG must declare a valid viewBox: {path}");
        }

        return new ViewBox(x, y, width, height);
    }

    private static void RejectExternalResources(XElement root, string path)
    {
        foreach (var attribute in root.DescendantsAndSelf().Attributes()
                     .Where(attribute => attribute.Name.LocalName is "href" or "src"))
        {
            var value = attribute.Value;
            if (!value.StartsWith('#') && !value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Icon SVG contains an external resource: {path}");
            }
        }
    }

    private static XNode CloneNode(XNode node)
    {
        return node switch
        {
            XElement element => new XElement(element),
            XText text => new XText(text.Value),
            XComment comment => new XComment(comment.Value),
            _ => new XText(string.Empty)
        };
    }

    private static void PrefixIds(XNode[] nodes)
    {
        var elements = nodes.OfType<XElement>().SelectMany(element => element.DescendantsAndSelf()).ToArray();
        var idAttributes = elements.Select(element => element.Attribute("id"))
            .Where(attribute => attribute != null).Select(attribute => attribute!).ToArray();
        var duplicateId = idAttributes.GroupBy(attribute => attribute.Value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId != null)
        {
            throw new InvalidDataException($"Icon SVG contains a duplicate id: {duplicateId.Key}");
        }

        var idMap = idAttributes.Select((attribute, index) =>
                (Attribute: attribute, NewId: $"button-icon-{index}"))
            .ToDictionary(item => item.Attribute.Value, item => item.NewId, StringComparer.Ordinal);
        foreach (var item in elements.Select(element => element.Attribute("id")).Where(attribute => attribute != null))
        {
            item!.Value = idMap[item.Value];
        }

        foreach (var attribute in elements.SelectMany(element => element.Attributes()).Where(attribute =>
                     attribute.Name.LocalName != "id"))
        {
            attribute.Value = RewriteUrlReferences(attribute.Value, idMap);
            if (attribute.Name.LocalName == "href" && attribute.Value.StartsWith('#') &&
                idMap.TryGetValue(attribute.Value[1..], out var referencedId))
            {
                attribute.Value = $"#{referencedId}";
            }
        }

        foreach (var text in elements.Where(element => element.Name.LocalName == "style").Nodes().OfType<XText>())
        {
            text.Value = RewriteUrlReferences(text.Value, idMap);
        }
    }

    private static string RewriteUrlReferences(string value, Dictionary<string, string> idMap)
    {
        foreach (var pair in idMap)
        {
            value = value.Replace($"url(#{pair.Key})", $"url(#{pair.Value})", StringComparison.Ordinal);
        }

        return value;
    }

    private static XDocument ComposeDocument(string templatePath, IconDocument icon, string transform)
    {
        var document = XDocument.Load(templatePath, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidDataException($"Button template has no root: {templatePath}");
        root.Add(new XElement(_svgNamespace + "g", new XAttribute("id", "buttonIcon"),
            new XAttribute("transform", transform), icon.Nodes.Select(CloneNode)));
        return document;
    }

    private static void EnsureOutputsAvailable(string normalPath, string pressedPath, bool overwrite)
    {
        if (!overwrite && (File.Exists(normalPath) || File.Exists(pressedPath)))
        {
            throw new InvalidDataException(
                $"Button output already exists; pass --overwrite to replace the pair: {normalPath}");
        }
    }

    private static void WritePair(XDocument normal, string normalPath, XDocument pressed, string pressedPath,
        bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(normalPath)!);
        var normalTemporaryPath = $"{normalPath}.{Guid.NewGuid():N}.tmp";
        var pressedTemporaryPath = $"{pressedPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            normal.Save(normalTemporaryPath);
            pressed.Save(pressedTemporaryPath);
            File.Move(normalTemporaryPath, normalPath, overwrite);
            File.Move(pressedTemporaryPath, pressedPath, overwrite);
        }
        finally
        {
            File.Delete(normalTemporaryPath);
            File.Delete(pressedTemporaryPath);
        }
    }

    private static void ValidateResourceName(string resourceName)
    {
        if (resourceName.Length == 0 || resourceName.Any(character => char.IsWhiteSpace(character)) ||
            resourceName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            Path.GetFileName(resourceName) != resourceName)
        {
            throw new InvalidDataException($"Invalid atlas resource name: {resourceName}");
        }
    }

    private sealed record ComposeOptions(float Scale, float OffsetX, float OffsetY, string? AssetDirectory,
        bool Overwrite);

    private sealed record ButtonDefinition(string NormalTemplate, string PressedTemplate, float SafeWidth,
        float SafeHeight, float CenterX, float CenterY);

    private sealed record IconDocument(XNode[] Nodes, ViewBox ViewBox);

    private sealed record ViewBox(float X, float Y, float Width, float Height);
}
