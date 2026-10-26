using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using ContentServer.Application;
using ContentServer.Application.Commands;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Infrastructure;

using MediatR;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;

namespace ContentServer.Test;

public sealed class AuthenticationDirectoryApiTest : IDisposable
{
    private const string _administratorKey = "authentication-directory-test-administrator";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "scnet-auth-api-" + Guid.NewGuid().ToString("N")
    );

    private readonly Dictionary<string, AuthenticationServiceDescription> _descriptors = [];

    private Func<Task>? _beforeProbe;

    private int _probeCalls;

    private bool _unavailable;

    [Fact]
    public async Task PublishingEditingReviewWithdrawalAndDeletionHaveIndependentAuditedBoundaries()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client);
        var owner = await PublisherAsync(client);
        var other = await PublisherAsync(client);
        var endpoint = RegisterEndpoint();
        await ExpectAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            null,
            HttpStatusCode.Unauthorized,
            Submission(endpoint)
        );
        Assert.Equal(0, _probeCalls);
        var submitted = await SendDataAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            owner.Key,
            Submission(endpoint)
        );
        var id = submitted.GetProperty("id").GetString()!;
        Assert.Equal("pending", submitted.GetProperty("status").GetString());
        Assert.Equal(1, submitted.GetProperty("revision").GetInt64());
        Assert.False(submitted.TryGetProperty("identity", out _));
        Assert.False(submitted.TryGetProperty("publicKey", out _));
        Assert.Equal(0, (await PublicAsync(client)).GetProperty("total").GetInt32());
        var calls = _probeCalls;
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/publisher/authentication-services/{id}/edit",
            other.Key,
            HttpStatusCode.NotFound,
            Submission(endpoint, 1)
        );
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/publisher/authentication-services/{id}/withdraw",
            other.Key,
            HttpStatusCode.NotFound,
            new { revision = 1 }
        );
        await ExpectAsync(
            client,
            HttpMethod.Delete,
            $"/api/v1/publisher/authentication-services/{id}?revision=1",
            other.Key,
            HttpStatusCode.NotFound
        );
        Assert.Equal(calls, _probeCalls);
        var ownerList = await SendDataAsync(
            client,
            HttpMethod.Get,
            "/api/v1/publisher/authentication-services?pageSize=1",
            owner.Key
        );
        Assert.Equal(id, Assert.Single(ownerList.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        var otherList = await SendDataAsync(
            client,
            HttpMethod.Get,
            "/api/v1/publisher/authentication-services",
            other.Key
        );
        Assert.Empty(otherList.GetProperty("items").EnumerateArray());
        var pending = await SendDataAsync(
            client,
            HttpMethod.Get,
            "/api/v1/admin/authentication-services?status=Pending",
            _administratorKey
        );
        Assert.Single(pending.GetProperty("items").EnumerateArray());
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/approve",
            owner.Key,
            HttpStatusCode.Unauthorized,
            new { revision = 1 }
        );
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/approve",
            _administratorKey,
            HttpStatusCode.OK,
            new { revision = 1, message = "Reviewed original identity" }
        );
        var published = Assert.Single((await PublicAsync(client)).GetProperty("items").EnumerateArray());
        Assert.False(published.TryGetProperty("publicKey", out _));
        Assert.False(published.TryGetProperty("identity", out _));
        Assert.Equal(endpoint.BaseUri.AbsoluteUri, published.GetProperty("serviceUrl").GetString());
        Assert.False(published.TryGetProperty("reviewMessage", out _));
        Assert.False(published.TryGetProperty("publisherId", out _));

        var replacement = RegisterEndpoint("https://replacement.example/");
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/publisher/authentication-services/{id}/edit",
            owner.Key,
            HttpStatusCode.OK,
            Submission(replacement, 2)
        );
        Assert.Empty((await PublicAsync(client)).GetProperty("items").EnumerateArray());
        calls = _probeCalls;
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/approve",
            _administratorKey,
            HttpStatusCode.Conflict,
            new { revision = 2 }
        );
        Assert.Equal(calls, _probeCalls);
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/approve",
            _administratorKey,
            HttpStatusCode.OK,
            new { revision = 3 }
        );
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/disable",
            _administratorKey,
            HttpStatusCode.OK,
            new { revision = 4, reason = "Operator requested maintenance" }
        );
        Assert.Empty((await PublicAsync(client)).GetProperty("items").EnumerateArray());
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/publisher/authentication-services/{id}/edit",
            owner.Key,
            HttpStatusCode.OK,
            Submission(replacement, 5)
        );
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/reject",
            _administratorKey,
            HttpStatusCode.OK,
            new { revision = 6, message = "Description requires correction" }
        );
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/publisher/authentication-services/{id}/withdraw",
            owner.Key,
            HttpStatusCode.OK,
            new { revision = 7 }
        );
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/publisher/authentication-services/{id}/edit",
            owner.Key,
            HttpStatusCode.OK,
            Submission(replacement, 8)
        );
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/approve",
            _administratorKey,
            HttpStatusCode.OK,
            new { revision = 9 }
        );
        await ExpectAsync(
            client,
            HttpMethod.Delete,
            $"/api/v1/publisher/authentication-services/{id}?revision=10",
            owner.Key,
            HttpStatusCode.OK
        );
        Assert.Empty((await PublicAsync(client)).GetProperty("items").EnumerateArray());
        var audit = await SendDataAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/authentication-services/{id}/audit?pageSize=100",
            _administratorKey
        );
        Assert.Equal(11, audit.GetProperty("total").GetInt32());
        var entries = audit.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("Deleted", entries[0].GetProperty("action").GetString());
        Assert.Equal(owner.Id, entries[0].GetProperty("publisherId").GetString());
        Assert.Equal(endpoint.BaseUri.AbsoluteUri, entries[^1].GetProperty("serviceUrl").GetString());
        Assert.Equal(replacement.BaseUri.AbsoluteUri, entries[0].GetProperty("serviceUrl").GetString());
        Assert.Contains(
            entries,
            entry => entry.GetProperty("message").GetString() == "Description requires correction"
        );
        await ExpectAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/authentication-services/{id}/audit",
            owner.Key,
            HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task AuthorityKeyChangesDoNotBecomeDirectoryIdentityDecisions()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client);
        var owner = await PublisherAsync(client);
        var endpoint = RegisterEndpoint();
        var submitted = await SendDataAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            owner.Key,
            Submission(endpoint)
        );
        var id = submitted.GetProperty("id").GetString()!;
        _descriptors[endpoint.GetUri(AuthenticationProtocol.DiscoveryPath).AbsoluteUri] =
            AuthenticationServiceRegistrationTest.Endpoint().Description with
            {
                ServiceUrl = endpoint.BaseUri.AbsoluteUri
            };
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/approve",
            _administratorKey,
            HttpStatusCode.OK,
            new { revision = 1 }
        );
        var published = Assert.Single((await PublicAsync(client)).GetProperty("items").EnumerateArray());
        Assert.Equal(endpoint.BaseUri.AbsoluteUri, published.GetProperty("serviceUrl").GetString());
        Assert.False(published.TryGetProperty("publicKey", out _));
        Assert.False(published.TryGetProperty("identity", out _));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
        var registration = db.Model.FindEntityType(typeof(AuthenticationServiceRegistration))!;
        Assert.Null(registration.FindProperty("PublicKey"));
        Assert.Null(registration.FindProperty("Identity"));
    }

    [Fact]
    public async Task InvalidUnavailableAndConflictingSubmissionsNeverCreateAnAlternativeTrustedListing()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client);
        var owner = await PublisherAsync(client);
        var pending = await PublisherAsync(client, active: false);
        var endpoint = RegisterEndpoint();
        await ExpectAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            pending.Key,
            HttpStatusCode.Forbidden,
            Submission(endpoint)
        );
        await ExpectAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            owner.Key,
            HttpStatusCode.BadRequest,
            new { name = "Authority", serviceUrl = "http://localhost/" }
        );
        await ExpectAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            owner.Key,
            HttpStatusCode.BadRequest,
            new { name = "", serviceUrl = endpoint.BaseUri.AbsoluteUri }
        );
        Assert.Equal(0, _probeCalls);
        _unavailable = true;
        await ExpectAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            owner.Key,
            HttpStatusCode.UnprocessableEntity,
            Submission(endpoint)
        );
        _unavailable = false;
        var wrong = AuthenticationServiceRegistrationTest.Endpoint();
        _descriptors[endpoint.GetUri(AuthenticationProtocol.DiscoveryPath).AbsoluteUri] =
            wrong.Description with { ServiceUrl = "https://redirected.example/" };
        await ExpectAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            owner.Key,
            HttpStatusCode.UnprocessableEntity,
            Submission(endpoint)
        );
        _descriptors[endpoint.GetUri(AuthenticationProtocol.DiscoveryPath).AbsoluteUri] = endpoint.Description;
        var submitted = await SendDataAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            owner.Key,
            Submission(endpoint)
        );
        var id = submitted.GetProperty("id").GetString();
        await ExpectAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            owner.Key,
            HttpStatusCode.Conflict,
            Submission(endpoint)
        );
        _unavailable = true;
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/approve",
            _administratorKey,
            HttpStatusCode.UnprocessableEntity,
            new { revision = 1 }
        );
        _unavailable = false;
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/approve",
            _administratorKey,
            HttpStatusCode.BadRequest,
            new { revision = 0 }
        );
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/reject",
            _administratorKey,
            HttpStatusCode.BadRequest,
            new { revision = 1, message = new string('x', 1001) }
        );
        await ExpectAsync(
            client,
            HttpMethod.Get,
            "/api/v1/authentication-services?pageIndex=101",
            null,
            HttpStatusCode.BadRequest
        );
        await ExpectAsync(
            client,
            HttpMethod.Get,
            "/api/v1/admin/authentication-services?status=99",
            _administratorKey,
            HttpStatusCode.BadRequest
        );
        await using var scope = factory.Services.CreateAsyncScope();
        var listing = await scope.ServiceProvider.GetRequiredService<ContentServerDbContext>().AuthenticationServices
            .SingleAsync();
        Assert.Equal(AuthenticationServiceRegistrationStatus.Pending, listing.Status);
        Assert.Equal(1, listing.Revision);
    }

    [Fact]
    public async Task PublicPagesAreStableBoundedAndContainOnlyApprovedListings()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client);
        var owner = await PublisherAsync(client);
        foreach (var url in new[] { "https://first.example/", "https://second.example/", "https://pending.example/" })
        {
            var submitted = await SendDataAsync(
                client,
                HttpMethod.Post,
                "/api/v1/publisher/authentication-services",
                owner.Key,
                Submission(RegisterEndpoint(url))
            );
            if (url.Contains("pending", StringComparison.Ordinal)) { continue; }

            var id = submitted.GetProperty("id").GetString();
            await ExpectAsync(
                client,
                HttpMethod.Post,
                $"/api/v1/admin/authentication-services/{id}/approve",
                _administratorKey,
                HttpStatusCode.OK,
                new { revision = 1 }
            );
        }

        var first = await SendDataAsync(client, HttpMethod.Get, "/api/v1/authentication-services?pageSize=1", null);
        var second = await SendDataAsync(
            client,
            HttpMethod.Get,
            "/api/v1/authentication-services?pageIndex=2&pageSize=1",
            null
        );
        Assert.Equal(2, first.GetProperty("total").GetInt32());
        Assert.Equal(2, second.GetProperty("total").GetInt32());
        var firstId = Assert.Single(first.GetProperty("items").EnumerateArray()).GetProperty("id").GetString();
        var secondId = Assert.Single(second.GetProperty("items").EnumerateArray()).GetProperty("id").GetString();
        Assert.NotEqual(firstId, secondId);
        var repeated = await SendDataAsync(client, HttpMethod.Get, "/api/v1/authentication-services?pageSize=1", null);
        Assert.Equal(
            firstId,
            Assert.Single(repeated.GetProperty("items").EnumerateArray()).GetProperty("id").GetString()
        );
        var capped = await SendDataAsync(client, HttpMethod.Get, "/api/v1/authentication-services?pageSize=1000", null);
        Assert.Equal(100, capped.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, capped.GetProperty("items").GetArrayLength());
        await ExpectAsync(
            client,
            HttpMethod.Get,
            "/api/v1/admin/authentication-services?status=unknown",
            _administratorKey,
            HttpStatusCode.BadRequest
        );
    }

    [Fact]
    public async Task EditDuringApprovalProbeCannotApproveTheChangedSource()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client);
        var owner = await PublisherAsync(client);
        var endpoint = RegisterEndpoint();
        var submitted = await SendDataAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            owner.Key,
            Submission(endpoint)
        );
        var id = submitted.GetProperty("id").GetString()!;
        var replacement = RegisterEndpoint("https://changed-during-review.example/");
        _beforeProbe = async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.Equal(
                AuthenticationServiceMutationResult.Completed,
                await mediator.Send(
                    new EditAuthenticationServiceCommand(
                        new AuthenticationServiceRegistrationId(Guid.Parse(id)),
                        new ContentServer.Domain.Publishers.PublisherId(Guid.Parse(owner.Id)),
                        1,
                        "Changed",
                        replacement.BaseUri,
                        null,
                        DateTimeOffset.UtcNow
                    )
                )
            );
        };
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/approve",
            _administratorKey,
            HttpStatusCode.Conflict,
            new { revision = 1 }
        );
        Assert.Empty((await PublicAsync(client)).GetProperty("items").EnumerateArray());
        var managed = await SendDataAsync(
            client,
            HttpMethod.Get,
            "/api/v1/admin/authentication-services",
            _administratorKey
        );
        var listing = Assert.Single(managed.GetProperty("items").EnumerateArray());
        Assert.Equal(2, listing.GetProperty("revision").GetInt64());
        Assert.Equal("pending", listing.GetProperty("status").GetString());
        Assert.Equal(replacement.BaseUri.AbsoluteUri, listing.GetProperty("serviceUrl").GetString());
    }

    [Fact]
    public async Task AdministratorDeletionRetainsHistoryAndSuspendedPublisherIsNotPublic()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await InitializeAsync(client);
        var owner = await PublisherAsync(client);
        var endpoint = RegisterEndpoint();
        var submitted = await SendDataAsync(
            client,
            HttpMethod.Post,
            "/api/v1/publisher/authentication-services",
            owner.Key,
            Submission(endpoint)
        );
        var id = submitted.GetProperty("id").GetString();
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/authentication-services/{id}/approve",
            _administratorKey,
            HttpStatusCode.OK,
            new { revision = 1 }
        );
        Assert.Single((await PublicAsync(client)).GetProperty("items").EnumerateArray());
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/admin/publishers/{owner.Id}/suspend",
            _administratorKey,
            HttpStatusCode.OK,
            new { message = "Suspended publisher" }
        );
        Assert.Empty((await PublicAsync(client)).GetProperty("items").EnumerateArray());
        await ExpectAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/publisher/authentication-services/{id}/edit",
            owner.Key,
            HttpStatusCode.Forbidden,
            Submission(endpoint, 2)
        );
        await ExpectAsync(
            client,
            HttpMethod.Delete,
            $"/api/v1/admin/authentication-services/{id}?revision=1",
            _administratorKey,
            HttpStatusCode.Conflict
        );
        await ExpectAsync(
            client,
            HttpMethod.Delete,
            $"/api/v1/admin/authentication-services/{id}?revision=2",
            _administratorKey,
            HttpStatusCode.OK
        );
        var audit = await SendDataAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/authentication-services/{id}/audit",
            _administratorKey
        );
        Assert.Equal(3, audit.GetProperty("total").GetInt32());
        var deleted = audit.GetProperty("items")[0];
        Assert.Equal("Deleted", deleted.GetProperty("action").GetString());
        Assert.NotEqual(JsonValueKind.Null, deleted.GetProperty("administratorId").ValueKind);
    }

    private AuthenticationServiceEndpoint RegisterEndpoint(string url = "https://authority.example/")
    {
        var endpoint = AuthenticationServiceRegistrationTest.Endpoint(url);
        _descriptors[endpoint.GetUri(AuthenticationProtocol.DiscoveryPath).AbsoluteUri] = endpoint.Description;
        return endpoint;
    }

    private static object Submission(AuthenticationServiceEndpoint endpoint, long revision = 0)
    {
        return new
        {
            name = "Community Authority", serviceUrl = endpoint.BaseUri.AbsoluteUri,
            description = "Community authentication", revision
        };
    }

    private static async Task InitializeAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/administrators/initialize",
            new { name = "Administrator", apiKey = _administratorKey }
        );
        response.EnsureSuccessStatusCode();
    }

    private static async Task<(string Id, string Key)> PublisherAsync(HttpClient client, bool active = true)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/publishers",
            new { displayName = "Directory Publisher", contact = "publisher@example.test" }
        );
        response.EnsureSuccessStatusCode();
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var id = data.GetProperty("publisherId").GetString()!;
        var key = data.GetProperty("apiKey").GetString()!;
        if (active)
        {
            await ExpectAsync(
                client,
                HttpMethod.Post,
                $"/api/v1/admin/publishers/{id}/approve",
                _administratorKey,
                HttpStatusCode.OK,
                new { }
            );
        }

        return (id, key);
    }

    private static Task<JsonElement> PublicAsync(HttpClient client)
    {
        return SendDataAsync(client, HttpMethod.Get, "/api/v1/authentication-services", null);
    }

    private static async Task<JsonElement> SendDataAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? key,
        object? body = null
    )
    {
        using var request = Request(method, path, key, body);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private static async Task ExpectAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? key,
        HttpStatusCode status,
        object? body = null
    )
    {
        using var request = Request(method, path, key, body);
        using var response = await client.SendAsync(request);
        Assert.Equal(status, response.StatusCode);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string? key, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (key is not null) { request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key); }

        if (body is not null) { request.Content = JsonContent.Create(body); }

        return request;
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ContentServer:DatabasePath"] = Path.Combine(_directory, "api.db"),
                            ["ContentServer:PackageStoragePath"] = Path.Combine(_directory, "packages")
                        }
                    )
                );
                builder.ConfigureServices(services =>
                    services.AddSingleton(new AuthenticationInspectionClient(() => new DescriptorHandler(this)))
                );
            }
        );
    }

    private sealed class DescriptorHandler(AuthenticationDirectoryApiTest owner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            owner._probeCalls++;
            var before = Interlocked.Exchange(ref owner._beforeProbe, null);
            if (before is not null) { await before(); }

            if (owner._unavailable) { return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); }

            return owner._descriptors.TryGetValue(request.RequestUri!.AbsoluteUri, out var description)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(description) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) { Directory.Delete(_directory, true); }
    }
}
