using System.Text.Json;

using Microsoft.Extensions.Options;

using ModServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ModServerOptions>(builder.Configuration.GetSection(ModServerOptions.SectionName));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});
builder.Services.AddSingleton<ModRepositoryStore>();

var app = builder.Build();

app.MapGet("/api/v1/health", () => Results.Ok(new
{
    name = "ModServer",
    version = "v1"
}));

app.MapGet("/api/v1/mods",
    async (HttpContext httpContext, ModRepositoryStore store, CancellationToken cancellationToken) =>
    {
        var records = await store.ListAllAsync(cancellationToken);
        return Results.Ok(new
        {
            items = records
                .OrderBy(record => record.ModId, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(record => record.UploadedAtUtc)
                .Select(record => ToResponse(httpContext, record))
                .ToArray()
        });
    });

app.MapGet("/api/v1/mods/{modId}", async Task<IResult> (
    HttpContext httpContext,
    string modId,
    ModRepositoryStore store,
    CancellationToken cancellationToken) =>
{
    var records = await store.ListByModIdAsync(modId, cancellationToken);
    if (records.Count == 0)
    {
        return Results.NotFound(new { message = $"Mod '{modId}' was not found." });
    }

    return Results.Ok(new
    {
        modId,
        items = records
            .OrderByDescending(record => record.UploadedAtUtc)
            .Select(record => ToResponse(httpContext, record))
            .ToArray()
    });
});

app.MapGet("/api/v1/mods/{modId}/versions/{version}", async Task<IResult> (
    HttpContext httpContext,
    string modId,
    string version,
    ModRepositoryStore store,
    CancellationToken cancellationToken) =>
{
    var record = await store.FindByVersionAsync(modId, version, cancellationToken);
    return record is null
        ? Results.NotFound(new { message = $"Mod '{modId}' version '{version}' was not found." })
        : Results.Ok(ToResponse(httpContext, record));
});

app.MapGet("/api/v1/packages/{packageHash}", async Task<IResult> (
    string packageHash,
    ModRepositoryStore store,
    CancellationToken cancellationToken) =>
{
    var record = await store.FindByHashAsync(packageHash, cancellationToken);
    if (record is null)
    {
        return Results.NotFound(new { message = $"Package '{packageHash}' was not found." });
    }

    var stream = await store.OpenPackageAsync(record, cancellationToken);
    return Results.File(
        fileStream: stream,
        contentType: "application/octet-stream",
        fileDownloadName: record.FileName,
        enableRangeProcessing: true
    );
});

app.MapPost("/api/v1/mods/upload", async Task<IResult> (
    HttpContext httpContext,
    IOptions<ModServerOptions> options,
    ModRepositoryStore store,
    CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(httpContext.Request, options.Value))
    {
        return Results.Unauthorized();
    }

    if (!httpContext.Request.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Expected multipart/form-data." });
    }

    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("package");
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "Form file 'package' is required." });
    }

    var description = NormalizeOptional(form["description"].ToString());
    var replace = ParseBoolean(httpContext.Request.Query["replace"].ToString()) ||
                  ParseBoolean(form["replace"].ToString());

    await using var stream = file.OpenReadStream();
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer, cancellationToken);
    var content = buffer.ToArray();
    UploadedModPackage package;
    try
    {
        package = UploadedModPackage.Read(
            string.IsNullOrWhiteSpace(file.FileName) ? "package.scpak" : file.FileName,
            content);
    }
    catch (Exception exception)
    {
        return Results.BadRequest(new { message = exception.Message });
    }

    var record = new ModPackageRecord(
        ModId: package.ModId,
        Version: package.Version,
        PackageHash: package.PackageHash,
        FileName: package.FileName,
        PackageSize: package.PackageSize,
        Side: package.Side,
        Description: description,
        UploadedAtUtc: DateTimeOffset.UtcNow);

    var result = await store.SavePackageAsync(record, content, replace, cancellationToken);
    if (result.Status == SavePackageStatus.Conflict)
    {
        return Results.Conflict(new
        {
            message =
                $"Mod '{package.ModId}' version '{package.Version}' already exists with a different package hash. Re-upload with replace=true to overwrite it.",
            existing = ToResponse(httpContext, result.Record!)
        });
    }

    var response = ToResponse(httpContext, result.Record!);
    return result.Status == SavePackageStatus.Created
        ? Results.Created($"/api/v1/mods/{package.ModId}/versions/{package.Version}", response)
        : Results.Ok(response);
});

app.MapDelete("/api/v1/mods/{modId}/versions/{version}", async Task<IResult> (
    HttpContext httpContext,
    string modId,
    string version,
    IOptions<ModServerOptions> options,
    ModRepositoryStore store,
    CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(httpContext.Request, options.Value))
    {
        return Results.Unauthorized();
    }

    var deleted = await store.DeletePackageAsync(modId, version, cancellationToken);
    return deleted == null
        ? Results.NotFound(new { message = $"Mod '{modId}' version '{version}' was not found." })
        : Results.NoContent();
});

app.Run();

static bool IsAuthorized(HttpRequest request, ModServerOptions options)
{
    if (options.UploadApiKeys.Count == 0)
    {
        return false;
    }

    var bearer = request.Headers.Authorization.ToString();
    if (bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
        options.UploadApiKeys.Contains(bearer["Bearer ".Length..], StringComparer.Ordinal))
    {
        return true;
    }

    var apiKey = request.Headers["X-Api-Key"].ToString();
    return !string.IsNullOrWhiteSpace(apiKey) &&
           options.UploadApiKeys.Contains(apiKey, StringComparer.Ordinal);
}

static object ToResponse(HttpContext httpContext, ModPackageRecord record)
{
    return new
    {
        modId = record.ModId,
        version = record.Version,
        packageHash = record.PackageHash,
        fileName = record.FileName,
        packageSize = record.PackageSize,
        side = record.Side,
        description = record.Description,
        uploadedAtUtc = record.UploadedAtUtc,
        downloadUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/v1/packages/{record.PackageHash}"
    };
}

static string? NormalizeOptional(string value)
{
    var normalized = value.Trim();
    return normalized.Length == 0 ? null : normalized;
}

static bool ParseBoolean(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "1" => true,
        "true" => true,
        "yes" => true,
        "on" => true,
        _ => false
    };
}
