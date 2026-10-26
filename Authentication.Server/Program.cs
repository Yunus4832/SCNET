using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Server.Endpoints;
using SCNET.Authentication.Server.Infrastructure;

if (args is ["--hash-password"])
{
    var password = Console.ReadLine() ?? throw new InvalidOperationException("Read password from standard input.");
    if (password.Length < 12) { throw new ArgumentException("Use at least twelve password characters."); }

    Console.WriteLine(new PasswordHasher<object>().HashPassword(new object(), password));
    return;
}

var managementAction = args.Length >= 2 && args[0] is "--ban-account" or "--unban-account" ? args[0] : null;
var managementAccount = managementAction is not null ? args[1] : null;
var builder = WebApplication.CreateBuilder(managementAction is not null ? args[2..] : args);
var options = builder.Configuration.GetSection("Authentication").Get<ProviderOptions>() ?? new ProviderOptions();
builder.WebHost.ConfigureKestrel(server => server.Limits.MaxRequestBodySize = 32768);
builder.Services.Configure<ForwardedHeadersOptions>(forwarding =>
    {
        forwarding.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        forwarding.ForwardLimit = 1;
        forwarding.KnownIPNetworks.Clear();
        forwarding.KnownProxies.Clear();
        if (options.TrustedProxies is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "Configure at least one explicit trusted proxy or keep the loopback defaults."
            );
        }

        foreach (var proxy in options.TrustedProxies) { forwarding.KnownProxies.Add(IPAddress.Parse(proxy)); }
    }
);
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase, false)
    )
);
if (managementAction is not null)
{
    using var runtime = new ProviderRuntime(options);
    runtime.Execute(
        authority =>
        {
            authority.SetAccountBan(managementAccount!, managementAction == "--ban-account");
            return true;
        },
        true
    );
    Console.WriteLine("Provider account access updated.");
    return;
}

builder.Services.Configure<ProviderOptions>(builder.Configuration.GetSection("Authentication"));
builder.Services.AddSingleton(services => new ProviderRuntime(
        services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProviderOptions>>().Value
    )
);
builder.Services.AddRateLimiter(limits =>
    {
        limits.AddPolicy(
            "browser",
            context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }
            )
        );
        limits.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 180, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }
            )
        );
        limits.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new AuthenticationErrorResponse(AuthenticationError.RateLimited, 2),
                cancellationToken
            );
        };
    }
);
var app = builder.Build();
_ = app.Services.GetRequiredService<ProviderRuntime>();
app.UseForwardedHeaders();
app.Use(async (context, next) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        try { await next(context); }
        catch (Exception exception) when (exception is AuthenticationProtocolException or AuthenticationRemoteException
                                              or
                                              FormatException or ArgumentException)
        {
            var error = exception is AuthenticationRemoteException remote
                ? remote.Error
                : AuthenticationError.InvalidRequest;
            context.Response.StatusCode = error == AuthenticationError.RateLimited ? 429 : 400;
            await context.Response.WriteAsJsonAsync(
                new AuthenticationErrorResponse(
                    error,
                    exception is AuthenticationRemoteException rejected ? rejected.RetryAfterSeconds : 0
                )
            );
        }
    }
);
app.UseRateLimiter();
app.MapAuthenticationProtocol();
app.MapBrowserLogin();
app.Run();

public partial class Program;
