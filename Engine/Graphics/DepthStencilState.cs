namespace Engine.Graphics;

public sealed class DepthStencilState : LockOnFirstUse
{
    public static readonly DepthStencilState Default = new()
    {
        isLocked = true
    };

    public static readonly DepthStencilState DepthRead = new()
    {
        DepthBufferWriteEnable = false,
        isLocked = true
    };

    public static readonly DepthStencilState DepthWrite = new()
    {
        DepthBufferTestEnable = false,
        isLocked = true
    };

    public static readonly DepthStencilState None = new()
    {
        DepthBufferTestEnable = false,
        DepthBufferWriteEnable = false,
        isLocked = true
    };

    public CompareFunction MDepthBufferFunction = CompareFunction.LessEqual;

    public bool MDepthBufferTestEnable = true;

    public bool MDepthBufferWriteEnable = true;

    public bool DepthBufferTestEnable
    {
        get => MDepthBufferTestEnable;
        set
        {
            ThrowIfLocked();
            MDepthBufferTestEnable = value;
        }
    }

    public bool DepthBufferWriteEnable
    {
        get => MDepthBufferWriteEnable;
        set
        {
            ThrowIfLocked();
            MDepthBufferWriteEnable = value;
        }
    }

    public CompareFunction DepthBufferFunction
    {
        get => MDepthBufferFunction;
        set
        {
            ThrowIfLocked();
            MDepthBufferFunction = value;
        }
    }
}
