using Engine.Core;

namespace Engine.Graphics;

public sealed class BlendState : LockOnFirstUse
{
    public static readonly BlendState Opaque = new()
    {
        isLocked = true
    };

    public static readonly BlendState Additive = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.SourceAlpha,
        AlphaDestinationBlend = Blend.One,
        isLocked = true
    };

    public static readonly BlendState AlphaBlend = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.InverseSourceAlpha,
        isLocked = true
    };

    public static readonly BlendState NonPremultiplied = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.SourceAlpha,
        AlphaDestinationBlend = Blend.InverseSourceAlpha,
        isLocked = true
    };

    public BlendFunction MAlphaBlendFunction;

    public Blend MAlphaDestinationBlend;

    public Blend MAlphaSourceBlend = Blend.One;

    public Vector4 MBlendFactor = Vector4.Zero;

    public BlendFunction MColorBlendFunction;

    public Blend MColorDestinationBlend;

    public Blend MColorSourceBlend = Blend.One;

    public BlendFunction AlphaBlendFunction
    {
        get => MAlphaBlendFunction;
        set
        {
            ThrowIfLocked();
            MAlphaBlendFunction = value;
        }
    }

    public Blend AlphaSourceBlend
    {
        get => MAlphaSourceBlend;
        set
        {
            ThrowIfLocked();
            MAlphaSourceBlend = value;
        }
    }

    public Blend AlphaDestinationBlend
    {
        get => MAlphaDestinationBlend;
        set
        {
            ThrowIfLocked();
            MAlphaDestinationBlend = value;
        }
    }

    public BlendFunction ColorBlendFunction
    {
        get => MColorBlendFunction;
        set
        {
            ThrowIfLocked();
            MColorBlendFunction = value;
        }
    }

    public Blend ColorSourceBlend
    {
        get => MColorSourceBlend;
        set
        {
            ThrowIfLocked();
            MColorSourceBlend = value;
        }
    }

    public Blend ColorDestinationBlend
    {
        get => MColorDestinationBlend;
        set
        {
            ThrowIfLocked();
            MColorDestinationBlend = value;
        }
    }

    public Vector4 BlendFactor
    {
        get => MBlendFactor;
        set
        {
            ThrowIfLocked();
            MBlendFactor = value;
        }
    }
}
