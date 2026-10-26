using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Server.Infrastructure;

namespace Authentication.Server.Test;

public sealed class ProviderStartupTest
{
    [Theory]
    [InlineData("")]
    [InlineData("http://auth.example/")]
    public void InvalidPublicEndpointFailsBeforeServingRequests(string url)
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-startup-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = Factory(directory, url);
            var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.IsType<AuthenticationProtocolException>(error.GetBaseException());
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void MissingEstablishedIdentityFailsStartupInsteadOfAdvertisingABrokenService()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-missing-key-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "authorizations.json"), "{}");
            using var factory = Factory(directory, "https://auth.example/");
            var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.IsType<InvalidDataException>(error.GetBaseException());
            Assert.False(File.Exists(Path.Combine(directory, "identity.p8")));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    private static WebApplicationFactory<Program> Factory(string directory, string publicUrl)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services => services.Configure<ProviderOptions>(options =>
                        {
                            options.PublicUrl = publicUrl;
                            options.DataDirectory = directory;
                        }
                    )
                );
            }
        );
    }
}
