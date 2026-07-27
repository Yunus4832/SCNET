using Game.Commands;

namespace Survivalcraft.Test.Commands;

public class ServerAdministrationBootstrapTest
{
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
