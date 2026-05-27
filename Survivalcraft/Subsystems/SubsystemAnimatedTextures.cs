using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemAnimatedTextures : Subsystem, IUpdateable
{
    private bool _disableTextureAnimation = false;

    private RenderTarget2D? _animatedBlocksTexture;

    private double _lastAnimateGameTime;

    private Vector2 _magmaOffset1;

    private Vector2 _magmaOffset2;

    private bool _magmaOrder;

    private readonly PrimitivesRenderer2D _primitivesRenderer = new();

    private readonly Random _random = new();

#if SERVER
    private readonly ScreenSpaceFireRenderer? _screenSpaceFireRenderer;
#else
    private readonly ScreenSpaceFireRenderer _screenSpaceFireRenderer = new(200);
#endif

    private SubsystemBlocksTexture _subsystemBlocksTexture = null!;

    private SubsystemTime _subsystemTime = null!;

    private Vector2 _waterOffset1;

    private Vector2 _waterOffset2;

    private bool _waterOrder;

    public bool ShowAnimatedTexture;

    public Texture2D AnimatedBlocksTexture
    {
        get
        {
            if (CommonLib.WorkType == WorkType.Client && CommonLib.BlockTexture != null)
            {
                return CommonLib.BlockTexture;
            }

            if (_disableTextureAnimation || _animatedBlocksTexture == null)
            {
                return _subsystemBlocksTexture.BlocksTexture;
            }

            return _animatedBlocksTexture;
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
#if SERVER
        return;
#endif
        if (_disableTextureAnimation || _subsystemTime.FixedTimeStep.HasValue)
        {
            return;
        }

        var dt2 = (float)MathUtils.Min(_subsystemTime.GameTime - _lastAnimateGameTime, 1.0);
        _lastAnimateGameTime = _subsystemTime.GameTime;
        var blocksTexture = _subsystemBlocksTexture.BlocksTexture;
        if (_animatedBlocksTexture == null || _animatedBlocksTexture.Width != blocksTexture.Width ||
            _animatedBlocksTexture.Height != blocksTexture.Height || _animatedBlocksTexture.MipLevelsCount > 1 !=
            SettingsManager.TerrainMipmapsEnabled)
        {
            Utilities.Dispose(ref _animatedBlocksTexture);
            _animatedBlocksTexture = new RenderTarget2D(blocksTexture.Width, blocksTexture.Height,
                !SettingsManager.TerrainMipmapsEnabled ? 1 : 4, ColorFormat.Rgba8888, DepthFormat.None);
            AnimatedBlocksTexture.Tag = blocksTexture.Tag;
        }

        var scissorRectangle = Display.ScissorRectangle;
        var renderTarget = Display.RenderTarget;
        Display.RenderTarget = _animatedBlocksTexture;
        try
        {
            Display.Clear(new Vector4(Color.Transparent));
            _primitivesRenderer
                .TexturedBatch(blocksTexture, false, -1, DepthStencilState.None, RasterizerState.CullNone,
                    BlendState.Opaque, SamplerState.PointClamp).QueueQuad(new Vector2(0f, 0f),
                    new Vector2(_animatedBlocksTexture.Width, _animatedBlocksTexture.Height), 0f, Vector2.Zero,
                    Vector2.One, Color.White);
            AnimateWaterBlocksTexture(_animatedBlocksTexture.Width);
            AnimateMagmaBlocksTexture(_animatedBlocksTexture.Width);
            _primitivesRenderer.Flush();
            Display.ScissorRectangle = AnimateFireBlocksTexture(dt2, _animatedBlocksTexture.Width);
            _primitivesRenderer.Flush();
        }
        finally
        {
            Display.RenderTarget = renderTarget;
            Display.ScissorRectangle = scissorRectangle;
        }

        if (SettingsManager.TerrainMipmapsEnabled && Time.FrameIndex % 2 == 0)
        {
            _animatedBlocksTexture.GenerateMipMaps();
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemBlocksTexture = Project.FindSubsystem<SubsystemBlocksTexture>(true)!;
#if !SERVER
        Display.DeviceReset += DisplayDeviceReset;
#endif
    }

    public override void Dispose()
    {
        Utilities.Dispose(ref _animatedBlocksTexture);
#if !SERVER
        Display.DeviceReset -= DisplayDeviceReset;
#endif
    }

    private void DisplayDeviceReset()
    {
        _animatedBlocksTexture = null;
    }

    private void AnimateWaterBlocksTexture(int textureWidth)
    {
        var batch = _primitivesRenderer.TexturedBatch(_subsystemBlocksTexture.BlocksTexture, false, 0,
            DepthStencilState.None, null, BlendState.AlphaBlend, SamplerState.PointClamp);
        var num = BlocksManager.Blocks[18].TextureSlot % 16;
        var num2 = BlocksManager.Blocks[18].TextureSlot / 16;
        var num3 = 1.0 * _subsystemTime.GameTime;
        var num4 = 1.0 * (_subsystemTime.GameTime - _subsystemTime.GameTimeDelta);
        var num5 = MathUtils.Min((float)MathUtils.Remainder(num3, 2.0), 1f);
        var num6 = MathUtils.Min((float)MathUtils.Remainder(num3 + 1.0, 2.0), 1f);
        var b = (byte)(255f * num5);
        var b2 = (byte)(255f * num6);
        if (MathUtils.Remainder(num3, 2.0) >= 1.0 && MathUtils.Remainder(num4, 2.0) < 1.0)
        {
            _waterOrder = true;
            _waterOffset2 = new Vector2(_random.Float(0f, 1f), _random.Float(0f, 1f));
        }
        else if (MathUtils.Remainder(num3 + 1.0, 2.0) >= 1.0 && MathUtils.Remainder(num4 + 1.0, 2.0) < 1.0)
        {
            _waterOrder = false;
            _waterOffset1 = new Vector2(_random.Float(0f, 1f), _random.Float(0f, 1f));
        }

        var tcOffset = new Vector2(num, num2) - (_waterOrder ? _waterOffset1 : _waterOffset2);
        var tcOffset2 = new Vector2(num, num2) - (_waterOrder ? _waterOffset2 : _waterOffset1);
        var color = _waterOrder ? new Color(b, b, b, b) : new Color(b2, b2, b2, b2);
        var color2 = _waterOrder ? new Color(b2, b2, b2, b2) : new Color(b, b, b, b);
        var num7 = MathUtils.Floor((float)MathUtils.Remainder(1.75 * _subsystemTime.GameTime, 1.0) * 16f) / 16f;
        var num8 = 0f - num7 + 1f;
        var num9 = MathUtils.Floor(
            (float)MathUtils.Remainder(1.75f / MathUtils.Sqrt(2f) * _subsystemTime.GameTime, 1.0) * 16f) / 16f;
        var num10 = 0f - num9 + 1f;
        var tc = new Vector2(0f, 0f);
        var tc2 = new Vector2(1f, 1f);
        DrawBlocksTextureSlot(batch, num, num2, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num, num2, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num7, 0f);
        tc2 = new Vector2(num7 + 1f, 1f);
        DrawBlocksTextureSlot(batch, num - 1, num2, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num - 1, num2, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num8, 0f);
        tc2 = new Vector2(num8 + 1f, 1f);
        DrawBlocksTextureSlot(batch, num + 1, num2, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num + 1, num2, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(0f, num7);
        tc2 = new Vector2(1f, num7 + 1f);
        DrawBlocksTextureSlot(batch, num, num2 - 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num, num2 - 1, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(0f, num8);
        tc2 = new Vector2(1f, num8 + 1f);
        DrawBlocksTextureSlot(batch, num, num2 + 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num, num2 + 1, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num9, num10);
        tc2 = new Vector2(num9 + 1f, num10 + 1f);
        DrawBlocksTextureSlot(batch, num - 1, num2 + 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num - 1, num2 + 1, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num10, num10);
        tc2 = new Vector2(num10 + 1f, num10 + 1f);
        DrawBlocksTextureSlot(batch, num + 1, num2 + 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num + 1, num2 + 1, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num9, num9);
        tc2 = new Vector2(num9 + 1f, num9 + 1f);
        DrawBlocksTextureSlot(batch, num - 1, num2 - 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num - 1, num2 - 1, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num10, num9);
        tc2 = new Vector2(num10 + 1f, num9 + 1f);
        DrawBlocksTextureSlot(batch, num + 1, num2 - 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num + 1, num2 - 1, tc, tc2, tcOffset2, color2, textureWidth);
    }

    public void AnimateMagmaBlocksTexture(int textureWidth)
    {
        var batch = _primitivesRenderer.TexturedBatch(_subsystemBlocksTexture.BlocksTexture, false, 0,
            DepthStencilState.None, null, BlendState.AlphaBlend, SamplerState.PointClamp);
        var num = BlocksManager.Blocks[92].TextureSlot % 16;
        var num2 = BlocksManager.Blocks[92].TextureSlot / 16;
        var num3 = 0.5 * _subsystemTime.GameTime;
        var num4 = 0.5 * (_subsystemTime.GameTime - _subsystemTime.GameTimeDelta);
        var num5 = MathUtils.Min((float)MathUtils.Remainder(num3, 2.0), 1f);
        var num6 = MathUtils.Min((float)MathUtils.Remainder(num3 + 1.0, 2.0), 1f);
        var b = (byte)(255f * num5);
        var b2 = (byte)(255f * num6);
        if (MathUtils.Remainder(num3, 2.0) >= 1.0 && MathUtils.Remainder(num4, 2.0) < 1.0)
        {
            _magmaOrder = true;
            _magmaOffset2 = new Vector2(_random.Float(0f, 1f), _random.Float(0f, 1f));
        }
        else if (MathUtils.Remainder(num3 + 1.0, 2.0) >= 1.0 && MathUtils.Remainder(num4 + 1.0, 2.0) < 1.0)
        {
            _magmaOrder = false;
            _magmaOffset1 = new Vector2(_random.Float(0f, 1f), _random.Float(0f, 1f));
        }

        var tcOffset = new Vector2(num, num2) - (_magmaOrder ? _magmaOffset1 : _magmaOffset2);
        var tcOffset2 = new Vector2(num, num2) - (_magmaOrder ? _magmaOffset2 : _magmaOffset1);
        var color = _magmaOrder ? new Color(b, b, b, b) : new Color(b2, b2, b2, b2);
        var color2 = _magmaOrder ? new Color(b2, b2, b2, b2) : new Color(b, b, b, b);
        var num7 = MathUtils.Floor(
            (float)MathUtils.Remainder(0.40000000596046448 * _subsystemTime.GameTime, 1.0) * 16f) / 16f;
        var num8 = 0f - num7 + 1f;
        var num9 = MathUtils.Floor(
            (float)MathUtils.Remainder(0.4f / MathUtils.Sqrt(2f) * _subsystemTime.GameTime, 1.0) * 16f) / 16f;
        var num10 = 0f - num9 + 1f;
        var tc = new Vector2(0f, 0f);
        var tc2 = new Vector2(1f, 1f);
        DrawBlocksTextureSlot(batch, num, num2, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num, num2, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num7, 0f);
        tc2 = new Vector2(num7 + 1f, 1f);
        DrawBlocksTextureSlot(batch, num - 1, num2, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num - 1, num2, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num8, 0f);
        tc2 = new Vector2(num8 + 1f, 1f);
        DrawBlocksTextureSlot(batch, num + 1, num2, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num + 1, num2, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(0f, num7);
        tc2 = new Vector2(1f, num7 + 1f);
        DrawBlocksTextureSlot(batch, num, num2 - 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num, num2 - 1, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(0f, num8);
        tc2 = new Vector2(1f, num8 + 1f);
        DrawBlocksTextureSlot(batch, num, num2 + 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num, num2 + 1, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num9, num10);
        tc2 = new Vector2(num9 + 1f, num10 + 1f);
        DrawBlocksTextureSlot(batch, num - 1, num2 + 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num - 1, num2 + 1, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num10, num10);
        tc2 = new Vector2(num10 + 1f, num10 + 1f);
        DrawBlocksTextureSlot(batch, num + 1, num2 + 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num + 1, num2 + 1, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num9, num9);
        tc2 = new Vector2(num9 + 1f, num9 + 1f);
        DrawBlocksTextureSlot(batch, num - 1, num2 - 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num - 1, num2 - 1, tc, tc2, tcOffset2, color2, textureWidth);
        tc = new Vector2(num10, num9);
        tc2 = new Vector2(num10 + 1f, num9 + 1f);
        DrawBlocksTextureSlot(batch, num + 1, num2 - 1, tc, tc2, tcOffset, color, textureWidth);
        DrawBlocksTextureSlot(batch, num + 1, num2 - 1, tc, tc2, tcOffset2, color2, textureWidth);
    }

    private Rectangle AnimateFireBlocksTexture(float dt, int textureWidth)
    {
#if SERVER
        return Rectangle.Empty;
#else
        var defaultTextureSlot = BlocksManager.Blocks[104].TextureSlot;
        float num = textureWidth / 16;
        var num2 = defaultTextureSlot % 16;
        var num3 = defaultTextureSlot / 16;
        _screenSpaceFireRenderer.ParticleSize = 1f * num;
        _screenSpaceFireRenderer.ParticleSpeed = 1.9f * num;
        _screenSpaceFireRenderer.ParticlesPerSecond = 24f;
        _screenSpaceFireRenderer.MinTimeToLive = float.PositiveInfinity;
        _screenSpaceFireRenderer.MaxTimeToLive = float.PositiveInfinity;
        _screenSpaceFireRenderer.ParticleAnimationOffset = 1f;
        _screenSpaceFireRenderer.ParticleAnimationPeriod = 3f;
        _screenSpaceFireRenderer.Origin = new Vector2(num2, num3 + 3) * num +
                                          new Vector2(0f, 0.5f * _screenSpaceFireRenderer.ParticleSize);
        _screenSpaceFireRenderer.Width = num;
        _screenSpaceFireRenderer.CutoffPosition = num3 * num;
        _screenSpaceFireRenderer.Update(dt);
        _screenSpaceFireRenderer.Draw(_primitivesRenderer, 0f, Matrix.Identity, Color.White);
        return new Rectangle((int)(num2 * num), (int)(num3 * num), (int)num, (int)(num * 3f));
#endif
    }

    private void DrawBlocksTextureSlot(
        TexturedBatch2D batch,
        int slotX,
        int slotY,
        Vector2 tc1,
        Vector2 tc2,
        Vector2 tcOffset,
        Color color,
        int textureWidth
    )
    {
        var s = textureWidth / 16f;
        batch.QueueQuad(new Vector2(slotX, slotY) * s, new Vector2(slotX + 1, slotY + 1) * s, 0f,
            (tc1 + tcOffset) / 16f, (tc2 + tcOffset) / 16f, color);
    }
}
