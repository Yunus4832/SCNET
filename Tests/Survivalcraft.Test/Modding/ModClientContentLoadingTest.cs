using Game.Modding;

namespace Survivalcraft.Test.Modding;

public sealed class ModClientContentLoadingTest
{
    [Fact]
    public void CoreEntrypointDoesNotResolveOptionalClientContent()
    {
        using var stream = ScpkgTestPackage.Create(
            """
            {
              "id":"example.separated",
              "name":"Separated",
              "version":"1.0.0",
              "entrypoint":"Survivalcraft.Test.Modding.PackageEntrypointMod, Survivalcraft.Test",
              "clientContentEntrypoint":"Missing.Client.Content, Missing.Client"
            }
            """,
            assemblyPath: typeof(PackageEntrypointMod).Assembly.Location
        );
        var package = ModPackage.Read("separated.scmod", stream);
        var descriptor = package.CreateDescriptor();
        using var lifetime = descriptor.Lifetime;
        Assert.NotNull(descriptor.Factory());
        var loader = Assert.IsAssignableFrom<IModEntrypointLoader>(descriptor.Entrypoints);
        Assert.Throws<FileNotFoundException>(() => loader.CreateEntrypoint<IDisposable>(
                package.Manifest.ClientContentEntrypoint!
            )
        );
    }

    [Fact]
    public void AClientOnlyEntrypointCanBeCreatedExplicitlyInThePackageScope()
    {
        using var stream = ScpkgTestPackage.Create(
            """
            {
              "id":"example.presentation",
              "name":"Presentation",
              "version":"1.0.0",
              "clientContentEntrypoint":"Survivalcraft.Test.Modding.ClientContentEntryFixture, Survivalcraft.Test"
            }
            """,
            assemblyPath: typeof(ClientContentEntryFixture).Assembly.Location
        );
        var package = ModPackage.Read("presentation.scmod", stream);
        var descriptor = package.CreateDescriptor();
        using var lifetime = descriptor.Lifetime;
        Assert.NotNull(descriptor.Factory());
        var loader = Assert.IsAssignableFrom<IModEntrypointLoader>(descriptor.Entrypoints);
        using var module = loader.CreateEntrypoint<IDisposable>(package.Manifest.ClientContentEntrypoint!);
        Assert.Equal(typeof(ClientContentEntryFixture).FullName, module.GetType().FullName);
        Assert.NotSame(typeof(ClientContentEntryFixture).Assembly, module.GetType().Assembly);
        Assert.Throws<InvalidOperationException>(() => loader.CreateEntrypoint<IMod>(
                package.Manifest.ClientContentEntrypoint!
            )
        );
    }

    [Fact]
    public void ClientContentSharesRuleTypeIdentityWithinTheSamePackageScope()
    {
        const string coreEntry = "Survivalcraft.Test.Modding.PackageEntrypointMod, Survivalcraft.Test";
        const string clientEntry = "Survivalcraft.Test.Modding.ClientContentEntryFixture, Survivalcraft.Test";
        using var stream = ScpkgTestPackage.Create(
            $$"""
              {
                "id":"example.shared",
                "name":"Shared",
                "version":"1.0.0",
                "entrypoint":"{{coreEntry}}",
                "clientContentEntrypoint":"{{clientEntry}}"
              }
              """,
            assemblyPath: typeof(ClientContentEntryFixture).Assembly.Location
        );
        var descriptor = ModPackage.Read("shared.scmod", stream).CreateDescriptor();
        using var lifetime = descriptor.Lifetime;
        var loader = Assert.IsAssignableFrom<IModEntrypointLoader>(descriptor.Entrypoints);
        var rule = loader.CreateEntrypoint<IMod>(coreEntry);
        var client = loader.CreateEntrypoint<IComparer<IMod>>(clientEntry);
        Assert.Equal(0, client.Compare(rule, rule));
        Assert.Same(rule.GetType().Assembly, client.GetType().Assembly);
    }
}
