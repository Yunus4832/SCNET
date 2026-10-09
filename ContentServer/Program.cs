using System.Text.Json;

using ContentServer;
using ContentServer.Application;
using ContentServer.Application.Commands;
using ContentServer.Infrastructure;
using ContentServer.Middlewares;

using FastEndpoints;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.DependencyInjection;

using ServerSource.Protocol;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOptions<ContentServerOptions>()
    .Bind(builder.Configuration.GetSection(ContentServerOptions.SectionName))
    .Validate(options => !options.BuiltInServerDirectoryEnabled || ServerSourceValidator.Validate(
        new ServerSourcePage(ServerSourceProtocol.CurrentVersion,
            new ServerSourceDescriptor(options.BuiltInServerDirectoryId, options.BuiltInServerDirectoryName), [],
            null)).IsValid, "Built-in server directory identity is invalid.")
    .Validate(options => string.IsNullOrWhiteSpace(options.PublicBaseUrl) ||
                         Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri) &&
                         uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo) &&
                         string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment),
        "ContentServer PublicBaseUrl must be an absolute HTTP URL without credentials, query, or fragment.")
    .ValidateOnStart();
var allowedOrigins = builder.Configuration
    .GetSection($"{ContentServerOptions.SectionName}:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("ContentWebUI", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "DELETE", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type")
            .WithExposedHeaders("Content-Disposition");
    }
}));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddDbContext<ContentServerDbContext>((services, options) =>
{
    var configuredPath = services.GetRequiredService<IOptions<ContentServerOptions>>().Value.DatabasePath;
    var contentRoot = services.GetRequiredService<IHostEnvironment>().ContentRootPath;
    var databasePath = Path.GetFullPath(configuredPath, contentRoot);
    Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
    options.UseSqlite($"Data Source={databasePath}");
});
builder.Services.AddMediatR(configuration => configuration
    .RegisterServicesFromAssemblyContaining<ApplyPublisherCommand>()
    .AddUnitOfWorkBehaviors());
builder.Services.AddUnitOfWork<ContentServerDbContext>();
builder.Services.AddRepositories(typeof(ContentServerDbContext).Assembly);
builder.Services.AddSingleton<ContentPackageStore>();
builder.Services.AddSingleton<ContentSubmissionLock>();
builder.Services.AddSingleton<ImageContentPackageBuilder>();
builder.Services.AddSingleton<IServerSourceInspectionService, ServerSourceInspectionService>();
builder.Services.AddScoped<ApiKeyAuthenticationContext>();
builder.Services.AddScoped<ApiKeyAuthenticationMiddleware>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddFastEndpoints();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
    await db.Database.MigrateAsync();
    var packageStore = scope.ServiceProvider.GetRequiredService<ContentPackageStore>();
    packageStore.CleanTemporaryFiles();
    var referencedHashes = await db.PackageBlobs.AsNoTracking().Select(package => package.Hash).ToHashSetAsync();
    var orphanCount = packageStore.CleanOrphans(referencedHashes);
    if (orphanCount > 0)
    {
        app.Logger.LogInformation("Removed {OrphanCount} unreferenced package files", orphanCount);
    }
}

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("ContentWebUI");
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseFastEndpoints();
app.MapFallbackToFile("index.html");
app.Run();
