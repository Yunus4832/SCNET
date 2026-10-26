using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using SCNET.Authentication.Server.Infrastructure;

namespace Authentication.Server.Test;

public sealed class TrustedProxyTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyExplicitTrustedProxyCanChangeTheRateLimitPartition(bool trusted)
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-proxy-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    builder.ConfigureServices(services =>
                        {
                            services.Configure<ProviderOptions>(options =>
                                {
                                    options.PublicUrl = "https://auth.test/";
                                    options.DataDirectory = directory;
                                }
                            );
                            services.Configure<ForwardedHeadersOptions>(options =>
                                {
                                    options.KnownIPNetworks.Clear();
                                    options.KnownProxies.Clear();
                                    options.KnownProxies.Add(
                                        IPAddress.Parse(trusted ? "203.0.113.10" : "203.0.113.11")
                                    );
                                }
                            );
                            services.AddSingleton<IStartupFilter>(new RemotePeer());
                        }
                    );
                }
            );
            using var client = factory.CreateClient();
            for (var index = 1; index <= 181; index++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/scnet-authentication");
                request.Headers.Add("X-Forwarded-For", $"198.51.100.{index}");
                request.Headers.Add("X-Forwarded-Proto", "https");
                using var response = await client.SendAsync(request);
                Assert.Equal(
                    !trusted && index == 181 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK,
                    response.StatusCode
                );
            }
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    private sealed class RemotePeer : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use((context, continuation) =>
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
                        return continuation(context);
                    }
                );
                next(app);
            };
        }
    }
}
