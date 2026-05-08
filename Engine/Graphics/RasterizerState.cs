namespace Engine.Graphics;

public sealed class RasterizerState : LockOnFirstUse
{
    public static readonly RasterizerState CullNone = new()
    {
        CullMode = CullMode.None,
        isLocked = true
    };

    public static readonly RasterizerState CullNoneScissor = new()
    {
        CullMode = CullMode.None,
        ScissorTestEnable = true,
        isLocked = true
    };

    public static readonly RasterizerState CullClockwise = new()
    {
        CullMode = CullMode.CullClockwise,
        isLocked = true
    };

    public static readonly RasterizerState CullClockwiseScissor = new()
    {
        CullMode = CullMode.CullClockwise,
        ScissorTestEnable = true,
        isLocked = true
    };

    public static readonly RasterizerState CullCounterClockwise = new()
    {
        CullMode = CullMode.CullCounterClockwise,
        isLocked = true
    };

    public static readonly RasterizerState CullCounterClockwiseScissor = new()
    {
        CullMode = CullMode.CullCounterClockwise,
        ScissorTestEnable = true,
        isLocked = true
    };

    public CullMode MCullMode = CullMode.CullCounterClockwise;

    public float MDepthBias;

    public bool MScissorTestEnable;

    public float MSlopeScaleDepthBias;

    public CullMode CullMode
    {
        get => MCullMode;
        set
        {
            ThrowIfLocked();
            MCullMode = value;
        }
    }

    public bool ScissorTestEnable
    {
        get => MScissorTestEnable;
        set
        {
            ThrowIfLocked();
            MScissorTestEnable = value;
        }
    }

    public float DepthBias
    {
        get => MDepthBias;
        set
        {
            ThrowIfLocked();
            MDepthBias = value;
        }
    }

    public float SlopeScaleDepthBias
    {
        get => MSlopeScaleDepthBias;
        set
        {
            ThrowIfLocked();
            MSlopeScaleDepthBias = value;
        }
    }
}
