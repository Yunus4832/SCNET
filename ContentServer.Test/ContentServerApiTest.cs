using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using ContentServer.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentServer.Test;

public sealed class ContentServerApiTest : IDisposable
{
    private const string _administratorKey = "integration-administrator-key";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"content-server-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task PublisherApprovalContentApprovalAndAnonymousDownloadFormOneFlow()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var initializationStatus = await ReadDataAsync(
            await client.GetAsync("/api/v1/administrators/initialization"));
        Assert.True(initializationStatus.GetProperty("required").GetBoolean());
        Assert.Equal(16, initializationStatus.GetProperty("apiKeyMinimumLength").GetInt32());

        using var invalidInitializationResponse = await client.PostAsJsonAsync(
            "/api/v1/administrators/initialize",
            new
            {
                name = "Integration Administrator",
                apiKey = "contains spaces and cannot be used"
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidInitializationResponse.StatusCode);
        Assert.Equal(
            "invalid_api_key",
            (await ReadJsonAsync(invalidInitializationResponse)).GetProperty("message").GetString());

        using var initializationResponse = await client.PostAsJsonAsync(
            "/api/v1/administrators/initialize",
            new
            {
                name = "Integration Administrator",
                apiKey = _administratorKey
            });
        Assert.Equal(HttpStatusCode.Created, initializationResponse.StatusCode);
        var administrator = await ReadDataAsync(initializationResponse);
        Assert.Equal("Integration Administrator", administrator.GetProperty("name").GetString());
        Assert.Equal("active", administrator.GetProperty("status").GetString());

        initializationStatus = await ReadDataAsync(
            await client.GetAsync("/api/v1/administrators/initialization"));
        Assert.False(initializationStatus.GetProperty("required").GetBoolean());

        using var repeatedInitializationResponse = await client.PostAsJsonAsync(
            "/api/v1/administrators/initialize",
            new
            {
                name = "Other Administrator",
                apiKey = "other-administrator-key"
            });
        Assert.Equal(HttpStatusCode.Conflict, repeatedInitializationResponse.StatusCode);
        Assert.Equal(
            "administrator_already_initialized",
            (await ReadJsonAsync(repeatedInitializationResponse)).GetProperty("message").GetString());

        var administratorApplication = await ReadDataAsync(await client.PostAsJsonAsync(
            "/api/v1/administrators/applications", new { name = "Second Administrator", contact = "admin2@example.test" }));
        var secondAdministratorId = administratorApplication.GetProperty("administratorId").GetString()!;
        var secondAdministratorKey = administratorApplication.GetProperty("apiKey").GetString()!;
        using var administratorStatusRequest = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/administrator", secondAdministratorKey);
        Assert.Equal("pending", (await ReadDataAsync(await client.SendAsync(administratorStatusRequest))).GetProperty("status").GetString());
        using var pendingAdministratorOperation = CreateAuthorizedRequest(
            HttpMethod.Get, "/api/v1/admin/content", secondAdministratorKey);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(pendingAdministratorOperation)).StatusCode);
        using var administratorStatusAfterForbidden = CreateAuthorizedRequest(
            HttpMethod.Get, "/api/v1/administrator", secondAdministratorKey);
        Assert.Equal("pending", (await ReadDataAsync(await client.SendAsync(administratorStatusAfterForbidden)))
            .GetProperty("status").GetString());
        using var approveAdministrator = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/admin/administrator-applications/{secondAdministratorId}/approve", _administratorKey);
        approveAdministrator.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(approveAdministrator)).StatusCode);
        using var repeatAdministratorApproval = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/admin/administrator-applications/{secondAdministratorId}/approve", _administratorKey);
        repeatAdministratorApproval.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(repeatAdministratorApproval)).StatusCode);

        using var applicationResponse = await client.PostAsJsonAsync("/api/v1/publishers", new
        {
            displayName = "Integration Publisher",
            contact = "publisher@example.test"
        });
        applicationResponse.EnsureSuccessStatusCode();
        var application = await ReadDataAsync(applicationResponse);
        var publisherId = application.GetProperty("publisherId").GetString()!;
        var publisherKey = application.GetProperty("apiKey").GetString()!;

        using var pendingRequest = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/publisher", publisherKey);
        using var pendingResponse = await client.SendAsync(pendingRequest);
        Assert.Equal("pending", (await ReadDataAsync(pendingResponse)).GetProperty("status").GetString());

        using var approvePublisher = CreateAuthorizedRequest(
            HttpMethod.Post, $"/api/v1/admin/publishers/{publisherId}/approve", _administratorKey);
        approvePublisher.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(approvePublisher)).StatusCode);
        using var repeatedPublisherApproval = CreateAuthorizedRequest(
            HttpMethod.Post, $"/api/v1/admin/publishers/{publisherId}/approve", _administratorKey);
        repeatedPublisherApproval.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(repeatedPublisherApproval)).StatusCode);

        using var submissionRequest = CreateAuthorizedRequest(
            HttpMethod.Post, "/api/v1/publisher/submissions", publisherKey);
        submissionRequest.Content = new MultipartFormDataContent
        {
            { new StringContent("Mod"), "type" },
            { new StringContent("integration.example"), "identifier" },
            { new StringContent("Integration Mod"), "name" },
            { new StringContent("1.0.0"), "version" },
            { new ByteArrayContent([1, 2, 3, 4]), "package", "integration.scpak" }
        };
        using var submissionResponse = await client.SendAsync(submissionRequest);
        submissionResponse.EnsureSuccessStatusCode();
        var submission = await ReadDataAsync(submissionResponse);
        var contentId = submission.GetProperty("contentId").GetString()!;
        var versionId = submission.GetProperty("versionId").GetString()!;
        var packageHash = submission.GetProperty("packageHash").GetString()!;

        using var submissionStatusRequest = CreateAuthorizedRequest(
            HttpMethod.Get, $"/api/v1/publisher/submissions/{versionId}", publisherKey);
        var submissionStatus = await ReadDataAsync(await client.SendAsync(submissionStatusRequest));
        Assert.Equal("pending", submissionStatus.GetProperty("status").GetString());

        using var secondSubmissionRequest = CreateAuthorizedRequest(
            HttpMethod.Post, "/api/v1/publisher/submissions", publisherKey);
        secondSubmissionRequest.Content = new MultipartFormDataContent
        {
            { new StringContent("Mod"), "type" },
            { new StringContent("integration.example"), "identifier" },
            { new StringContent("Integration Mod"), "name" },
            { new StringContent("1.1.0"), "version" },
            { new ByteArrayContent([5, 6, 7, 8]), "package", "integration-1.1.scpak" }
        };
        using var secondSubmissionResponse = await client.SendAsync(secondSubmissionRequest);
        secondSubmissionResponse.EnsureSuccessStatusCode();
        var secondSubmission = await ReadDataAsync(secondSubmissionResponse);
        Assert.Equal(contentId, secondSubmission.GetProperty("contentId").GetString());
        Assert.NotEqual(versionId, secondSubmission.GetProperty("versionId").GetString());

        for (var index = 2; index <= 10; index++)
        {
            using var additionalSubmissionRequest = CreateAuthorizedRequest(
                HttpMethod.Post, "/api/v1/publisher/submissions", publisherKey);
            additionalSubmissionRequest.Content = new MultipartFormDataContent
            {
                { new StringContent("Mod"), "type" },
                { new StringContent("integration.example"), "identifier" },
                { new StringContent("Integration Mod"), "name" },
                { new StringContent($"1.{index}.0"), "version" },
                { new ByteArrayContent([(byte)index]), "package", $"integration-1.{index}.scpak" }
            };
            using var additionalSubmissionResponse = await client.SendAsync(additionalSubmissionRequest);
            additionalSubmissionResponse.EnsureSuccessStatusCode();
        }

        using var firstPageRequest = CreateAuthorizedRequest(
            HttpMethod.Get, "/api/v1/publisher/submissions", publisherKey);
        using var firstPageResponse = await client.SendAsync(firstPageRequest);
        var firstPage = await ReadDataAsync(firstPageResponse);
        Assert.Equal(1, firstPage.GetProperty("pageIndex").GetInt32());
        Assert.Equal(10, firstPage.GetProperty("pageSize").GetInt32());
        Assert.Equal(11, firstPage.GetProperty("total").GetInt32());
        Assert.Equal(10, firstPage.GetProperty("items").GetArrayLength());

        using var secondPageRequest = CreateAuthorizedRequest(
            HttpMethod.Get, "/api/v1/publisher/submissions?pageIndex=2&pageSize=10", publisherKey);
        using var secondPageResponse = await client.SendAsync(secondPageRequest);
        var secondPage = await ReadDataAsync(secondPageResponse);
        Assert.Equal(1, secondPage.GetProperty("items").GetArrayLength());

        using var administratorPublishersRequest = CreateAuthorizedRequest(
            HttpMethod.Get, "/api/v1/admin/publishers", _administratorKey);
        using var administratorPublishersResponse = await client.SendAsync(administratorPublishersRequest);
        var administratorPublishers = await ReadDataAsync(administratorPublishersResponse);
        Assert.Equal(1, administratorPublishers.GetProperty("total").GetInt32());

        using var administratorSubmissionsRequest = CreateAuthorizedRequest(
            HttpMethod.Get, "/api/v1/admin/submissions?pageIndex=2", _administratorKey);
        using var administratorSubmissionsResponse = await client.SendAsync(administratorSubmissionsRequest);
        var administratorSubmissions = await ReadDataAsync(administratorSubmissionsResponse);
        Assert.Equal(11, administratorSubmissions.GetProperty("total").GetInt32());
        Assert.Equal(1, administratorSubmissions.GetProperty("items").GetArrayLength());

        using var matchingContentRequest = CreateAuthorizedRequest(
            HttpMethod.Get, "/api/v1/admin/content?type=Mod&query=Integration", _administratorKey);
        Assert.Equal(1, (await ReadDataAsync(await client.SendAsync(matchingContentRequest)))
            .GetProperty("total").GetInt32());
        using var excludedContentRequest = CreateAuthorizedRequest(
            HttpMethod.Get, "/api/v1/admin/content?type=World&query=Integration", _administratorKey);
        Assert.Equal(0, (await ReadDataAsync(await client.SendAsync(excludedContentRequest)))
            .GetProperty("total").GetInt32());

        using var unpublishedPackageResponse = await client.GetAsync($"/api/v1/packages/{packageHash}");
        Assert.Equal(HttpStatusCode.NotFound, unpublishedPackageResponse.StatusCode);
        Assert.Equal("package_not_found", (await ReadJsonAsync(unpublishedPackageResponse))
            .GetProperty("message").GetString());

        using var reviewDownloadRequest = CreateAuthorizedRequest(
            HttpMethod.Get,
            $"/api/v1/admin/submissions/{versionId}/package",
            _administratorKey);
        using var reviewDownloadResponse = await client.SendAsync(reviewDownloadRequest);
        reviewDownloadResponse.EnsureSuccessStatusCode();
        Assert.Equal([1, 2, 3, 4], await reviewDownloadResponse.Content.ReadAsByteArrayAsync());

        using var approveContent = CreateAuthorizedRequest(
            HttpMethod.Post, $"/api/v1/admin/submissions/{versionId}/approve", _administratorKey);
        approveContent.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(approveContent)).StatusCode);
        using var repeatedContentApproval = CreateAuthorizedRequest(
            HttpMethod.Post, $"/api/v1/admin/submissions/{versionId}/approve", _administratorKey);
        repeatedContentApproval.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(repeatedContentApproval)).StatusCode);

        var publicContent = await ReadDataAsync(await client.GetAsync("/api/v1/content"));
        Assert.Equal(1, publicContent.GetProperty("total").GetInt32());
        var publicMods = await ReadDataAsync(await client.GetAsync("/api/v1/mods"));
        Assert.Equal(1, publicMods.GetProperty("total").GetInt32());
        var publicModVersions = await ReadDataAsync(
            await client.GetAsync("/api/v1/mods/integration.example"));
        Assert.Equal(1, publicModVersions.GetProperty("total").GetInt32());

        Assert.Equal([1, 2, 3, 4], await client.GetByteArrayAsync($"/api/v1/packages/{packageHash}"));

        using var publisherContentRequest = CreateAuthorizedRequest(
            HttpMethod.Get, "/api/v1/publisher/content", publisherKey);
        var publisherContent = await ReadDataAsync(await client.SendAsync(publisherContentRequest));
        Assert.Equal(1, publisherContent.GetProperty("total").GetInt32());
        Assert.Equal("active", publisherContent.GetProperty("items")[0].GetProperty("status").GetString());

        using var publisherDisableContent = CreateAuthorizedRequest(
            HttpMethod.Post, $"/api/v1/publisher/content/{contentId}/disable", publisherKey);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(publisherDisableContent)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/packages/{packageHash}")).StatusCode);

        using var publisherEnableContent = CreateAuthorizedRequest(
            HttpMethod.Post, $"/api/v1/publisher/content/{contentId}/enable", publisherKey);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(publisherEnableContent)).StatusCode);
        Assert.Equal([1, 2, 3, 4], await client.GetByteArrayAsync($"/api/v1/packages/{packageHash}"));

        using var disableContent = CreateAuthorizedRequest(
            HttpMethod.Post, $"/api/v1/admin/content/{contentId}/disable", _administratorKey);
        disableContent.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(disableContent)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/packages/{packageHash}")).StatusCode);

        using var revokeKey = CreateAuthorizedRequest(
            HttpMethod.Post, $"/api/v1/admin/publishers/{publisherId}/revoke-key", _administratorKey);
        revokeKey.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(revokeKey)).StatusCode);
        using var revokedRequest = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/publisher", publisherKey);
        using var revokedResponse = await client.SendAsync(revokedRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedResponse.StatusCode);
        var unauthorized = await ReadJsonAsync(revokedResponse);
        Assert.False(unauthorized.GetProperty("success").GetBoolean());
        Assert.Equal("unauthorized", unauthorized.GetProperty("message").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
        Assert.Equal(2, await db.AdministratorKeys.CountAsync());
        Assert.Equal(5, await db.ReviewRecords.CountAsync());
        Assert.Equal(11, await db.PackageBlobs.CountAsync());
        Assert.Equal(11, await db.ContentVersions.CountAsync());
        var storedPackage = await db.PackageBlobs.SingleAsync(item => item.Hash == packageHash);
        var storedVersion = await db.ContentVersions.SingleAsync(item => item.Id == new ContentServer.Domain.Contents.ContentVersionId(Guid.Parse(versionId)));
        Assert.Equal(storedPackage.Id, storedVersion.PackageBlobId);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ContentServer:DatabasePath"] = _databasePath
                }));
        });
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string uri, string key)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("data");
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
