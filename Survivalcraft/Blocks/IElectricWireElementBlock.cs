namespace Game.Blocks;

public interface IElectricWireElementBlock : IElectricElementBlock
{
    int GetConnectedWireFacesMask(int value, int face);
}
