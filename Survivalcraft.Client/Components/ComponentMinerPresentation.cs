using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;

namespace Game.Components;

public sealed class ComponentMinerPresentation : Component
{
    private ComponentMiner _miner = null!;

    private SubsystemAudio _audio = null!;

    private SubsystemParticles _particles = null!;

    private SubsystemSoundMaterials _materials = null!;

    private SubsystemTerrain _terrain = null!;

    private BlockPresentationCatalog _blockPresentations = null!;

    private readonly Random _random = new();

    private double _lastToolHintTime;

    private bool _subscribed;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _miner = Entity.FindComponent<ComponentMiner>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _materials = Project.FindSubsystem<SubsystemSoundMaterials>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _blockPresentations = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog;
        Subscribe();
    }

    public override void OnEntityAdded() => Subscribe();
    public override void OnEntityRemoved() => Unsubscribe();
    public override void Dispose() => Unsubscribe();

    private bool CanPresent => _subscribed &&
                               WorldRuntimeContext.IsProjectActive(Project) &&
                               WorldRuntimeContext.For(Project).HasPresentation;

    private void Subscribe()
    {
        if (_miner is null || _subscribed) { return; }

        _miner.ToolLevelRequired += OnToolLevelRequired;
        _miner.ToolHintRequested += OnToolHintRequested;
        _miner.ItemDisabled += OnItemDisabled;
        _miner.FriendlyFireRejected += OnFriendlyFireRejected;
        _miner.DigImpact += OnDigImpact;
        _miner.BlockDug += OnBlockDug;
        _miner.BlockPlaced += OnBlockPlaced;
        _miner.MeleeSwing += OnMeleeSwing;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (_miner is null || !_subscribed) { return; }

        _miner.ToolLevelRequired -= OnToolLevelRequired;
        _miner.ToolHintRequested -= OnToolHintRequested;
        _miner.ItemDisabled -= OnItemDisabled;
        _miner.FriendlyFireRejected -= OnFriendlyFireRejected;
        _miner.DigImpact -= OnDigImpact;
        _miner.BlockDug -= OnBlockDug;
        _miner.BlockPlaced -= OnBlockPlaced;
        _miner.MeleeSwing -= OnMeleeSwing;
        _subscribed = false;
    }

    private void Notify(string text, Color color, bool blinking, bool sound)
    {
        if (!CanPresent || _miner.ComponentCharacter is not { IsLocallyControlled: true } character) { return; }

        character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(text, color, blinking, sound);
    }

    private void OnToolLevelRequired(int value)
    {
        if (!CanPresent) { return; }

        var block = _blockPresentations.Get(Terrain.ExtractContents(value));
        Notify(
            string.Format(
                Localization.ClientLanguage.Catalog.Get(ComponentMiner.Name, 1),
                block.Block.PlayerLevelRequired,
                block.GetDisplayName(_terrain, value)
            ),
            Color.White,
            true,
            true
        );
    }

    private void OnToolHintRequested(int digValue, float digTime)
    {
        if (!CanPresent ||
            _miner.ComponentCharacter is not { IsLocallyControlled: true } ||
            (_lastToolHintTime != 0 && Time.FrameStartTime - _lastToolHintTime <= 300)) { return; }

        var value = _miner.FindBestInventoryToolForDigging(digValue);
        string text;
        if (value == 0)
        {
            if (Terrain.ExtractContents(_miner.ActiveBlockValue) == 23 ||
                !digTime.CloseTo(_miner.CalculateDigTime(digValue, 0))) { return; }

            text = Localization.ClientLanguage.Catalog.Get(ComponentMiner.Name, "11");
        }
        else
        {
            if (_miner.CalculateDigTime(digValue, Terrain.ExtractContents(value)) >= .5f * digTime) { return; }

            text = string.Format(
                Localization.ClientLanguage.Catalog.Get(ComponentMiner.Name, 12),
                _blockPresentations.Get(Terrain.ExtractContents(value)).GetDisplayName(_terrain, value)
            );
        }

        Notify(text, Color.White, true, true);
        _lastToolHintTime = Time.FrameStartTime;
    }

    private void OnItemDisabled() => Notify(
        Localization.ClientLanguage.Catalog.Get(ComponentMiner.Name, "ItemDisabled"),
        Color.Red,
        false,
        true
    );

    private void OnFriendlyFireRejected()
    {
        Notify(Localization.ClientLanguage.Catalog.Get(ComponentMiner.Name, 3), Color.White, true, true);
    }

    private void OnDigImpact(int value, Vector3 position, float loudness)
    {
        if (!CanPresent) { return; }

        _materials.PlayImpactSound(value, position, loudness);
    }

    private void OnBlockDug(BlockDigOutcome outcome)
    {
        if (!CanPresent) { return; }

        _blockPresentations.Get(Terrain.ExtractContents(outcome.OldValue)).OnDug(_terrain, _particles, outcome);
    }

    private void OnBlockPlaced(Vector3 position)
    {
        if (!CanPresent) { return; }

        _audio.PlaySound("Audio/BlockPlaced", 1f, 0f, position, 5f, false);
    }

    private void OnMeleeSwing(Vector3 position)
    {
        if (!CanPresent) { return; }

        _audio.PlaySound("Audio/Swoosh", 1f, _random.Float(-.2f, .2f), position, 3f, false);
    }
}
