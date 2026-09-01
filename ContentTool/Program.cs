using Content.Packaging;

return await ContentToolProgram.RunAsync(args);

internal static class ContentToolProgram
{
    public static Task<int> RunAsync(string[] args)
    {
        if (args is ["pack", _, _, _])
        {
            return Task.FromResult(Pack(args[1], args[2], args[3]));
        }

        if (args.Length != 2 || args[0] is not ("inspect" or "verify"))
        {
            WriteUsage();
            return Task.FromResult(2);
        }

        var path = args[1];
        if (!path.EndsWith(ContentPackageReader.FileExtension, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Package path must use {ContentPackageReader.FileExtension}.");
            return Task.FromResult(2);
        }

        try
        {
            using var stream = File.OpenRead(path);
            var inspection = ContentPackageReader.Inspect(stream);
            if (args[0] == "inspect")
            {
                Console.WriteLine($"Type: {inspection.Manifest.Type}");
                Console.WriteLine($"Identifier: {inspection.Manifest.Identifier}");
                Console.WriteLine($"Name: {inspection.Manifest.Name}");
                Console.WriteLine($"Version: {inspection.Manifest.Version}");
                Console.WriteLine($"PackageHash: {inspection.PackageHash}");
                Console.WriteLine($"Payload entries: {inspection.Entries.Count - 1}");
            }
            else
            {
                Console.WriteLine($"Verified {path}");
                Console.WriteLine($"PackageHash: {inspection.PackageHash}");
            }

            return Task.FromResult(0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ContentPackageException)
        {
            Console.Error.WriteLine($"ContentTool: {exception.Message}");
            return Task.FromResult(1);
        }
    }

    private static int Pack(string manifestPath, string payloadDirectory, string outputPath)
    {
        if (!outputPath.EndsWith(ContentPackageReader.FileExtension, StringComparison.Ordinal) ||
            !File.Exists(manifestPath) || !Directory.Exists(payloadDirectory))
        {
            WriteUsage();
            return 2;
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath)!;
        Directory.CreateDirectory(outputDirectory);
        var temporaryPath = Path.Combine(outputDirectory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var manifest = ContentPackageManifest.Parse(File.ReadAllBytes(manifestPath));
            var entries = Directory.EnumerateFiles(payloadDirectory, "*", SearchOption.AllDirectories)
                .Select(path =>
                {
                    var relativePath = Path.GetRelativePath(payloadDirectory, path).Replace('\\', '/');
                    var length = new FileInfo(path).Length;
                    return new ContentPackageWriteEntry($"payload/{relativePath}", length, () => File.OpenRead(path));
                })
                .ToArray();
            string hash;
            using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                hash = ContentPackageWriter.Write(output, manifest, entries);
                output.Flush(true);
            }

            File.Move(temporaryPath, fullOutputPath, overwrite: true);
            Console.WriteLine($"Created {fullOutputPath}");
            Console.WriteLine($"PackageHash: {hash}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ContentPackageException)
        {
            Console.Error.WriteLine($"ContentTool: {exception.Message}");
            return 1;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  ContentTool inspect <package.scpkg>");
        Console.Error.WriteLine("  ContentTool verify <package.scpkg>");
        Console.Error.WriteLine("  ContentTool pack <manifest.json> <payload-directory> <output.scpkg>");
    }
}
