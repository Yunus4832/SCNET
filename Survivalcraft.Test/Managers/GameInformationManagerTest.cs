using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

using Game;
using Game.Content;
using Game.Managers;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class GameInformationManagerTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"scnet-update-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(Platform.Android, Architecture.Arm, false, "android-arm32")]
    [InlineData(Platform.Android, Architecture.Arm64, false, "android-arm64")]
    [InlineData(Platform.Android, Architecture.X86, false, null)]
    [InlineData(Platform.Android, Architecture.X64, false, null)]
    [InlineData(Platform.Desktop, Architecture.X64, false, "linux-x64")]
    [InlineData(Platform.Desktop, Architecture.X64, true, "windows-x64")]
    [InlineData(Platform.Desktop, Architecture.Arm64, false, null)]
    public void PlatformSelectionNeverSubstitutesAnotherArchitecture(Platform platform, Architecture architecture,
        bool windows, string? expected)
    {
        Assert.Equal(expected, GameInformationManager.GetReleasePlatform(platform, architecture, windows));
    }

    [Theory]
    [InlineData("0.1.0", "UpToDate")]
    [InlineData("0.0.9", "UpToDate")]
    [InlineData("0.1.1", "Available")]
    public void ValidReleasesAreComparedWithTheCurrentVersion(string version, string expected)
    {
        Assert.Equal(expected, GameInformationManager.EvaluateRelease(Release() with { Version = version },
            "linux-x64", "0.1.0").ToString());
    }

    [Theory]
    [InlineData("0.1.0-alpha.2", "0.1.0-alpha.10", "Available")]
    [InlineData("0.1.0-alpha.10", "0.1.0", "Available")]
    [InlineData("0.1.9", "0.1.10", "Available")]
    [InlineData("0.1.0", "0.1.0-alpha.1", "UpToDate")]
    public void ReleaseComparisonUsesSemanticVersionOrdering(string current, string available, string expected)
    {
        Assert.Equal(expected, GameInformationManager.EvaluateRelease(Release() with { Version = available },
            "linux-x64", current).ToString());
    }

    [Fact]
    public void NoPublishedReleaseIsDistinctFromAlreadyUpToDate()
    {
        Assert.Equal(GameInformationManager.ReleaseCheckResult.NotPublished,
            GameInformationManager.EvaluateRelease(null, "linux-x64", "0.1.0"));
    }

    [Theory]
    [InlineData("version", "invalid")]
    [InlineData("platform", "windows-x64")]
    [InlineData("hash", "abc")]
    [InlineData("hash", "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    [InlineData("url", "file:///release.zip")]
    [InlineData("status", "draft")]
    [InlineData("id", "")]
    public void InvalidMetadataIsRejectedEvenWhenVersionIsNotNewer(string field, string value)
    {
        var release = Release() with { Version = "0.0.9" };
        release = field switch
        {
            "version" => release with { Version = value },
            "platform" => release with { Platform = value },
            "hash" => release with { Sha256 = value },
            "url" => release with { DownloadUrl = value },
            "status" => release with { Status = value },
            _ => release with { Id = value }
        };
        Assert.Throws<InvalidDataException>(() =>
            GameInformationManager.EvaluateRelease(release, "linux-x64", "0.1.0"));
    }

    [Fact]
    public void RepeatedChecksShareOneOperationUntilItEnds()
    {
        var (manager, source, _) = CreateSources();
        var operation = GameInformationManager.TryBeginUpdate(manager, source, false)!;
        try
        {
            Assert.Null(GameInformationManager.TryBeginUpdate(manager, source, true));
            Assert.True(operation.ManualRequested);
            Assert.True(GameInformationManager.IsCurrent(operation));
        }
        finally
        {
            GameInformationManager.EndUpdate(operation);
        }

        var next = GameInformationManager.TryBeginUpdate(manager, source, true)!;
        Assert.NotSame(operation, next);
        GameInformationManager.EndUpdate(next);
    }

    [Fact]
    public void SwitchingAwayAndBackInvalidatesOldResultsAndVerifiedPackage()
    {
        var (manager, first, second) = CreateSources();
        var old = GameInformationManager.TryBeginUpdate(manager, first, true)!;
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "verified.zip");
        File.WriteAllText(path, "verified");
        old.PackagePath = path;
        manager.SelectGameInformationSource(second.Id);
        manager.SelectGameInformationSource(first.Id);
        Assert.True(old.Token.IsCancellationRequested);
        Assert.False(GameInformationManager.IsCurrent(old));
        Assert.False(File.Exists(path));

        var next = GameInformationManager.TryBeginUpdate(manager, first, true)!;
        try
        {
            GameInformationManager.EndUpdate(old);
            Assert.True(GameInformationManager.IsCurrent(next));
        }
        finally
        {
            GameInformationManager.EndUpdate(next);
        }
    }

    [Fact]
    public void EditingTheSourceAddressInvalidatesAnOperationButReorderingDoesNot()
    {
        var (manager, first, second) = CreateSources();
        var operation = GameInformationManager.TryBeginUpdate(manager, first, true)!;
        try
        {
            manager.SetOrder([second.Id, first.Id]);
            Assert.True(GameInformationManager.IsCurrent(operation));
            manager.Edit(first with { BaseUrl = "https://changed.example" });
            Assert.False(GameInformationManager.IsCurrent(operation));
            Assert.True(operation.Token.IsCancellationRequested);
        }
        finally
        {
            GameInformationManager.EndUpdate(operation);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DisablingOrDeletingTheSourceCancelsTheOperation(bool delete)
    {
        var (manager, first, _) = CreateSources();
        var operation = GameInformationManager.TryBeginUpdate(manager, first, true)!;
        try
        {
            if (delete)
            {
                manager.Delete(first.Id);
            }
            else
            {
                manager.Edit(first with { IsEnabled = false });
            }

            Assert.Null(manager.GameInformationSource);
            Assert.True(operation.Token.IsCancellationRequested);
            Assert.False(GameInformationManager.IsCurrent(operation));
        }
        finally
        {
            GameInformationManager.EndUpdate(operation);
        }
    }

    [Fact]
    public async Task ReleaseCheckTimesOutWhenHeadersArriveButJsonBodyStalls()
    {
        using var body = new BlockingBody();
        using var http = new HttpClient(new ResponseHandler(() => new StreamContent(body)));
        using var client = new ContentServerClient("https://source.example", http);
        var request = GameInformationManager.FetchReleaseAsync(client, "linux-x64", CancellationToken.None,
            TimeSpan.FromMilliseconds(100));
        await body.Reading.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<TimeoutException>(() => request.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task DownloadTimesOutAndRemovesPartialFileWhenBodyStalls()
    {
        using var body = new BlockingBody();
        using var client = new HttpClient(new ResponseHandler(() => new StreamContent(body)));
        var path = Path.Combine(_directory, "update.zip");
        var download = GameInformationManager.DownloadPackageAsync(client, Release(), path, (_, _) => { },
            CancellationToken.None, TimeSpan.FromMilliseconds(100));
        await body.Reading.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<TimeoutException>(() => download.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DownloadHasATotalDeadlineEvenWhileBodyMakesProgress()
    {
        using var body = new BlockingBody(makeProgress: true);
        using var client = new HttpClient(new ResponseHandler(() => new StreamContent(body)));
        var path = Path.Combine(_directory, "update.zip");
        long progress = 0;
        await Assert.ThrowsAsync<TimeoutException>(() => GameInformationManager.DownloadPackageAsync(client,
            Release(), path, (count, _) => progress = count, CancellationToken.None,
            TimeSpan.FromMilliseconds(200)).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(progress > 0);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task UserCancellationIsDistinctFromTimeoutAndRemovesPartialFile()
    {
        using var body = new BlockingBody();
        using var client = new HttpClient(new ResponseHandler(() => new StreamContent(body)));
        using var cancellation = new CancellationTokenSource();
        var path = Path.Combine(_directory, "update.zip");
        var download = GameInformationManager.DownloadPackageAsync(client, Release(), path, (_, _) => { },
            cancellation.Token, TimeSpan.FromMinutes(10));
        await body.Reading.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task SuccessfulDownloadRetainsOnlyAHashVerifiedFile()
    {
        var bytes = Encoding.UTF8.GetBytes("update package");
        using var client = new HttpClient(new ResponseHandler(() => new ByteArrayContent(bytes)));
        var path = Path.Combine(_directory, "update.zip");
        var release = Release() with { Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
        await GameInformationManager.DownloadPackageAsync(client, release, path, (_, _) => { },
            CancellationToken.None, TimeSpan.FromSeconds(5));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        await Assert.ThrowsAsync<InvalidDataException>(() => GameInformationManager.DownloadPackageAsync(client,
            Release(), path, (_, _) => { }, CancellationToken.None, TimeSpan.FromSeconds(5)));
        Assert.False(File.Exists(path));
    }

    private static GameRelease Release() => new("release-id", "0.1.1", "linux-x64", "Update",
        "https://source.example/update.zip", new string('a', 64), "published", DateTimeOffset.UtcNow, null);

    private static (ContentRepositoryManager Manager, ContentRepository First, ContentRepository Second) CreateSources()
    {
        var first = new ContentRepository { Name = "First", BaseUrl = "https://first.example" };
        var second = new ContentRepository { Name = "Second", BaseUrl = "https://second.example" };
        return (new ContentRepositoryManager(new ContentRepositorySettings
        {
            GameInformationSourceId = first.Id,
            Repositories = [first, second]
        }, _ => { }), first, second);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private sealed class ResponseHandler(Func<HttpContent> content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content()
            });
    }

    private sealed class BlockingBody(bool makeProgress = false) : Stream
    {
        internal TaskCompletionSource Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Reading.TrySetResult();
            await Task.Delay(makeProgress ? 10 : Timeout.Infinite, cancellationToken);
            buffer.Span[0] = 42;
            return 1;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush()
        {
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
