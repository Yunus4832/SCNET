using Game.Commands;
using Game.Commands.BuiltIn;
using Game.Localization;
using Game.Modding;
using Game.Network;
using Game.Network.Handling;
using Game.Protocol;

namespace Survivalcraft.Hosting.Test.Commands;

public sealed class BuiltInProcessCommandsTest
{
    [Theory]
    [InlineData(CommandDomain.Application)]
    [InlineData(CommandDomain.Server)]
    public void CpuEntrypointRegistersProcessLifecycleCommands(CommandDomain diagnosticsDomain)
    {
        var transitions = 0;
        var catalog = new LanguageCatalog();
        catalog.LanguageTypes.Add("zh-CN");
        var services = new ProcessCommandServices(
            _ => transitions++,
            () => transitions++,
            _ => transitions++,
            () => catalog,
            _ => transitions++,
            () => transitions++
        );
        var node = new NetNode(NetNodeOptions.Default, false, PackageHandlerCatalog.Empty, NodeRole.Authority);
        using var mods = GameModRuntime.Start(
            registerAppCommands: (commands, owner) =>
                new BuiltInProcessCommands(
                        services,
                        new NetworkCommandDiagnostics(() => node, () => null),
                        diagnosticsDomain
                    )
                    .Register(commands, owner)
        );
        Assert.True(
            mods.Commands.TryGetDefinition(ResourceId.Parse("game:diagnostics/network/get"), out var diagnostic)
        );
        Assert.Equal(diagnosticsDomain, diagnostic!.Definition.Domain);
        var context = new CommandContext(CommandInvocationChannel.Console, CommandPrincipal.LocalOperator, null);
        var dispatcher = new CommandDispatcher(mods.Commands);
        Assert.True(dispatcher.Execute(new GetLanguageCommand(), context).Success);
        Assert.True(dispatcher.Execute(new RestartApplicationCommand(), context).Success);
        Assert.Equal(1, transitions);
        foreach (var path in new[]
                 {
                     "application/restart", "application/exit",
                     "application/instance/create", "application/instance/delete", "application/instance/clone",
                     "application/instance/switch", "application/language/get", "application/language/set",
                     "diagnostics/network/get"
                 })
        {
            Assert.True(mods.Commands.TryGetDefinition(ResourceId.Parse("game:" + path), out _));
        }
    }
}
