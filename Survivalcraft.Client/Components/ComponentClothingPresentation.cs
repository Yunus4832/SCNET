using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Blocks.Presentation;
using Game.Network.Transport;
using Game.World.Presentation;

namespace Game.Components;

public sealed class ComponentClothingPresentation : Component, IUpdateable
{
    private static readonly ClothingSlot[] _innerSlotsOrder =
    [
        ClothingSlot.Head,
        ClothingSlot.Torso,
        ClothingSlot.Feet,
        ClothingSlot.Legs
    ];

    private static readonly ClothingSlot[] _outerSlotsOrder =
    [
        ClothingSlot.Head,
        ClothingSlot.Torso,
        ClothingSlot.Legs,
        ClothingSlot.Feet
    ];


    private ComponentClothing _clothing = null!;

    private ComponentCharacter _componentCharacter = null!;

    private ComponentBody _body = null!;

    private ComponentHumanModel _componentHumanModel = null!;

    private ComponentOuterClothingModel _componentOuterClothingModel = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemAudio _audio = null!;

    private SubsystemParticles _particles = null!;

    private readonly Random _random = new();

    private Texture2D? _skinTexture;

    private string _skinTextureName = string.Empty;

    private RenderTarget2D? _innerClothedTexture;

    private RenderTarget2D? _outerClothedTexture;

    private bool _clothedTexturesValid;

    private bool _subscribed;

    private PrimitivesRenderer2D PrimitivesRenderer => field ??= new PrimitivesRenderer2D();

    public Texture2D InnerClothedTexture => _innerClothedTexture ??
                                            throw new InvalidOperationException(
                                                "Inner clothing texture is not initialized."
                                            );

    public Texture2D OuterClothedTexture => _outerClothedTexture ??
                                            throw new InvalidOperationException(
                                                "Outer clothing texture is not initialized."
                                            );

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.CreatureModels - 1;

    private bool CanPresent => _subscribed &&
                               WorldRuntimeContext.IsProjectActive(Project) &&
                               WorldRuntimeContext.For(Project).HasPresentation;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _clothing = Entity.FindComponent<ComponentClothing>(true)!;
        _componentCharacter = Entity.FindComponent<ComponentCharacter>(true)!;
        _body = Entity.FindComponent<ComponentBody>(true)!;
        _componentHumanModel = Entity.FindComponent<ComponentHumanModel>(true)!;
        _componentOuterClothingModel = Entity.FindComponent<ComponentOuterClothingModel>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        Subscribe();
    }

    public void Update(float dt)
    {
        if (!IsAddedToProject || !CanPresent) { return; }

        UpdateRenderTargets();
    }

    private void Subscribe()
    {
        if (_clothing is null || _subscribed) { return; }

        _clothing.ClothesChanged += Invalidate;
        Display.DeviceReset += Invalidate;
        _subscribed = true;
        Invalidate();
    }

    private void Unsubscribe()
    {
        if (!_subscribed) { return; }

        _clothing.ClothesChanged -= Invalidate;
        Display.DeviceReset -= Invalidate;
        _subscribed = false;
    }

    private void Invalidate() => _clothedTexturesValid = false;
    public override void OnEntityAdded() => Subscribe();

    public override void OnEntityRemoved()
    {
        Unsubscribe();
        ReleaseResources();
    }

    public override void Dispose()
    {
        Unsubscribe();
        ReleaseResources();
    }

    private void ReleaseRenderTargets()
    {
        if (_componentHumanModel is not null &&
            ReferenceEquals(_componentHumanModel.TextureOverride, _innerClothedTexture))
        {
            _componentHumanModel.TextureOverride = null;
        }

        if (_componentOuterClothingModel is not null &&
            ReferenceEquals(_componentOuterClothingModel.TextureOverride, _outerClothedTexture))
        {
            _componentOuterClothingModel.TextureOverride = null;
        }

        _innerClothedTexture?.Dispose();
        _innerClothedTexture = null;
        _outerClothedTexture?.Dispose();
        _outerClothedTexture = null;
        Invalidate();
    }

    private void ReleaseResources()
    {
        ReleaseRenderTargets();
        if (_skinTexture is not null && !ContentManager.IsContent(_skinTexture)) { _skinTexture.Dispose(); }

        _skinTexture = null;
        _skinTextureName = string.Empty;
    }

    private void UpdateSkin()
    {
        var name = _componentCharacter.CharacterData.CharacterSkinName;
        if (_skinTexture is not null && name == _skinTextureName) { return; }

        if (CharacterSkinsManager.CreateStore().Contains(name))
        {
            var replacement = CharacterSkinsManager.LoadTexture(name);
            ReleaseResources();
            _skinTexture = replacement;
            _skinTextureName = name;
        }
        else if (Time.PeriodicEvent(1, 0))
        {
            NetworkSender.SendToServer(
                WorldRuntimeContext.For(Project),
                new SkinRequestPackage { SkinName = name }
            );
        }

        _skinTexture ??= CharacterSkinsManager.LoadTexture("$Male1")!;
    }

    public void ApplyEffect(ClothingEffectPackage effect)
    {
        if (!CanPresent) { return; }

        if (effect.Kind == ClothingEffectKind.ArmorImpact)
        {
            var data = _clothing.GetClothingDataSafe(effect.ClothingValue);
            if (!string.IsNullOrEmpty(data?.ImpactSoundsFolder))
            {
                _audio.PlayRandomSound(data.ImpactSoundsFolder, 1f, _random.Float(-.3f, .3f), _body.Position, 4f, .15f);
            }

            return;
        }

        if (effect.Kind is ClothingEffectKind.Broken or ClothingEffectKind.WornOut)
        {
            _particles.AddParticleSystem(
                new BlockDebrisParticleSystem(
                    _subsystemTerrain,
                    _body.Position + _body.StanceBoxSize / 2f,
                    1f,
                    1f,
                    Color.White,
                    0
                )
            );
            if (effect.Kind == ClothingEffectKind.WornOut && _componentCharacter.IsLocallyControlled)
            {
                _componentCharacter.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                    Localization.ClientLanguage.Catalog.Get(nameof(ComponentClothing), 2),
                    Color.White,
                    true,
                    true
                );
            }

            return;
        }

        if (effect.Kind == ClothingEffectKind.LevelRequired && _componentCharacter.IsLocallyControlled)
        {
            var data = _clothing.GetClothingDataSafe(effect.ClothingValue);
            if (data is null) { return; }

            _componentCharacter.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                string.Format(
                    Localization.ClientLanguage.Catalog.Get(nameof(ComponentClothing), 1),
                    data.PlayerLevelRequired,
                    Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog
                        .Get(Terrain.ExtractContents(effect.ClothingValue))
                        .GetDisplayName(_subsystemTerrain, effect.ClothingValue)
                ),
                Color.White,
                true,
                true
            );
        }
    }

    public void UpdateRenderTargets()
    {
        if (!CanPresent) { return; }

        UpdateSkin();
        var skinTexture = _skinTexture ?? throw new InvalidOperationException("Character skin texture is unavailable.");

        if (_innerClothedTexture == null ||
            _innerClothedTexture.Width != skinTexture.Width ||
            _innerClothedTexture.Height != skinTexture.Height)
        {
            _innerClothedTexture = new RenderTarget2D(
                skinTexture.Width,
                skinTexture.Height,
                1,
                ColorFormat.Rgba8888,
                DepthFormat.None
            );
            _componentHumanModel.TextureOverride = _innerClothedTexture;
            _clothedTexturesValid = false;
        }

        if (_outerClothedTexture == null ||
            _outerClothedTexture.Width != skinTexture.Width ||
            _outerClothedTexture.Height != skinTexture.Height)
        {
            _outerClothedTexture = new RenderTarget2D(
                skinTexture.Width,
                skinTexture.Height,
                1,
                ColorFormat.Rgba8888,
                DepthFormat.None
            );
            _componentOuterClothingModel.TextureOverride = _outerClothedTexture;
            _clothedTexturesValid = false;
        }

        if (_clothedTexturesValid)
        {
            return;
        }

        _clothedTexturesValid = true;
        var scissorRectangle = Display.ScissorRectangle;
        var renderTarget = Display.RenderTarget;
        try
        {
            Display.RenderTarget = _innerClothedTexture;
            Display.Clear(new Vector4(Color.Transparent));
            var num = 0;
            var texturedBatch2D = PrimitivesRenderer.TexturedBatch(
                skinTexture,
                false,
                num++,
                DepthStencilState.None,
                null,
                BlendState.AlphaBlend,
                SamplerState.PointClamp
            );
            texturedBatch2D.QueueQuad(
                Vector2.Zero,
                new Vector2(_innerClothedTexture.Width, _innerClothedTexture.Height),
                0f,
                Vector2.Zero,
                Vector2.One,
                Color.White
            );
            var innerSlotsOrder = _innerSlotsOrder;
            foreach (var slot in innerSlotsOrder)
            {
                foreach (var cloth in _clothing.GetClothes(slot))
                {
                    var data = Terrain.ExtractData(cloth);
                    var clothingData = Modding.Blocks.WorldContentBinding.For(Project).Catalog
                        .Blocks[Terrain.ExtractContents(cloth)].GetClothingData(data);
                    var fabricColor =
                        PalettePresentation.GetFabricColor(_subsystemTerrain, ClothingBlock.GetClothingColor(data));
                    texturedBatch2D = PrimitivesRenderer.TexturedBatch(
                        ((ClothingBlockPresentation)
                            Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(
                                Terrain.ExtractContents(cloth)
                            ))
                        .GetClothingTexture(clothingData.Index),
                        false,
                        num++,
                        DepthStencilState.None,
                        null,
                        BlendState.AlphaBlend,
                        SamplerState.PointClamp
                    );
                    if (!clothingData.IsOuter)
                    {
                        texturedBatch2D.QueueQuad(
                            new Vector2(0f, 0f),
                            new Vector2(_innerClothedTexture.Width, _innerClothedTexture.Height),
                            0f,
                            Vector2.Zero,
                            Vector2.One,
                            fabricColor
                        );
                    }
                }
            }

            PrimitivesRenderer.Flush();
            Display.RenderTarget = _outerClothedTexture;
            Display.Clear(new Vector4(Color.Transparent));
            num = 0;
            innerSlotsOrder = _outerSlotsOrder;
            foreach (var slot2 in innerSlotsOrder)
            {
                foreach (var clothe2 in _clothing.GetClothes(slot2))
                {
                    var data2 = Terrain.ExtractData(clothe2);
                    var clothingData2 = Modding.Blocks.WorldContentBinding.For(Project).Catalog
                        .Blocks[Terrain.ExtractContents(clothe2)].GetClothingData(data2);
                    var fabricColor2 =
                        PalettePresentation.GetFabricColor(_subsystemTerrain, ClothingBlock.GetClothingColor(data2));
                    texturedBatch2D = PrimitivesRenderer.TexturedBatch(
                        ((ClothingBlockPresentation)
                            Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(
                                Terrain.ExtractContents(clothe2)
                            ))
                        .GetClothingTexture(clothingData2.Index),
                        false,
                        num++,
                        DepthStencilState.None,
                        null,
                        BlendState.AlphaBlend,
                        SamplerState.PointClamp
                    );
                    if (clothingData2.IsOuter)
                    {
                        texturedBatch2D.QueueQuad(
                            new Vector2(0f, 0f),
                            new Vector2(_outerClothedTexture.Width, _outerClothedTexture.Height),
                            0f,
                            Vector2.Zero,
                            Vector2.One,
                            fabricColor2
                        );
                    }
                }
            }

            PrimitivesRenderer.Flush();
        }
        finally
        {
            Display.RenderTarget = renderTarget;
            Display.ScissorRectangle = scissorRectangle;
        }
    }
}
