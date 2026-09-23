using System.Globalization;
using System.Xml;
using System.Xml.Linq;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using SkiaSharp;

using Svg.Skia;

return GuiAtlasProgram.Run(args);

internal static class GuiAtlasProgram
{
    private const int _atlasWidth = 1024;
    private const int _padding = 2;
    private const int _svgSupersamplingScale = 2;

    public static int Run(string[] args)
    {
        try
        {
            switch (args)
            {
                case ["generate", var sourceDirectory, var texturePath, var definitionPath]:
                    Generate(sourceDirectory, texturePath, definitionPath);
                    return 0;
                case ["prepare-png", var inputPath, var outputPath, .. var options]:
                    PreparePng(inputPath, outputPath, options);
                    return 0;
                case ["extract", var texturePath, var definitionPath, var outputDirectory, .. var entries]:
                    Extract(texturePath, definitionPath, outputDirectory, entries);
                    return 0;
                case ["compose-game-button", var buttonType, var iconPath, var resourceName, .. var options]:
                    ButtonComposer.Compose(buttonType, iconPath, resourceName, options);
                    return 0;
                default:
                    WriteUsage();
                    return 2;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              InvalidDataException or XmlException)
        {
            Console.Error.WriteLine($"GuiAtlasTool: {exception.Message}");
            return 1;
        }
    }

    private static void PreparePng(string inputPath, string outputPath, string[] options)
    {
        var (premultiplied, overwrite) = ParsePrepareOptions(options);
        using var image = Image.Load<Rgba32>(inputPath);
        if (premultiplied)
        {
            UnpremultiplyAlpha(image);
        }

        SaveSourcePng(image, outputPath, overwrite);
        Console.WriteLine($"Prepared PNG source: {Path.GetFullPath(outputPath)}");
    }

    private static void Extract(string texturePath, string definitionPath, string outputDirectory,
        string[] arguments)
    {
        var overwrite = arguments.Contains("--overwrite", StringComparer.Ordinal);
        var names = arguments.Where(argument => argument != "--overwrite").ToArray();
        if (names.Length == 0 || arguments.Any(argument => argument.StartsWith("--", StringComparison.Ordinal) &&
                                                           argument != "--overwrite"))
        {
            throw new InvalidDataException("Extract requires one or more entry names and only supports --overwrite.");
        }

        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
        {
            throw new InvalidDataException("Extract entry names must be unique.");
        }

        var regions = ReadDefinition(definitionPath);
        using var atlas = Image.Load<Rgba32>(texturePath);
        var selections = names.Select(name =>
        {
            ValidateEntryName(name);
            if (!regions.TryGetValue(name, out var region))
            {
                throw new InvalidDataException($"Atlas entry does not exist: {name}");
            }

            if (region.X < 0 || region.Y < 0 || region.Width <= 0 || region.Height <= 0 ||
                region.X + region.Width > atlas.Width || region.Y + region.Height > atlas.Height)
            {
                throw new InvalidDataException($"Atlas entry is outside the texture: {name}");
            }

            var outputPath = Path.Combine(outputDirectory, $"{name}.png");
            if (!overwrite && File.Exists(outputPath))
            {
                throw new InvalidDataException($"Output already exists; pass --overwrite to replace it: " +
                                               Path.GetFullPath(outputPath));
            }

            return (Name: name, Region: region, OutputPath: outputPath);
        }).ToArray();

        foreach (var selection in selections)
        {
            using var image = atlas.Clone(context => context.Crop(
                new Rectangle(selection.Region.X, selection.Region.Y, selection.Region.Width,
                    selection.Region.Height)));
            UnpremultiplyAlpha(image);
            SaveSourcePng(image, selection.OutputPath, overwrite);
            Console.WriteLine($"Extracted PNG source: {Path.GetFullPath(selection.OutputPath)}");
        }
    }

    private static (bool Premultiplied, bool Overwrite) ParsePrepareOptions(string[] options)
    {
        var premultiplied = options.Contains("--premultiplied", StringComparer.Ordinal);
        var overwrite = options.Contains("--overwrite", StringComparer.Ordinal);
        if (options.Any(option => option is not "--premultiplied" and not "--overwrite"))
        {
            throw new InvalidDataException("Prepare-png only supports --premultiplied and --overwrite.");
        }

        return (premultiplied, overwrite);
    }

    private static void Generate(string sourceDirectory, string texturePath, string definitionPath)
    {
        var fullSourceDirectory = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(fullSourceDirectory))
        {
            throw new InvalidDataException($"Asset source directory does not exist: {fullSourceDirectory}");
        }

        var sources = Directory.EnumerateFiles(fullSourceDirectory, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".svg" or ".png")
            .Select(ReadSource)
            .OrderByDescending(source => source.Height)
            .ThenByDescending(source => source.Width)
            .ThenBy(source => source.Name, StringComparer.Ordinal)
            .ToArray();
        if (sources.Length == 0)
        {
            throw new InvalidDataException($"No SVG or PNG files were found in {fullSourceDirectory}.");
        }

        var duplicate = sources.GroupBy(source => source.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidDataException($"Duplicate atlas entry name: {duplicate.Key}");
        }

        var entries = Pack(sources);
        var atlasHeight = NextPowerOfTwo(entries.Max(entry => entry.Y + entry.Source.Height + _padding));
        if (atlasHeight > 4096)
        {
            throw new InvalidDataException($"Atlas height {atlasHeight} exceeds the 4096 pixel limit.");
        }

        using var atlas = new Image<Rgba32>(_atlasWidth, atlasHeight);
        foreach (var entry in entries)
        {
            using var image = Render(entry.Source);
            PremultiplyAlpha(image);
            CopyImage(image, atlas, entry.X, entry.Y);
        }

        var definition = string.Join('\n', entries.OrderBy(entry => entry.Source.Name, StringComparer.Ordinal)
            .Select(entry => string.Create(CultureInfo.InvariantCulture,
                $"{entry.Source.Name} {entry.X} {entry.Y} {entry.Source.Width} {entry.Source.Height} 0 0 0 0"))) +
                         '\n';
        WriteOutputs(atlas, definition, texturePath, definitionPath);
        Console.WriteLine($"Generated {sources.Length} atlas entries in {_atlasWidth}x{atlasHeight} texture.");
    }

    private static AtlasSource ReadSource(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        ValidateEntryName(name);

        return Path.GetExtension(path) switch
        {
            ".svg" => ReadSvgSource(name, path),
            ".png" => ReadPngSource(name, path),
            _ => throw new InvalidDataException($"Unsupported atlas source: {path}")
        };
    }

    private static void ValidateEntryName(string name)
    {
        if (name.Length == 0 || name.Any(character => char.IsWhiteSpace(character)) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetFileName(name) != name)
        {
            throw new InvalidDataException($"Invalid atlas entry name: {name}");
        }
    }

    private static AtlasSource ReadSvgSource(string name, string path)
    {
        using var stream = File.OpenRead(path);
        var document = XDocument.Load(stream, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException($"SVG has no root element: {path}");
        if (root.Name.LocalName != "svg")
        {
            throw new InvalidDataException($"File is not an SVG document: {path}");
        }

        var width = ParsePixelSize(root.Attribute("width")?.Value, "width", path);
        var height = ParsePixelSize(root.Attribute("height")?.Value, "height", path);
        ParseViewBox(root.Attribute("viewBox")?.Value, width, height, path);
        return new AtlasSource(name, path, width, height, AtlasSourceKind.Svg);
    }

    private static AtlasSource ReadPngSource(string name, string path)
    {
        var info = Image.Identify(path) ?? throw new InvalidDataException($"Unable to read PNG: {path}");
        return new AtlasSource(name, path, info.Width, info.Height, AtlasSourceKind.Png);
    }

    private static SKRect ParseViewBox(string? value, int width, int height, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SKRect.Create(width, height);
        }

        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 ||
            !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var viewBoxWidth) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var viewBoxHeight) ||
            viewBoxWidth <= 0f || viewBoxHeight <= 0f)
        {
            throw new InvalidDataException($"SVG viewBox must contain four valid numbers: {path}");
        }

        return SKRect.Create(x, y, viewBoxWidth, viewBoxHeight);
    }

    private static int ParsePixelSize(string? value, string attribute, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"SVG must declare an integer pixel {attribute}: {path}");
        }

        var text = value.EndsWith("px", StringComparison.OrdinalIgnoreCase) ? value[..^2] : value;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result <= 0)
        {
            throw new InvalidDataException($"SVG {attribute} must use a positive integer pixel value: {path}");
        }

        return result;
    }

    private static AtlasEntry[] Pack(AtlasSource[] sources)
    {
        var entries = new List<AtlasEntry>(sources.Length);
        var x = _padding;
        var y = _padding;
        var rowHeight = 0;
        foreach (var source in sources)
        {
            if (source.Width + 2 * _padding > _atlasWidth)
            {
                throw new InvalidDataException($"Asset is wider than the atlas: {source.Path}");
            }

            if (x + source.Width + _padding > _atlasWidth)
            {
                x = _padding;
                y += rowHeight + _padding;
                rowHeight = 0;
            }

            entries.Add(new AtlasEntry(source, x, y));
            x += source.Width + _padding;
            rowHeight = Math.Max(rowHeight, source.Height);
        }

        return [.. entries];
    }

    private static Image<Rgba32> Render(AtlasSource source)
    {
        return source.Kind switch
        {
            AtlasSourceKind.Svg => RenderSvg(source),
            AtlasSourceKind.Png => Image.Load<Rgba32>(source.Path),
            _ => throw new InvalidDataException($"Unsupported atlas source: {source.Path}")
        };
    }

    private static Image<Rgba32> RenderSvg(AtlasSource source)
    {
        using var svg = new SKSvg();
        using var stream = File.OpenRead(source.Path);
        var picture = svg.Load(stream) ?? throw new InvalidDataException($"Unable to render SVG: {source.Path}");
        var bounds = picture.CullRect;
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            throw new InvalidDataException($"SVG has no visible content: {source.Path}");
        }

        var renderWidth = source.Width * _svgSupersamplingScale;
        var renderHeight = source.Height * _svgSupersamplingScale;
        var info = new SKImageInfo(renderWidth, renderHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidDataException("Unable to create SVG surface.");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var scale = Math.Min(renderWidth / bounds.Width, renderHeight / bounds.Height);
        var offsetX = (renderWidth - bounds.Width * scale) / 2f;
        var offsetY = (renderHeight - bounds.Height * scale) / 2f;
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100) ??
                         throw new InvalidDataException($"Unable to encode SVG: {source.Path}");
        using var encoded = data.AsStream();
        var image = Image.Load<Rgba32>(encoded);
        image.Mutate(context => context.Resize(source.Width, source.Height, KnownResamplers.Bicubic));
        return image;
    }

    private static void PremultiplyAlpha(Image<Rgba32> image)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (ref var pixel in row)
                {
                    pixel.R = (byte)((pixel.R * pixel.A + 127) / 255);
                    pixel.G = (byte)((pixel.G * pixel.A + 127) / 255);
                    pixel.B = (byte)((pixel.B * pixel.A + 127) / 255);
                }
            }
        });
    }

    private static void UnpremultiplyAlpha(Image<Rgba32> image)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (ref var pixel in row)
                {
                    if (pixel.A == 0)
                    {
                        pixel.R = 0;
                        pixel.G = 0;
                        pixel.B = 0;
                    }
                    else
                    {
                        pixel.R = (byte)Math.Min(255, (pixel.R * 255 + pixel.A / 2) / pixel.A);
                        pixel.G = (byte)Math.Min(255, (pixel.G * 255 + pixel.A / 2) / pixel.A);
                        pixel.B = (byte)Math.Min(255, (pixel.B * 255 + pixel.A / 2) / pixel.A);
                    }
                }
            }
        });
    }

    private static Dictionary<string, AtlasRegion> ReadDefinition(string definitionPath)
    {
        var regions = new Dictionary<string, AtlasRegion>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(definitionPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5 ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
                !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
            {
                throw new InvalidDataException($"Invalid atlas definition line: {line}");
            }

            if (!regions.TryAdd(parts[0], new AtlasRegion(x, y, width, height)))
            {
                throw new InvalidDataException($"Duplicate atlas definition entry: {parts[0]}");
            }
        }

        return regions;
    }

    private static void SaveSourcePng(Image<Rgba32> image, string outputPath, bool overwrite)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        if (!overwrite && File.Exists(fullOutputPath))
        {
            throw new InvalidDataException($"Output already exists; pass --overwrite to replace it: {fullOutputPath}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        var temporaryPath = $"{fullOutputPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            image.Save(temporaryPath, new PngEncoder
            {
                ColorType = PngColorType.RgbWithAlpha,
                CompressionLevel = PngCompressionLevel.BestCompression
            });
            File.Move(temporaryPath, fullOutputPath, overwrite);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static void CopyImage(Image<Rgba32> source, Image<Rgba32> target, int targetX, int targetY)
    {
        source.ProcessPixelRows(target, (sourceAccessor, targetAccessor) =>
        {
            for (var y = 0; y < sourceAccessor.Height; y++)
            {
                sourceAccessor.GetRowSpan(y).CopyTo(targetAccessor.GetRowSpan(targetY + y)[targetX..]);
            }
        });
    }

    private static void WriteOutputs(Image<Rgba32> atlas, string definition, string texturePath,
        string definitionPath)
    {
        var fullTexturePath = Path.GetFullPath(texturePath);
        var fullDefinitionPath = Path.GetFullPath(definitionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullTexturePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDefinitionPath)!);
        var textureTemporaryPath = $"{fullTexturePath}.{Guid.NewGuid():N}.tmp";
        var definitionTemporaryPath = $"{fullDefinitionPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            atlas.Save(textureTemporaryPath, new PngEncoder
            {
                ColorType = PngColorType.RgbWithAlpha,
                CompressionLevel = PngCompressionLevel.BestCompression
            });
            File.WriteAllText(definitionTemporaryPath, definition);
            File.Move(textureTemporaryPath, fullTexturePath, true);
            File.Move(definitionTemporaryPath, fullDefinitionPath, true);
        }
        finally
        {
            File.Delete(textureTemporaryPath);
            File.Delete(definitionTemporaryPath);
        }
    }

    private static int NextPowerOfTwo(int value)
    {
        var result = 1;
        while (result < value)
        {
            result *= 2;
        }

        return result;
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine(
            "  SCNET.GuiAtlasTool generate <asset-directory> <atlas-texture.png> <atlas-definition.txt>");
        Console.Error.WriteLine(
            "  SCNET.GuiAtlasTool prepare-png <input.png> <output.png> [--premultiplied] [--overwrite]");
        Console.Error.WriteLine(
            "  SCNET.GuiAtlasTool extract <atlas.png> <atlas.txt> <output-directory> <entry>... [--overwrite]");
        Console.Error.WriteLine(
            "  SCNET.GuiAtlasTool compose-game-button <floating|left-attached|right-attached> " +
            "<icon.svg> <resource-name> [--scale <factor>] [--offset-x <value>] [--offset-y <value>] " +
            "[--asset-directory <directory>] [--overwrite]");
    }

    private sealed record AtlasSource(string Name, string Path, int Width, int Height, AtlasSourceKind Kind);

    private sealed record AtlasEntry(AtlasSource Source, int X, int Y);

    private sealed record AtlasRegion(int X, int Y, int Width, int Height);

    private enum AtlasSourceKind
    {
        Svg,
        Png
    }
}
