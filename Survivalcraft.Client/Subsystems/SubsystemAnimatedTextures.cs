using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemAnimatedTextures : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    private bool _disableTextureAnimation = false;

    private RenderTarget2D? _animatedBlocksTexture;

    private double _lastAnimateGameTime;

    private Vector2 _magmaOffset1;

    private Vector2 _magmaOffset2;

    private bool _magmaOrder;

    private PrimitivesRenderer2D PrimitivesRenderer => field ??= new PrimitivesRenderer2D();

    private readonly Random _random = new();

    private ScreenSpaceFireRenderer ScreenSpaceFireRenderer => field ??= new ScreenSpaceFireRenderer(200);

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
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        if (_disableTextureAnimation || _subsystemTime.FixedTimeStep.HasValue)
        {
            return;
        }

        var dt2 = (float)MathUtils.Min(_subsystemTime.GameTime - _lastAnimateGameTime, 1.0);
        _lastAnimateGameTime = _subsystemTime.GameTime;
        var blocksTexture = _subsystemBlocksTexture.BlocksTexture;
        if (_animatedBlocksTexture == null ||
            _animatedBlocksTexture.Width != blocksTexture.Width ||
            _animatedBlocksTexture.Height != blocksTexture.Height ||
            _animatedBlocksTexture.MipLevelsCount > 1 !=
            Configuration.ClientConfiguration.Preferences.TerrainMipmapsEnabled)
        {
            _animatedBlocksTexture?.Dispose();
            _animatedBlocksTexture = null;
            _animatedBlocksTexture = new RenderTarget2D(
                blocksTexture.Width,
                blocksTexture.Height,
                !Configuration.ClientConfiguration.Preferences.TerrainMipmapsEnabled ? 1 : 4,
                ColorFormat.Rgba8888,
                DepthFormat.None
            );
            AnimatedBlocksTexture.Tag = blocksTexture.Tag;
        }

        var scissorRectangle = Display.ScissorRectangle;
        var renderTarget = Display.RenderTarget;
        Display.RenderTarget = _animatedBlocksTexture;
        try
        {
            Display.Clear(new Vector4(Color.Transparent));
            PrimitivesRenderer
                .TexturedBatch(
                    blocksTexture,
                    false,
                    -1,
                    DepthStencilState.None,
                    RasterizerState.CullNone,
                    BlendState.Opaque,
                    SamplerState.PointClamp
                ).QueueQuad(
                    new Vector2(0f, 0f),
                    new Vector2(_animatedBlocksTexture.Width, _animatedBlocksTexture.Height),
                    0f,
                    Vector2.Zero,
                    Vector2.One,
                    Color.White
                );
            AnimateWaterBlocksTexture(_animatedBlocksTexture.Width);
            AnimateMagmaBlocksTexture(_animatedBlocksTexture.Width);
            PrimitivesRenderer.Flush();
            Display.ScissorRectangle = AnimateFireBlocksTexture(dt2, _animatedBlocksTexture.Width);
            PrimitivesRenderer.Flush();
        }
        finally
        {
            Display.RenderTarget = renderTarget;
            Display.ScissorRectangle = scissorRectangle;
        }

        if (Configuration.ClientConfiguration.Preferences.TerrainMipmapsEnabled && Time.FrameIndex % 2 == 0)
        {
            _animatedBlocksTexture.GenerateMipMaps();
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemBlocksTexture = Project.FindSubsystem<SubsystemBlocksTexture>(true)!;
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        Display.DeviceReset += DisplayDeviceReset;
    }

    public override void Dispose()
    {
        _animatedBlocksTexture?.Dispose();
        _animatedBlocksTexture = null;
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        Display.DeviceReset -= DisplayDeviceReset;
    }

    private void DisplayDeviceReset()
    {
        _animatedBlocksTexture = null;
    }

    private void AnimateWaterBlocksTexture(int textureWidth)
    {
        var batch = PrimitivesRenderer.TexturedBatch(
            _subsystemBlocksTexture.BlocksTexture,
            false,
            0,
            DepthStencilState.None,
            null,
            BlendState.AlphaBlend,
            SamplerState.PointClamp
        );
        var num = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(18).TextureSlot % 16;
        var num2 = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(18).TextureSlot / 16;
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
                       (float)MathUtils.Remainder(1.75f / MathUtils.Sqrt(2f) * _subsystemTime.GameTime, 1.0) * 16f
                   ) /
                   16f;
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
        var batch = PrimitivesRenderer.TexturedBatch(
            _subsystemBlocksTexture.BlocksTexture,
            false,
            0,
            DepthStencilState.None,
            null,
            BlendState.AlphaBlend,
            SamplerState.PointClamp
        );
        var num = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(92).TextureSlot % 16;
        var num2 = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(92).TextureSlot / 16;
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
                       (float)MathUtils.Remainder(0.40000000596046448 * _subsystemTime.GameTime, 1.0) * 16f
                   ) /
                   16f;
        var num8 = 0f - num7 + 1f;
        var num9 = MathUtils.Floor(
                       (float)MathUtils.Remainder(0.4f / MathUtils.Sqrt(2f) * _subsystemTime.GameTime, 1.0) * 16f
                   ) /
                   16f;
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
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return Rectangle.Empty;
        }

        var defaultTextureSlot = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(104).TextureSlot;
        float num = textureWidth / 16;
        var num2 = defaultTextureSlot % 16;
        var num3 = defaultTextureSlot / 16;
        ScreenSpaceFireRenderer.ParticleSize = 1f * num;
        ScreenSpaceFireRenderer.ParticleSpeed = 1.9f * num;
        ScreenSpaceFireRenderer.ParticlesPerSecond = 24f;
        ScreenSpaceFireRenderer.MinTimeToLive = float.PositiveInfinity;
        ScreenSpaceFireRenderer.MaxTimeToLive = float.PositiveInfinity;
        ScreenSpaceFireRenderer.ParticleAnimationOffset = 1f;
        ScreenSpaceFireRenderer.ParticleAnimationPeriod = 3f;
        ScreenSpaceFireRenderer.Origin = new Vector2(num2, num3 + 3) * num +
                                         new Vector2(0f, 0.5f * ScreenSpaceFireRenderer.ParticleSize);
        ScreenSpaceFireRenderer.Width = num;
        ScreenSpaceFireRenderer.CutoffPosition = num3 * num;
        ScreenSpaceFireRenderer.Update(dt);
        ScreenSpaceFireRenderer.Draw(PrimitivesRenderer, 0f, Matrix.Identity, Color.White);
        return new Rectangle((int)(num2 * num), (int)(num3 * num), (int)num, (int)(num * 3f));
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
        batch.QueueQuad(
            new Vector2(slotX, slotY) * s,
            new Vector2(slotX + 1, slotY + 1) * s,
            0f,
            (tc1 + tcOffset) / 16f,
            (tc2 + tcOffset) / 16f,
            color
        );
    }
}
