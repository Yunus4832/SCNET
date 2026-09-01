using System.Text.Json;

using ContentServer;
using ContentServer.Application.Commands;
using ContentServer.Application;
using ContentServer.Infrastructure;
using ContentServer.Middlewares;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ContentServerOptions>(builder.Configuration.GetSection(ContentServerOptions.SectionName));
var allowedOrigins = builder.Configuration
    .GetSection($"{ContentServerOptions.SectionName}:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("ContentWebUI", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "OPTIONS")
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
builder.Services.AddScoped<ContentPackageSubmissionService>();
builder.Services.AddScoped<ApiKeyAuthenticationContext>();
builder.Services.AddScoped<ApiKeyAuthenticationMiddleware>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(object (error) => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "invalid_value"
                : error.ErrorMessage)
            .ToArray();
        return new BadRequestObjectResult(new NetCorePal.Extensions.Dto.ResponseData(
            false,
            "invalid_request",
            StatusCodes.Status400BadRequest,
            errors));
    };
});

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContentServerDbContext>();
    await db.Database.MigrateAsync();
    var packageStore = scope.ServiceProvider.GetRequiredService<ContentPackageStore>();
    packageStore.CleanTemporaryFiles();
    var referencedHashes = await db.PackageBlobs.AsNoTracking().Select(package => package.Hash).ToHashSetAsync();
    var orphanCount = packageStore.AuditOrphans(referencedHashes).Count;
    if (orphanCount > 0)
    {
        app.Logger.LogWarning("Content package storage contains {OrphanCount} orphan package files", orphanCount);
    }
}
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("ContentWebUI");
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();
