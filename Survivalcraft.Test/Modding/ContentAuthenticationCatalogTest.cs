using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

using Game.Content;

namespace Survivalcraft.Test.Modding;

public sealed class ContentAuthenticationCatalogTest
{
    [Theory]
    [InlineData("url")]
    [InlineData("name")]
    [InlineData("review")]
    [InlineData("id")]
    public async Task InvalidSourceMetadataIsRejected(string field)
    {
        var record = Record();
        record = field switch
        {
            "url" => record with { ServiceUrl = "http://auth.example/" },
            "name" => record with { Name = "Authority\nInjected" },
            "review" => record with { ReviewedAt = null },
            _ => record with { Id = "not-a-directory-id" }
        };
        using var http = new HttpClient(new Handler(_ => Response([record])));
        using var client = new ContentServerClient("https://directory.example/", http);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.ListAuthenticationServicesAsync());
    }

    [Fact]
    public async Task AllBoundedPagesAreReadWithoutSendingAnyPlayerCredentials()
    {
        var records = Enumerable.Range(0, 101).Select(_ => Record()).ToArray();
        var calls = 0;
        using var http = new HttpClient(
            new Handler(request =>
                {
                    calls++;
                    Assert.Null(request.Headers.Authorization);
                    Assert.Null(request.Content);
                    Assert.False(request.Headers.Contains("Cookie"));
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal("/api/v1/authentication-services", request.RequestUri!.AbsolutePath);
                    return request.RequestUri.Query.Contains("pageIndex=1&", StringComparison.Ordinal)
                        ? Response(records[..100], total: 101)
                        : Response(records[100..], pageIndex: 2, total: 101);
                }
            )
        );
        using var client = new ContentServerClient("https://directory.example/", http);
        var result = await client.ListAuthenticationServicesAsync();
        Assert.Equal(101, result.Count);
        Assert.Equal(2, calls);
        Assert.Equal(records[^1].ServiceUrl, result[^1].Validate().AbsoluteUri);
    }

    [Theory]
    [InlineData("duplicate-id")]
    [InlineData("duplicate-source")]
    [InlineData("partial")]
    [InlineData("oversize")]
    [InlineData("wrong-page")]
    public async Task InvalidPaginationAndDuplicateSourcesNeverYieldAPartialCatalog(string fault)
    {
        var record = Record();
        using var http = new HttpClient(
            new Handler(_ => fault switch
                {
                    "duplicate-id" => Response([record, Record() with { Id = record.Id }]),
                    "duplicate-source" => Response([record, record with { Id = Guid.NewGuid().ToString("D") }]),
                    "partial" => Response([record], total: 101),
                    "oversize" => Response([record], total: 10001),
                    _ => Response([record], pageIndex: 2)
                }
            )
        );
        using var client = new ContentServerClient("https://directory.example/", http);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.ListAuthenticationServicesAsync());
    }

    [Fact]
    public async Task RepositoryFailuresAreSeparateFromEmptyResultsAndSharedUrlsRemainIndependentDirectorySources()
    {
        var record = Record();
        var good = new ContentRepository { Name = "Good", BaseUrl = "https://good.example/" };
        var mirror = new ContentRepository { Name = "Mirror", BaseUrl = "https://mirror.example/" };
        var failed = new ContentRepository { Name = "Failed", BaseUrl = "https://failed.example/" };
        var empty = new ContentRepository { Name = "Empty", BaseUrl = "https://empty.example/" };
        var disabled = new ContentRepository
            { Name = "Disabled", BaseUrl = "https://disabled.example/", IsEnabled = false };
        var factory = new Factory(record);
        using var pool = new ContentServerClientPool(factory);
        var result = await new ContentAuthenticationCatalog(pool).QueryAsync(
            [good, mirror, failed, empty, disabled],
            CancellationToken.None
        );
        Assert.Equal(2, result.Services.Count);
        Assert.All(result.Services, item => Assert.Equal(record.ServiceUrl, item.ServiceUrl));
        Assert.Equal(2, result.Services.Select(item => item.RepositoryId).Distinct().Count());
        Assert.Equal(failed.Id, Assert.Single(result.UnavailableRepositories));
        Assert.DoesNotContain(disabled.Id, factory.Created);
        Assert.Equal(4, factory.Created.Count);
    }

    [Fact]
    public async Task CancellationDoesNotBecomeAFailedRepositoryOrCreateAClient()
    {
        var factory = new Factory(Record());
        using var pool = new ContentServerClientPool(factory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ContentAuthenticationCatalog(pool).QueryAsync(
                [new ContentRepository { Name = "Good", BaseUrl = "https://good.example/" }],
                cancellation.Token
            )
        );
        Assert.Empty(factory.Created);
    }

    private static ContentServerAuthenticationService Record()
    {
        return new ContentServerAuthenticationService(
            Guid.NewGuid().ToString("D"),
            "Authority",
            "https://auth.example/" + Guid.NewGuid().ToString("N") + "/",
            "Public directory entry",
            DateTimeOffset.UtcNow
        );
    }

    private static HttpResponseMessage Response(
        IReadOnlyList<ContentServerAuthenticationService> items,
        int pageIndex = 1,
        int? total = null
    )
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new { success = true, data = new { items, total = total ?? items.Count, pageIndex, pageSize = 100 } }
            )
        };
    }

    private sealed class Factory(ContentServerAuthenticationService record) : ContentServerClientFactory
    {
        public ConcurrentBag<Guid> Created { get; } = [];

        public override ContentServerClient Create(ContentRepository repository)
        {
            Created.Add(repository.Id);
            return new ContentServerClient(
                repository.BaseUrl,
                new HttpClient(
                    new Handler(request => request.RequestUri!.Host switch
                        {
                            "failed.example" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                            "empty.example" => Response([]),
                            _ => Response([record])
                        }
                    )
                ),
                true
            );
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
