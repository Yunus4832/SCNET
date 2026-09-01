using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Content.Packaging;

using ContentServer.Infrastructure;

using Game.Modding;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ContentServer.Test;

public sealed class ContentServerApiTest : IDisposable
{
    private const string _administratorKey = "integration-administrator-key";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"content-server-{Guid.NewGuid():N}.db");
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"content-server-files-{Guid.NewGuid():N}");

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
            "/api/v1/administrators/applications",
            new { name = "Second Administrator", contact = "admin2@example.test" }));
        var secondAdministratorId = administratorApplication.GetProperty("administratorId").GetString()!;
        var secondAdministratorKey = administratorApplication.GetProperty("apiKey").GetString()!;
        using var administratorStatusRequest =
            CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/administrator", secondAdministratorKey);
        Assert.Equal("pending",
            (await ReadDataAsync(await client.SendAsync(administratorStatusRequest))).GetProperty("status")
            .GetString());
        using var pendingAdministratorOperation = CreateAuthorizedRequest(
            HttpMethod.Get, "/api/v1/admin/content", secondAdministratorKey);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(pendingAdministratorOperation)).StatusCode);
        using var administratorStatusAfterForbidden = CreateAuthorizedRequest(
            HttpMethod.Get, "/api/v1/administrator", secondAdministratorKey);
        Assert.Equal("pending", (await ReadDataAsync(await client.SendAsync(administratorStatusAfterForbidden)))
            .GetProperty("status").GetString());
        using var approveAdministrator = CreateAuthorizedRequest(HttpMethod.Post,
            $"/api/v1/admin/administrator-applications/{secondAdministratorId}/approve", _administratorKey);
        approveAdministrator.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(approveAdministrator)).StatusCode);
        using var repeatAdministratorApproval = CreateAuthorizedRequest(HttpMethod.Post,
            $"/api/v1/admin/administrator-applications/{secondAdministratorId}/approve", _administratorKey);
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
        var imageSource = CreatePng();

        using (var anonymousImageValidation = new MultipartFormDataContent
               {
                   { new StringContent("CharacterSkin"), "type" },
                   { new ByteArrayContent(imageSource), "source", "source.png" }
               })
        {
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsync("/api/v1/publisher/packages/image/validate-source",
                    anonymousImageValidation)).StatusCode);
        }

        using var pendingRequest = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/publisher", publisherKey);
        using var pendingResponse = await client.SendAsync(pendingRequest);
        Assert.Equal("pending", (await ReadDataAsync(pendingResponse)).GetProperty("status").GetString());
        using (var pendingImageRequest = CreateAuthorizedRequest(HttpMethod.Post,
                   "/api/v1/publisher/packages/image/validate-source", publisherKey))
        {
            pendingImageRequest.Content = new MultipartFormDataContent
            {
                { new StringContent("CharacterSkin"), "type" },
                { new ByteArrayContent(imageSource), "source", "source.png" }
            };
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(pendingImageRequest)).StatusCode);
        }

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
        var firstPackage = CreateModPackage("1.0.0");
        submissionRequest.Content = new MultipartFormDataContent
        {
            { new ByteArrayContent(firstPackage), "package", "integration.scpkg" }
        };
        using var submissionResponse = await client.SendAsync(submissionRequest);
        submissionResponse.EnsureSuccessStatusCode();
        var submission = await ReadDataAsync(submissionResponse);
        var contentId = submission.GetProperty("contentId").GetString()!;
        var versionId = submission.GetProperty("versionId").GetString()!;
        var packageHash = submission.GetProperty("packageHash").GetString()!;

        using var idempotentRequest = CreateAuthorizedRequest(
            HttpMethod.Post, "/api/v1/publisher/submissions", publisherKey);
        idempotentRequest.Content = new MultipartFormDataContent
        {
            { new ByteArrayContent(firstPackage), "package", "same-logical-package.scpkg" }
        };
        var idempotent = await ReadDataAsync(await client.SendAsync(idempotentRequest));
        Assert.Equal(versionId, idempotent.GetProperty("versionId").GetString());

        using var conflictingRequest = CreateAuthorizedRequest(
            HttpMethod.Post, "/api/v1/publisher/submissions", publisherKey);
        conflictingRequest.Content = new MultipartFormDataContent
        {
            { new ByteArrayContent(CreateModPackage("1.0.0", "different")), "package", "conflict.scpkg" }
        };
        using var conflictingResponse = await client.SendAsync(conflictingRequest);
        Assert.Equal(HttpStatusCode.Conflict, conflictingResponse.StatusCode);

        using var invalidRequest = CreateAuthorizedRequest(
            HttpMethod.Post, "/api/v1/publisher/submissions", publisherKey);
        invalidRequest.Content = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3, 4]), "package", "invalid.scpkg" }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(invalidRequest)).StatusCode);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_storagePath, "temp")));

        using var submissionStatusRequest = CreateAuthorizedRequest(
            HttpMethod.Get, $"/api/v1/publisher/submissions/{versionId}", publisherKey);
        var submissionStatus = await ReadDataAsync(await client.SendAsync(submissionStatusRequest));
        Assert.Equal("pending", submissionStatus.GetProperty("status").GetString());

        using var secondSubmissionRequest = CreateAuthorizedRequest(
            HttpMethod.Post, "/api/v1/publisher/submissions", publisherKey);
        secondSubmissionRequest.Content = new MultipartFormDataContent
        {
            { new ByteArrayContent(CreateModPackage("1.1.0")), "package", "integration-1.1.scpkg" }
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
                { new ByteArrayContent(CreateModPackage($"1.{index}.0")), "package", $"integration-1.{index}.scpkg" }
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
        Assert.Equal(firstPackage, await reviewDownloadResponse.Content.ReadAsByteArrayAsync());

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

        var downloadedPackage = await client.GetByteArrayAsync($"/api/v1/packages/{packageHash}");
        Assert.Equal(firstPackage, downloadedPackage);
        using (var downloadedStream = new MemoryStream(downloadedPackage, writable: false))
        {
            var runtimePackage = ModPackage.Read("content-server-download.scpkg", downloadedStream);
            Assert.Equal(packageHash, runtimePackage.PackageHash);
            Assert.Equal("integration.example", runtimePackage.Manifest.Id);
        }

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
        Assert.Equal(firstPackage, await client.GetByteArrayAsync($"/api/v1/packages/{packageHash}"));

        using var disableContent = CreateAuthorizedRequest(
            HttpMethod.Post, $"/api/v1/admin/content/{contentId}/disable", _administratorKey);
        disableContent.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(disableContent)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/packages/{packageHash}")).StatusCode);

        var concurrentPackage = CreateModPackage("2.0.0");

        async Task<JsonElement> SubmitPackageAsync(byte[] bytes, string fileName)
        {
            using var request = CreateAuthorizedRequest(
                HttpMethod.Post, "/api/v1/publisher/submissions", publisherKey);
            request.Content = new MultipartFormDataContent
            {
                { new ByteArrayContent(bytes), "package", fileName }
            };
            return await ReadDataAsync(await client.SendAsync(request));
        }

        var concurrentResults = await Task.WhenAll(
            SubmitPackageAsync(concurrentPackage, "concurrent-a.scpkg"),
            SubmitPackageAsync(concurrentPackage, "concurrent-b.scpkg"));
        Assert.Equal(concurrentResults[0].GetProperty("versionId").GetString(),
            concurrentResults[1].GetProperty("versionId").GetString());

        var failedPackage = CreateModPackage("1.0.0", markerSuffix: "failure", identifier: "failure.example");
        string failedHash;
        using (var failedStream = new MemoryStream(failedPackage, writable: false))
        {
            failedHash = ContentPackageReader.Inspect(failedStream).PackageHash;
        }

        await using (var failureScope = factory.Services.CreateAsyncScope())
        {
            var failureDb = failureScope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
            await failureDb.Database.ExecuteSqlRawAsync("""
                                                        CREATE TRIGGER fail_content_version_insert
                                                        BEFORE INSERT ON ContentVersions
                                                        BEGIN
                                                            SELECT RAISE(FAIL, 'injected transaction failure');
                                                        END;
                                                        """);
        }

        using (var failedRequest = CreateAuthorizedRequest(
                   HttpMethod.Post, "/api/v1/publisher/submissions", publisherKey))
        {
            failedRequest.Content = new MultipartFormDataContent
            {
                { new ByteArrayContent(failedPackage), "package", "failure.scpkg" }
            };
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.SendAsync(failedRequest)).StatusCode);
        }

        await using (var failureScope = factory.Services.CreateAsyncScope())
        {
            var failureDb = failureScope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
            await failureDb.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_content_version_insert");
            Assert.False(await failureDb.PackageBlobs.AnyAsync(item => item.Hash == failedHash));
            Assert.False(await failureDb.Contents.AnyAsync(item => item.Identifier == "failure.example"));
            var failureStore = failureScope.ServiceProvider.GetRequiredService<ContentPackageStore>();
            var referenced = await failureDb.PackageBlobs.Select(item => item.Hash).ToHashSetAsync();
            Assert.Single(failureStore.AuditOrphans(referenced));
            Assert.Equal(1, failureStore.CleanOrphans(referenced));
        }

        using (var validationRequest = CreateAuthorizedRequest(HttpMethod.Post,
                   "/api/v1/publisher/packages/image/validate-source", publisherKey))
        {
            validationRequest.Content = new MultipartFormDataContent
            {
                { new StringContent("CharacterSkin"), "type" },
                { new ByteArrayContent(imageSource), "source", "skin.png" }
            };
            var validation = await ReadDataAsync(await client.SendAsync(validationRequest));
            Assert.Equal(16, validation.GetProperty("width").GetInt32());
            Assert.Equal(16, validation.GetProperty("height").GetInt32());
            Assert.Equal("image/png", validation.GetProperty("mediaType").GetString());
        }

        var imageIdentifier = Guid.NewGuid().ToString();

        MultipartFormDataContent ImageCreationForm() => new()
        {
            { new StringContent("CharacterSkin"), "type" },
            { new StringContent(imageIdentifier), "identifier" },
            { new StringContent("Integration Skin"), "name" },
            { new StringContent("1.0.0"), "version" },
            { new StringContent("Generated in the API integration test"), "description" },
            { new ByteArrayContent(imageSource), "source", "skin.png" }
        };

        byte[] builtImagePackage;
        using (var buildRequest = CreateAuthorizedRequest(HttpMethod.Post,
                   "/api/v1/publisher/packages/image/build", publisherKey))
        {
            buildRequest.Content = ImageCreationForm();
            using var buildResponse = await client.SendAsync(buildRequest);
            buildResponse.EnsureSuccessStatusCode();
            builtImagePackage = await buildResponse.Content.ReadAsByteArrayAsync();
        }

        string builtImageHash;
        using (var stream = new MemoryStream(builtImagePackage, writable: false))
        {
            var inspection = ContentPackageReader.Inspect(stream);
            Assert.Equal(ContentPackageType.CharacterSkin, inspection.Manifest.Type);
            builtImageHash = inspection.PackageHash;
        }

        using (var generatedSubmitRequest = CreateAuthorizedRequest(HttpMethod.Post,
                   "/api/v1/publisher/packages/image/submit", publisherKey))
        {
            generatedSubmitRequest.Content = ImageCreationForm();
            var generated = await ReadDataAsync(await client.SendAsync(generatedSubmitRequest));
            Assert.Equal(builtImageHash, generated.GetProperty("packageHash").GetString());
            Assert.Equal("pending", generated.GetProperty("status").GetString());
        }

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
        Assert.Equal(13, await db.PackageBlobs.CountAsync());
        Assert.Equal(13, await db.ContentVersions.CountAsync());
        var storedPackage = await db.PackageBlobs.SingleAsync(item => item.Hash == packageHash);
        var storedVersion = await db.ContentVersions.SingleAsync(item =>
            item.Id == new ContentServer.Domain.Contents.ContentVersionId(Guid.Parse(versionId)));
        Assert.Equal(storedPackage.Id, storedVersion.PackageBlobId);

        var packageStore = scope.ServiceProvider.GetRequiredService<ContentPackageStore>();
        var orphanPath = Path.Combine(_storagePath, "packages", new string('a', 64) + ".scpkg");
        await File.WriteAllBytesAsync(orphanPath, firstPackage);
        var referencedHashes = await db.PackageBlobs.Select(item => item.Hash).ToHashSetAsync();
        Assert.Contains(orphanPath, packageStore.AuditOrphans(referencedHashes));
        Assert.Equal(1, packageStore.CleanOrphans(referencedHashes));
        Assert.False(File.Exists(orphanPath));
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }

        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, recursive: true);
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
                    ["ContentServer:DatabasePath"] = _databasePath,
                    ["ContentServer:PackageStoragePath"] = _storagePath
                }));
        });
    }

    private static byte[] CreateModPackage(
        string version,
        string? markerSuffix = null,
        string identifier = "integration.example")
    {
        using var metadata = JsonDocument.Parse("""
                                                {"side":"common","entrypoints":{},"dependencies":[]}
                                                """);
        var manifest = new ContentPackageManifest(1, ContentPackageType.Mod, identifier,
            "Integration Mod", version,
            new ContentPackagePayload("scnet.mod-v1", "payload/mod.json", "application/json"),
            metadata.RootElement.Clone());
        var modJson = "{\"formatVersion\":1}"u8.ToArray();
        var marker = System.Text.Encoding.UTF8.GetBytes(version + markerSuffix);
        using var output = new MemoryStream();
        ContentPackageWriter.Write(output, manifest,
        [
            new ContentPackageWriteEntry("payload/mod.json", modJson.Length,
                () => new MemoryStream(modJson, writable: false)),
            new ContentPackageWriteEntry("payload/data/version.txt", marker.Length,
                () => new MemoryStream(marker, writable: false))
        ]);
        return output.ToArray();
    }

    private static byte[] CreatePng()
    {
        using var image = new Image<Rgba32>(16, 16, new Rgba32(32, 96, 160, 255));
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
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
