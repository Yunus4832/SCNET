using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemMusketBlockBehavior : SubsystemBlockBehavior
{
    private const string _typeName = "SubsystemMusketBlockBehavior";

    private readonly Dictionary<ComponentMiner, double> _aimStartTimes = new();

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemNoise _subsystemNoise = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemProjectiles _subsystemProjectiles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public override int[] HandledBlocks => [];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        componentPlayer.ComponentGui.ModalPanelWidget = componentPlayer.ComponentGui.ModalPanelWidget == null
            ? new MusketWidget(inventory, slotIndex)
            : null;
        return true;
    }

    public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
    {
        var inventory = componentMiner.Inventory;
        var activeSlotIndex = inventory.ActiveSlotIndex;
        if (activeSlotIndex < 0)
        {
            return false;
        }

        var slotValue = inventory.GetSlotValue(activeSlotIndex);
        var slotCount = inventory.GetSlotCount(activeSlotIndex);
        var num = Terrain.ExtractContents(slotValue);
        var data = Terrain.ExtractData(slotValue);
        var num2 = slotValue;
        var num3 = 0;
        if (num != 212 || slotCount <= 0)
        {
            return false;
        }

        if (!_aimStartTimes.TryGetValue(componentMiner, out var value))
        {
            value = _subsystemTime.GameTime;
            _aimStartTimes[componentMiner] = value;
        }

        var num4 = (float)(_subsystemTime.GameTime - value);
        var num5 = (float)MathUtils.Remainder(_subsystemTime.GameTime, 1000.0);
        var v = ((componentMiner.ComponentCreature.ComponentBody.IsSneaking ? 0.01f : 0.03f) +
                 0.2f * MathUtils.Saturate((num4 - 2.5f) / 6f)) * new Vector3
        {
            X = SimplexNoise.OctavedNoise(num5, 2f, 3, 2f, 0.5f),
            Y = SimplexNoise.OctavedNoise(num5 + 100f, 2f, 3, 2f, 0.5f),
            Z = SimplexNoise.OctavedNoise(num5 + 200f, 2f, 3, 2f, 0.5f)
        };
        aim.Direction = Vector3.Normalize(aim.Direction + v);
        switch (state)
        {
            case AimState.InProgress:
            {
                if (num4 >= 10f)
                {
                    componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
                    return true;
                }

                if (num4 > 0.5f && !MusketBlock.GetHammerState(Terrain.ExtractData(num2)))
                {
                    num2 = Terrain.MakeBlockValue(num, 0,
                        MusketBlock.SetHammerState(Terrain.ExtractData(num2), true));
                    if (componentMiner.ComponentPlayer != null)
                    {
                        _subsystemAudio.PlaySound("Audio/HammerCock", 1f, _random.Float(-0.1f, 0.1f),
                            componentMiner.ComponentPlayer.ComponentCreatureModel.EyePosition, 64f, false);
                    }
                }

                var componentFirstPersonModel =
                    componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
                if (componentFirstPersonModel != null)
                {
                    componentMiner.ComponentPlayer?.ComponentAimingSights.ShowAimingSights(aim.Position,
                        aim.Direction);
                    componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
                    componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
                }

                componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
                componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder =
                    new Vector3(-0.08f, -0.08f, 0.07f);
                componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder =
                    new Vector3(-1.7f, 0f, 0f);
                break;
            }
            case AimState.Cancelled:
                if (MusketBlock.GetHammerState(Terrain.ExtractData(num2)))
                {
                    num2 = Terrain.MakeBlockValue(num, 0,
                        MusketBlock.SetHammerState(Terrain.ExtractData(num2), false));
                    if (componentMiner.ComponentPlayer != null)
                    {
                        _subsystemAudio.PlaySound("Audio/HammerUncock", 1f, _random.Float(-0.1f, 0.1f),
                            componentMiner.ComponentPlayer.ComponentCreatureModel.EyePosition, 64f, false);
                    }
                }

                _aimStartTimes.Remove(componentMiner);
                break;
            case AimState.Completed:
            {
                var flag = false;
                var value2 = 0;
                var num6 = 0;
                var s = 0f;
                var vector = Vector3.Zero;
                var loadState = MusketBlock.GetLoadState(data);
                var bulletType = MusketBlock.GetBulletType(data);
                if (MusketBlock.GetHammerState(Terrain.ExtractData(num2)))
                {
                    switch (loadState)
                    {
                        case MusketBlock.LoadState.Empty:
                            componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                                LanguageControl.Get(_typeName, 0), Color.White, true, false);
                            break;
                        case MusketBlock.LoadState.Gunpowder:
                        case MusketBlock.LoadState.Wad:
                            flag = true;
                            componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                                LanguageControl.Get(_typeName, 1), Color.White, true, false);
                            break;
                        case MusketBlock.LoadState.Loaded:
                            flag = true;
                            if (bulletType == BulletBlock.BulletType.Buckshot)
                            {
                                value2 = Terrain.MakeBlockValue(214, 0,
                                    BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall));
                                num6 = 8;
                                vector = new Vector3(0.04f, 0.04f, 0.25f);
                                s = 80f;
                            }
                            else if (bulletType == BulletBlock.BulletType.BuckshotBall)
                            {
                                value2 = Terrain.MakeBlockValue(214, 0,
                                    BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall));
                                num6 = 1;
                                vector = new Vector3(0.06f, 0.06f, 0f);
                                s = 60f;
                            }
                            else if (bulletType.HasValue)
                            {
                                value2 = Terrain.MakeBlockValue(214, 0,
                                    BulletBlock.SetBulletType(0, bulletType.Value));
                                num6 = 1;
                                s = 120f;
                            }

                            break;
                    }
                }

                if (flag)
                {
                    if (componentMiner.ComponentCreature.ComponentBody.ImmersionFactor > 0.4f)
                    {
                        _subsystemAudio.PlaySound("Audio/MusketMisfire", 1f, _random.Float(-0.1f, 0.1f),
                            componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 8f, true);
                    }
                    else
                    {
                        var vector2 = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition +
                                      componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f -
                                      componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
                        var vector3 = Vector3.Normalize(vector2 + aim.Direction * 10f - vector2);
                        var vector4 = Vector3.Normalize(Vector3.Cross(vector3, Vector3.UnitY));
                        var v2 = Vector3.Normalize(Vector3.Cross(vector3, vector4));
                        for (var i = 0; i < num6; i++)
                        {
                            var v3 = _random.Float(0f - vector.X, vector.X) * vector4 +
                                     _random.Float(0f - vector.Y, vector.Y) * v2 +
                                     _random.Float(0f - vector.Z, vector.Z) * vector3;
                            var projectile = _subsystemProjectiles.FireProjectile(value2, vector2,
                                s * (vector3 + v3), Vector3.Zero, componentMiner.ComponentCreature);
                            if (projectile != null)
                            {
                                projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
                            }
                        }

                        _subsystemAudio.PlaySound("Audio/MusketFire", 1f, _random.Float(-0.1f, 0.1f),
                            componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 64f, true);
                        _subsystemParticles.AddParticleSystem(
                            new GunSmokeParticleSystem(_subsystemTerrain, vector2 + 0.3f * vector3,
                                vector3));
                        _subsystemNoise.MakeNoise(vector2, 1f, 40f);
                        componentMiner.ComponentCreature.ComponentBody.ApplyImpulse(-4f * vector3);
                    }

                    num2 = Terrain.MakeBlockValue(Terrain.ExtractContents(num2), 0,
                        MusketBlock.SetLoadState(Terrain.ExtractData(num2), MusketBlock.LoadState.Empty));
                    num3 = 1;
                }

                if (MusketBlock.GetHammerState(Terrain.ExtractData(num2)))
                {
                    num2 = Terrain.MakeBlockValue(Terrain.ExtractContents(num2), 0,
                        MusketBlock.SetHammerState(Terrain.ExtractData(num2), false));
                    _subsystemAudio.PlaySound("Audio/HammerRelease", 1f, _random.Float(-0.1f, 0.1f),
                        componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 64f, false);
                }

                _aimStartTimes.Remove(componentMiner);
                if (CommonLib.WorkType == WorkType.Client)
                {
                    return true;
                }

                if (num2 != slotValue)
                {
                    inventory.RemoveSlotItems(activeSlotIndex, 1);
                    inventory.AddSlotItems(activeSlotIndex, num2, 1);
                }

                if (num3 > 0)
                {
                    componentMiner.DamageActiveTool(num3);
                }

                return true;
            }
        }

        if (CommonLib.WorkType != WorkType.Client)
        {
            if (num2 != slotValue)
            {
                inventory.RemoveSlotItems(activeSlotIndex, 1);
                inventory.AddSlotItems(activeSlotIndex, num2, 1);
            }

            if (num3 > 0)
            {
                componentMiner.DamageActiveTool(num3);
            }
        }
        else
        {
            //防止客户端声音重复
            if (num2 == slotValue)
            {
                return false;
            }

            inventory.RemoveNetSlotItems(activeSlotIndex, 1);
            inventory.AddNetSlotItems(activeSlotIndex, num2, 1);
        }

        return false;
    }

    public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
    {
        var num = Terrain.ExtractContents(value);
        var loadState = MusketBlock.GetLoadState(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));
        if (loadState == MusketBlock.LoadState.Empty && num == 109)
        {
            return 1;
        }

        if (loadState == MusketBlock.LoadState.Gunpowder && num == 205)
        {
            return 1;
        }

        if (loadState == MusketBlock.LoadState.Wad && num == 214)
        {
            return 1;
        }

        return 0;
    }

    public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count,
        int processCount, out int processedValue, out int processedCount)
    {
        processedValue = value;
        processedCount = count;
        if (processCount == 1)
        {
            var data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
            var loadState = MusketBlock.GetLoadState(data);
            var bulletType = MusketBlock.GetBulletType(data);
            switch (loadState)
            {
                case MusketBlock.LoadState.Empty:
                    loadState = MusketBlock.LoadState.Gunpowder;
                    bulletType = null;
                    break;
                case MusketBlock.LoadState.Gunpowder:
                    loadState = MusketBlock.LoadState.Wad;
                    bulletType = null;
                    break;
                case MusketBlock.LoadState.Wad:
                {
                    loadState = MusketBlock.LoadState.Loaded;
                    var data2 = Terrain.ExtractData(value);
                    bulletType = BulletBlock.GetBulletType(data2);
                    break;
                }
            }

            processedValue = 0;
            processedCount = 0;
            inventory.RemoveSlotItems(slotIndex, 1);
            inventory.AddSlotItems(slotIndex,
                Terrain.MakeBlockValue(212, 0,
                    MusketBlock.SetBulletType(MusketBlock.SetLoadState(data, loadState), bulletType)), 1);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
        base.Load(valuesDictionary);
    }
}
