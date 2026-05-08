using Engine.Core;

namespace Engine.Graphics;

public sealed class SamplerState : LockOnFirstUse
{
    public static readonly SamplerState PointClamp = new()
    {
        FilterMode = TextureFilterMode.Point,
        AddressModeU = TextureAddressMode.Clamp,
        AddressModeV = TextureAddressMode.Clamp,
        isLocked = true
    };

    public static readonly SamplerState PointWrap = new()
    {
        FilterMode = TextureFilterMode.Point,
        AddressModeU = TextureAddressMode.Wrap,
        AddressModeV = TextureAddressMode.Wrap,
        isLocked = true
    };

    public static readonly SamplerState LinearClamp = new()
    {
        FilterMode = TextureFilterMode.Linear,
        AddressModeU = TextureAddressMode.Clamp,
        AddressModeV = TextureAddressMode.Clamp,
        isLocked = true
    };

    public static readonly SamplerState LinearWrap = new()
    {
        FilterMode = TextureFilterMode.Linear,
        AddressModeU = TextureAddressMode.Wrap,
        AddressModeV = TextureAddressMode.Wrap,
        isLocked = true
    };

    public static readonly SamplerState AnisotropicClamp = new()
    {
        FilterMode = TextureFilterMode.Anisotropic,
        AddressModeU = TextureAddressMode.Clamp,
        AddressModeV = TextureAddressMode.Clamp,
        MaxAnisotropy = 16,
        isLocked = true
    };

    public static readonly SamplerState AnisotropicWrap = new()
    {
        FilterMode = TextureFilterMode.Anisotropic,
        AddressModeU = TextureAddressMode.Wrap,
        AddressModeV = TextureAddressMode.Wrap,
        MaxAnisotropy = 16,
        isLocked = true
    };

    private TextureAddressMode _addressModeU;

    private TextureAddressMode _addressModeV;

    private TextureFilterMode _filterMode;

    private int _maxAnisotropy;

    private float _maxLod = 1000f;

    private float _minLod = -1000f;

    private float _mipLodBias;

    public TextureFilterMode FilterMode
    {
        get => _filterMode;
        init
        {
            ThrowIfLocked();
            _filterMode = value;
        }
    }

    public TextureAddressMode AddressModeU
    {
        get => _addressModeU;
        init
        {
            ThrowIfLocked();
            _addressModeU = value;
        }
    }

    public TextureAddressMode AddressModeV
    {
        get => _addressModeV;
        init
        {
            ThrowIfLocked();
            _addressModeV = value;
        }
    }

    public int MaxAnisotropy
    {
        get => _maxAnisotropy;
        init
        {
            ThrowIfLocked();
            _maxAnisotropy = MathUtils.Max(value, 1);
        }
    }

    public float MinLod
    {
        get => _minLod;
        set
        {
            ThrowIfLocked();
            _minLod = value;
        }
    }

    public float MaxLod
    {
        get => _maxLod;
        init
        {
            ThrowIfLocked();
            _maxLod = value;
        }
    }

    public float MipLodBias
    {
        get => _mipLodBias;
        set
        {
            ThrowIfLocked();
            _mipLodBias = value;
        }
    }
}
