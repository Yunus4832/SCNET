using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
[Union(0, typeof(CrossbowPullPackage))]
[Union(1, typeof(OpenBlockInventoryPackage))]
[Union(2, typeof(OpenInventoryPackage))]
[Union(3, typeof(OpenSignEditorPackage))]
public abstract record BlockEditPackage : IProtocolMessage
{
    public enum EventType
    {
        OpenInventoryByID = 0, // 打开背包，通过背包id， 注：背包指的是IInventory

        OpenInventoryByPoint = 1, // 打开背包，通过位置，通常是BlockEntity，例如箱子，炉子

        CrossbowPull = 2, // 十字弩拉弓

        EditSign = 3 // 编辑牌子
    }

    [IgnoreMember] public abstract EventType Type { get; }
}
