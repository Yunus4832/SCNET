using Game.Commands;
using Game.Commands.BuiltIn;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Commands;

public sealed class BuiltInWorldRegistrationTest
{
    [Fact]
    public void WorldRegistrarKeepsServerAndWorldCommandsWithoutApplicationCommands()
    {
        var registry = new CommandRegistry();
        BuiltInCommandTestRegistry.RegisterWorld(registry, new ModId("game"));
        registry.Freeze();
        Assert.DoesNotContain(registry.Definitions, entry => entry.Definition.Domain == CommandDomain.Application);
        Assert.True(registry.TryGetDefinition(typeof(StopServerCommand), out _));
        Assert.True(registry.TryGetDefinition(typeof(ClaimServerAdministrationCommand), out _));
        Assert.True(registry.TryGetDefinition(typeof(TeleportSelfCommand), out _));
        Assert.True(registry.TryGetDefinition(typeof(StartP2PCommand), out _));
        Assert.True(registry.TryGetDefinition(typeof(GetWorldTimeCommand), out _));
        Assert.False(registry.TryGetDefinition(typeof(GetLanguageCommand), out _));
        Assert.False(registry.TryGetDefinition(typeof(ExitApplicationCommand), out _));
        Assert.DoesNotContain(
            typeof(BuiltInWorldCommands).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name is "Survivalcraft" or "Survivalcraft.Client"
        );
    }
}
