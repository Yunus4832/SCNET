using EntitySystem.Core;

using Game.Commands.BuiltIn;
using Game.Subsystems;
using Game.World;

namespace Survivalcraft.Test.Commands;

public class ServerAdministrationBootstrapTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority, true)]
    [InlineData(RuntimeSide.Client, false)]
    public void ClaimCodesBelongToTheLiveAuthorityWorld(RuntimeSide side, bool expected)
    {
        using var project = new ClaimProject();
        var runtime = new WorldRuntimeContext(
            side,
            (side == RuntimeSide.Client && true)
        );
        runtime.BindProject(project);
        Assert.Equal(expected, ServerAdministrationBootstrap.TryGetClaimCode(project, out var first));
        Assert.Equal(expected, ServerAdministrationBootstrap.TryRegenerateClaimCode(project, out var second));
        if (expected)
        {
            Assert.NotEmpty(first);
            Assert.NotEqual(first, second);
        }
        else
        {
            Assert.Empty(first);
            Assert.Empty(second);
        }

        runtime.Close();
        Assert.False(ServerAdministrationBootstrap.TryGetClaimCode(project, out _));
    }

    private sealed class ClaimProject : Project
    {
        public ClaimProject()
        {
            subsystems.Add(new SubsystemPlayers());
            subsystems.Add(new SubsystemGameInfo());
        }
    }

    [Fact]
    public void ClaimCodeIsHumanReadableAndContainsSixtyBitsOfRandomCharacters()
    {
        var code = ServerAdministrationBootstrap.GenerateClaimCode();

        Assert.Matches("^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$", code);
    }

    [Fact]
    public void ClaimCodeComparisonIgnoresCaseAndSeparatorsButNotOtherCharacters()
    {
        const string code = "7KQM-4DPT-W9HX";

        Assert.True(ServerAdministrationBootstrap.CodesEqual(code, "7kqm4dptw9hx"));
        Assert.False(ServerAdministrationBootstrap.CodesEqual(code, "7KQM-4DPT-W9HY"));
        Assert.False(ServerAdministrationBootstrap.CodesEqual(code, "7KQM 4DPT W9HX"));
    }
}
