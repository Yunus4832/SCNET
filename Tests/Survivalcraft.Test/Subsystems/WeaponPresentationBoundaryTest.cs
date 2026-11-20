using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Characters;
using Game.Components;
using Game.Entities;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;

using Random = Engine.Core.Mathematics.Random;

namespace Survivalcraft.Test.Subsystems;

public sealed class WeaponPresentationBoundaryTest
{
    [Theory]
    [InlineData(191, 1.2f)]
    [InlineData(200, 1.3f)]
    [InlineData(212, 1.4f)]
    public void AuthorityWeaponLoadsAndAimsWithoutAnyPresentationSubsystemOrComponent(int contents, float handAngle)
    {
        using var project = RuleWorld(RuntimeSide.Authority);
        var behavior = CreateBehavior(contents);
        project.InitializeSubsystem(behavior);
        behavior.Load(new ValuesDictionary());
        var miner = CreateMiner(project, contents, false);
        WeaponAimRequest? request = null;
        SubscribeAim(behavior, fact => request = fact);
        var aim = new Ray3(Vector3.Zero, Vector3.UnitZ);
        Assert.False(behavior.OnAim(aim, miner, AimState.InProgress));
        Assert.Equal(handAngle, miner.ComponentCreature.ComponentCreaturePose.AimHandAngleOrder);
        var actual = Assert.IsType<WeaponAimRequest>(request);
        Assert.Same(miner, actual.Miner);
        Assert.InRange(actual.Aim.Direction.Length(), .99999f, 1.00001f);
        Assert.Null(project.FindSubsystem<SubsystemAudio>());
        Assert.Null(project.FindSubsystem<SubsystemParticles>());
    }

    [Fact]
    public void ThrowableLoadsWithoutAudioOrParticles()
    {
        using var project = RuleWorld(RuntimeSide.Authority);
        var behavior = new SubsystemThrowableBlockBehavior();
        project.InitializeSubsystem(behavior);
        behavior.Load(new ValuesDictionary());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MusketUncockConsumesTheOriginalRandomSampleEvenWithoutAListener(bool listen)
    {
        using var project = RuleWorld(RuntimeSide.Client);
        var behavior = new SubsystemMusketBlockBehavior();
        project.InitializeSubsystem(behavior);
        behavior.Load(new ValuesDictionary());
        var miner = CreateMiner(project, 212, true);
        var inventory = (WeaponInventory)miner.Inventory;
        inventory.Put(Terrain.MakeBlockValue(212, MusketBlock.SetHammerState(0, true)));
        var random = (Random)Field(behavior, "_random")!;
        random.Seed(12345);
        var reference = new Random(12345);
        var expectedPitch = reference.Float(-.1f, .1f);
        WeaponSoundRequest? sound = null;
        if (listen) { behavior.SoundRequested += request => sound = request; }

        behavior.OnAim(new Ray3(Vector3.Zero, Vector3.UnitZ), miner, AimState.Cancelled);
        Assert.Equal(reference.State, random.State);
        Assert.False(MusketBlock.GetHammerState(Terrain.ExtractData(inventory.GetSlotValue(0))));
        if (listen)
        {
            Assert.Equal("Audio/HammerUncock", sound!.Value.Name);
            Assert.Equal(expectedPitch, sound.Value.Pitch);
            Assert.Equal(0f, sound.Value.Delay);
        }
    }

    [Theory]
    [InlineData(191, WeaponInventoryKind.Bow)]
    [InlineData(200, WeaponInventoryKind.Crossbow)]
    [InlineData(212, WeaponInventoryKind.Musket)]
    public void EditingUsesALocalTypedRequestAndRejectsCrossWorldAndStaleSlots(int contents, WeaponInventoryKind kind)
    {
        using var project = RuleWorld(RuntimeSide.Client);
        using var other = RuleWorld(RuntimeSide.Client);
        var miner = CreateMiner(project, contents, true);
        var behavior = CreateBehavior(contents);
        project.InitializeSubsystem(behavior);
        WeaponInventoryEditRequest? request = null;
        SubscribeEdit(behavior, value => request = value);
        Assert.True(behavior.OnEditInventoryItem(miner.Inventory, 0, miner.ComponentCharacter!));
        var actual = Assert.IsType<WeaponInventoryEditRequest>(request);
        Assert.Equal(kind, actual.Kind);
        Assert.True(SubsystemWeaponPresentation.CanEdit(project, actual));
        Assert.False(SubsystemWeaponPresentation.CanEdit(other, actual));
        Assert.False(SubsystemWeaponPresentation.CanEdit(project, actual with { SlotIndex = 10 }));
        ((WeaponInventory)miner.Inventory).Put(Terrain.MakeBlockValue(2));
        Assert.False(SubsystemWeaponPresentation.CanEdit(project, actual));
        WorldRuntimeContext.For(project).Close();
        Assert.False(SubsystemWeaponPresentation.CanEdit(project, actual));
    }

    [Theory]
    [InlineData(191)]
    [InlineData(200)]
    [InlineData(212)]
    public void AuthorityEditingDoesNotInvokeClientPresentation(int contents)
    {
        using var project = RuleWorld(RuntimeSide.Authority);
        var miner = CreateMiner(project, contents, false);
        var behavior = CreateBehavior(contents);
        project.InitializeSubsystem(behavior);
        var calls = 0;
        SubscribeEdit(behavior, _ => calls++);
        Assert.True(behavior.OnEditInventoryItem(miner.Inventory, 0, miner.ComponentCharacter!));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void PresentationDisposalUnsubscribesEveryRulePublisher()
    {
        using var project = RuleWorld(RuntimeSide.Client);
        var rules = new SubsystemBlockBehavior[]
        {
            new SubsystemBowBlockBehavior(), new SubsystemCrossbowBlockBehavior(),
            new SubsystemMusketBlockBehavior(), new SubsystemThrowableBlockBehavior()
        };
        foreach (var rule in rules) { project.AddSubsystem(rule); }

        project.AddSubsystem(new SubsystemAudio());
        project.AddSubsystem(new SubsystemParticles());
        var presentation = new SubsystemWeaponPresentation();
        project.InitializeSubsystem(presentation);
        presentation.Load(new ValuesDictionary());
        foreach (var rule in rules)
        {
            Assert.NotNull(Field(rule, "AimPresentationRequested"));
            Assert.NotNull(Field(rule, "SoundRequested"));
        }

        presentation.Dispose();
        presentation.Dispose();
        foreach (var rule in rules)
        {
            foreach (var field in rule.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                         .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType)))
            {
                Assert.Null(field.GetValue(rule));
            }
        }
    }

    private static RuntimeTestProject RuleWorld(RuntimeSide side)
    {
        var project = new RuntimeTestProject(side);
        project.AddSubsystem(new SubsystemTerrain());
        project.AddSubsystem(new SubsystemTime());
        project.AddSubsystem(new SubsystemProjectiles());
        project.AddSubsystem(new SubsystemNoise());
        return project;
    }

    private static ComponentMiner CreateMiner(RuntimeTestProject project, int contents, bool local)
    {
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(data, local);
        var character = new ComponentCharacter
        {
            CharacterData = data,
            ComponentBody = new ComponentBody { Rotation = Quaternion.Identity },
            ComponentCreaturePose = new ComponentCreaturePose()
        };
        project.InitializeComponent(character);
        SetField(character.ComponentCreaturePose, "_creature", character);
        var inventory = new WeaponInventory();
        project.InitializeComponent(inventory);
        inventory.Put(Terrain.MakeBlockValue(contents));
        var miner = new ComponentMiner();
        project.InitializeComponent(miner);
        typeof(ComponentMiner).GetProperty(nameof(ComponentMiner.Inventory))!.SetValue(miner, inventory);
        typeof(ComponentMiner).GetProperty(nameof(ComponentMiner.ComponentCreature))!.SetValue(miner, character);
        typeof(ComponentMiner).GetProperty(nameof(ComponentMiner.ComponentCharacter))!.SetValue(miner, character);
        return miner;
    }

    private static SubsystemBlockBehavior CreateBehavior(int contents)
    {
        return contents switch
        {
            191 => new SubsystemBowBlockBehavior(),
            200 => new SubsystemCrossbowBlockBehavior(),
            212 => new SubsystemMusketBlockBehavior(),
            _ => throw new ArgumentOutOfRangeException(nameof(contents))
        };
    }

    private static void SubscribeAim(SubsystemBlockBehavior behavior, Action<WeaponAimRequest> callback)
    {
        if (behavior is SubsystemBowBlockBehavior bow) { bow.AimPresentationRequested += callback; }

        if (behavior is SubsystemCrossbowBlockBehavior crossbow) { crossbow.AimPresentationRequested += callback; }

        if (behavior is SubsystemMusketBlockBehavior musket) { musket.AimPresentationRequested += callback; }
    }

    private static void SubscribeEdit(SubsystemBlockBehavior behavior, Action<WeaponInventoryEditRequest> callback)
    {
        if (behavior is SubsystemBowBlockBehavior bow) { bow.InventoryEditRequested += callback; }

        if (behavior is SubsystemCrossbowBlockBehavior crossbow) { crossbow.InventoryEditRequested += callback; }

        if (behavior is SubsystemMusketBlockBehavior musket) { musket.InventoryEditRequested += callback; }
    }

    private static object? Field(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
    }

    private static void SetField(object instance, string name, object value)
    {
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
    }

    private sealed class WeaponInventory : ComponentInventoryBase
    {
        public override int ActiveSlotIndex { get; set; }

        public override int GetSlotCapacity(int slotIndex, int value) => 1;

        public void Put(int value)
        {
            slots.Clear();
            slots.Add(new Slot { Value = value, Count = 1 });
        }

        public override void OnSlotChange(int slotIndex)
        {
        }
    }
}
