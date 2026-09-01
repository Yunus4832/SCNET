using System.Security.Cryptography;

using Content.Packaging;

using Microsoft.Extensions.Options;

namespace ContentServer.Infrastructure;

public sealed record StagedContentPackage(
    string TemporaryPath,
    ContentPackageInspection Inspection,
    string BlobHash,
    long Size,
    string FileName,
    string MediaType);

public sealed class ContentPackageStore
{
    private readonly string _packagesPath;
    private readonly string _temporaryPath;

    public ContentPackageStore(IHostEnvironment environment, IOptions<ContentServerOptions> options)
    {
        var root = Path.GetFullPath(options.Value.PackageStoragePath, environment.ContentRootPath);
        _packagesPath = Path.Combine(root, "packages");
        _temporaryPath = Path.Combine(root, "temp");
        Directory.CreateDirectory(_packagesPath);
        Directory.CreateDirectory(_temporaryPath);
    }

    public async Task<StagedContentPackage> StageAsync(
        Stream source,
        string fileName,
        string mediaType,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var temporaryPath = Path.Combine(_temporaryPath, $"{Guid.NewGuid():N}.upload");
        try
        {
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > maximumBytes)
                    {
                        throw new ContentPackageException("Package exceeds the upload size limit.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                await output.FlushAsync(cancellationToken);
            }

            ContentPackageInspection inspection;
            string blobHash;
            await using (var input = OpenRead(temporaryPath))
            {
                inspection = ContentPackageReader.Inspect(input);
                input.Position = 0;
                blobHash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
            }

            return new StagedContentPackage(temporaryPath, inspection, blobHash,
                new FileInfo(temporaryPath).Length, Path.GetFileName(fileName), mediaType);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    public bool Commit(StagedContentPackage package)
    {
        var destination = GetPath(package.Inspection.PackageHash);
        if (File.Exists(destination))
        {
            File.Delete(package.TemporaryPath);
            return false;
        }

        try
        {
            File.Move(package.TemporaryPath, destination);
            return true;
        }
        catch (IOException) when (File.Exists(destination))
        {
            File.Delete(package.TemporaryPath);
            return false;
        }
    }

    public FileStream Open(string packageHash) => OpenRead(GetPath(packageHash));

    public string CreateTemporaryPath(string suffix) =>
        Path.Combine(_temporaryPath, $"{Guid.NewGuid():N}{suffix}");

    public async Task<StagedContentPackage> InspectTemporaryPackageAsync(
        string temporaryPath,
        string fileName,
        CancellationToken cancellationToken)
    {
        ContentPackageInspection inspection;
        string blobHash;
        await using (var input = OpenRead(temporaryPath))
        {
            inspection = ContentPackageReader.Inspect(input);
            input.Position = 0;
            blobHash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
        }

        return new StagedContentPackage(temporaryPath, inspection, blobHash,
            new FileInfo(temporaryPath).Length, fileName, "application/vnd.scnet.content-package");
    }

    public void DeleteTemporary(StagedContentPackage package) => File.Delete(package.TemporaryPath);

    public IReadOnlyList<string> AuditOrphans(IReadOnlySet<string> referencedHashes) =>
        Directory.EnumerateFiles(_packagesPath, $"*{ContentPackageReader.FileExtension}")
            .Where(path => !referencedHashes.Contains(Path.GetFileNameWithoutExtension(path)))
            .ToArray();

    public int CleanOrphans(IReadOnlySet<string> referencedHashes)
    {
        var paths = AuditOrphans(referencedHashes);
        foreach (var path in paths)
        {
            File.Delete(path);
        }

        return paths.Count;
    }

    public int CleanTemporaryFiles()
    {
        var paths = Directory.EnumerateFiles(_temporaryPath, "*.upload").ToArray();
        foreach (var path in paths)
        {
            File.Delete(path);
        }

        return paths.Length;
    }

    private string GetPath(string packageHash) =>
        Path.Combine(_packagesPath, packageHash + ContentPackageReader.FileExtension);

    private static FileStream OpenRead(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
        64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
}
