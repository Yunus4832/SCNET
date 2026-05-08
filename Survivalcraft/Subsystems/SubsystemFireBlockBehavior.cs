using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public class SubsystemFireBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    private readonly Dictionary<Point3, float> _expansionProbabilities = new();

    private readonly Dictionary<Point3, FireData> _fireData = new();

    private readonly DynamicArray<Point3> _firePointsCopy = new();

    private readonly Random _random = new();

    private readonly Dictionary<Point3, float> _toBurnAway = new();

    private readonly Dictionary<Point3, float> _toExpand = new();

    private int _copyIndex;

    private float _fireSoundIntensity;

    private float _fireSoundVolume;

    private float _lastScanDuration;

    private double _lastScanTime;

    private float _remainderToScan;

    private SubsystemAmbientSounds _subsystemAmbientSounds = null!;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemGameWidgets _subsystemViews = null!;

    public override int[] HandledBlocks => [104];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_firePointsCopy.Count == 0)
        {
            _firePointsCopy.Count += _fireData.Count;
            _fireData.Keys.CopyTo(_firePointsCopy.Array, 0);
            _copyIndex = 0;
            _lastScanDuration = (float)(_subsystemTime.GameTime - _lastScanTime);
            _lastScanTime = _subsystemTime.GameTime;
            if (_firePointsCopy.Count == 0)
            {
                _fireSoundVolume = 0f;
            }
        }

        if (_firePointsCopy.Count > 0)
        {
            var num = MathUtils.Min(1f * dt * _firePointsCopy.Count + _remainderToScan, 50f);
            var num2 = (int)num;
            _remainderToScan = num - num2;
            var num3 = MathUtils.Min(_copyIndex + num2, _firePointsCopy.Count);
            while (_copyIndex < num3)
            {
                if (_fireData.TryGetValue(_firePointsCopy.Array[_copyIndex], out var value))
                {
                    var x = value.Point.X;
                    var y = value.Point.Y;
                    var z = value.Point.Z;
                    var num4 = Terrain.ExtractData(SubsystemTerrain.Terrain.GetCellValue(x, y, z));
                    _fireSoundIntensity +=
                        1f / (_subsystemAudio.CalculateListenerDistanceSquared(new Vector3(x, y, z)) + 0.01f);
                    if ((num4 & 1) != 0)
                    {
                        value.Time0 -= _lastScanDuration;
                        if (value.Time0 <= 0f)
                        {
                            QueueBurnAway(x, y, z + 1, value.FireExpandability * 0.85f);
                        }

                        foreach (var expansionProbability in _expansionProbabilities)
                        {
                            if (_random.Float(0f, 1f) < expansionProbability.Value * _lastScanDuration *
                                value.FireExpandability)
                            {
                                _toExpand[
                                    new Point3(x + expansionProbability.Key.X, y + expansionProbability.Key.Y,
                                        z + 1 + expansionProbability.Key.Z)] = value.FireExpandability * 0.85f;
                            }
                        }
                    }

                    if ((num4 & 2) != 0)
                    {
                        value.Time1 -= _lastScanDuration;
                        if (value.Time1 <= 0f)
                        {
                            QueueBurnAway(x + 1, y, z, value.FireExpandability * 0.85f);
                        }

                        foreach (var expansionProbability2 in _expansionProbabilities)
                        {
                            if (_random.Float(0f, 1f) < expansionProbability2.Value * _lastScanDuration *
                                value.FireExpandability)
                            {
                                _toExpand[
                                    new Point3(x + 1 + expansionProbability2.Key.X, y + expansionProbability2.Key.Y,
                                        z + expansionProbability2.Key.Z)] = value.FireExpandability * 0.85f;
                            }
                        }
                    }

                    if ((num4 & 4) != 0)
                    {
                        value.Time2 -= _lastScanDuration;
                        if (value.Time2 <= 0f)
                        {
                            QueueBurnAway(x, y, z - 1, value.FireExpandability * 0.85f);
                        }

                        foreach (var expansionProbability3 in _expansionProbabilities)
                        {
                            if (_random.Float(0f, 1f) < expansionProbability3.Value * _lastScanDuration *
                                value.FireExpandability)
                            {
                                _toExpand[
                                    new Point3(x + expansionProbability3.Key.X, y + expansionProbability3.Key.Y,
                                        z - 1 + expansionProbability3.Key.Z)] = value.FireExpandability * 0.85f;
                            }
                        }
                    }

                    if ((num4 & 8) != 0)
                    {
                        value.Time3 -= _lastScanDuration;
                        if (value.Time3 <= 0f)
                        {
                            QueueBurnAway(x - 1, y, z, value.FireExpandability * 0.85f);
                        }

                        foreach (var expansionProbability4 in _expansionProbabilities)
                        {
                            if (_random.Float(0f, 1f) < expansionProbability4.Value * _lastScanDuration *
                                value.FireExpandability)
                            {
                                _toExpand[
                                    new Point3(x - 1 + expansionProbability4.Key.X, y + expansionProbability4.Key.Y,
                                        z + expansionProbability4.Key.Z)] = value.FireExpandability * 0.85f;
                            }
                        }
                    }

                    if (num4 == 0)
                    {
                        value.Time5 -= _lastScanDuration;
                        if (value.Time5 <= 0f)
                        {
                            QueueBurnAway(x, y - 1, z, value.FireExpandability * 0.85f);
                        }
                    }
                }

                _copyIndex++;
            }

            if (_copyIndex >= _firePointsCopy.Count)
            {
                _fireSoundVolume = 0.75f * _fireSoundIntensity;
                _firePointsCopy.Clear();
                _fireSoundIntensity = 0f;
            }
        }

        if (_subsystemTime.PeriodicGameTimeEvent(5.0, 0.0))
        {
            var num5 = 0;
            var num6 = 0;
            foreach (var item in _toBurnAway)
            {
                var key = item.Key;
                var value2 = item.Value;
                SubsystemTerrain.ChangeCell(key.X, key.Y, key.Z, Terrain.ReplaceContents(0, 0));
                if (value2 > 0.25f)
                {
                    for (var i = 0; i < 5; i++)
                    {
                        var point = CellFace.FaceToPoint3(i);
                        SetCellOnFire(key.X + point.X, key.Y + point.Y, key.Z + point.Z, value2);
                    }
                }

                var num7 = _subsystemViews.CalculateDistanceFromNearestView(new Vector3(key));
                if (num5 < 15 && num7 < 24f)
                {
                    _subsystemParticles.AddParticleSystem(
                        new BurntDebrisParticleSystem(SubsystemTerrain, key.X, key.Y, key.Z));
                    num5++;
                }

                if (num6 < 4 && num7 < 16f)
                {
                    _subsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, _random.Float(-0.25f, 0.25f),
                        new Vector3(key.X, key.Y, key.Z), 3f, true);
                    num6++;
                }
            }

            foreach (var item2 in _toExpand)
            {
                SetCellOnFire(item2.Key.X, item2.Key.Y, item2.Key.Z, item2.Value);
            }

            _toBurnAway.Clear();
            _toExpand.Clear();
        }

        _subsystemAmbientSounds.FireSoundVolume =
            MathUtils.Max(_subsystemAmbientSounds.FireSoundVolume, _fireSoundVolume);
    }

    public bool IsCellOnFire(int x, int y, int z)
    {
        for (var i = 0; i < 4; i++)
        {
            var point = CellFace.FaceToPoint3(i);
            var cellValue = SubsystemTerrain.Terrain.GetCellValue(x + point.X, y + point.Y, z + point.Z);
            if (Terrain.ExtractContents(cellValue) == 104)
            {
                var num = Terrain.ExtractData(cellValue);
                var num2 = CellFace.OppositeFace(i);
                if ((num & (1 << num2)) != 0)
                {
                    return true;
                }
            }
        }

        var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z);
        if (Terrain.ExtractContents(cellValue2) == 104 && Terrain.ExtractData(cellValue2) == 0)
        {
            return true;
        }

        return false;
    }

    public bool SetCellOnFire(int x, int y, int z, float fireExpandability, ComponentMiner? miner = null)
    {
        //在领地范围
        if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(x, z, out Territoriy? territoriy))
        {
            if (miner == null || !SubsystemBedrockBlockBehavior.AllowPlayerAction(miner.ComponentPlayer, territoriy!))
            {
                miner?.ComponentPlayer?.ComponentGui.DisplaySmallMessage("领地范围内不可点火", Color.Yellow, false, true);
                return false;
            }
        }

        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        if (BlocksManager.Blocks[num].FireDuration == 0f)
        {
            return false;
        }

        var result = false;
        for (var i = 0; i < 5; i++)
        {
            var point = CellFace.FaceToPoint3(i);
            var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(x + point.X, y + point.Y, z + point.Z);
            var num2 = Terrain.ExtractContents(cellValue2);
            if (num2 == 0 || num2 == 104 || num2 == 61)
            {
                var num3 = num2 == 104 ? Terrain.ExtractData(cellValue2) : 0;
                var num4 = CellFace.OppositeFace(i);
                num3 |= (1 << num4) & 0xF;
                cellValue = Terrain.ReplaceData(Terrain.ReplaceContents(0, 104), num3);
                AddFire(x + point.X, y + point.Y, z + point.Z, fireExpandability);
                SubsystemTerrain.ChangeCell(x + point.X, y + point.Y, z + point.Z, cellValue);
                result = true;
            }
        }

        return result;
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var num = Terrain.ExtractData(SubsystemTerrain.Terrain.GetCellValue(x, y, z));
        if ((num & 1) != 0 &&
            BlocksManager.Blocks[SubsystemTerrain.Terrain.GetCellContents(x, y, z + 1)].FireDuration == 0f)
        {
            num &= -2;
        }

        if ((num & 2) != 0 &&
            BlocksManager.Blocks[SubsystemTerrain.Terrain.GetCellContents(x + 1, y, z)].FireDuration == 0f)
        {
            num &= -3;
        }

        if ((num & 4) != 0 &&
            BlocksManager.Blocks[SubsystemTerrain.Terrain.GetCellContents(x, y, z - 1)].FireDuration == 0f)
        {
            num &= -5;
        }

        if ((num & 8) != 0 &&
            BlocksManager.Blocks[SubsystemTerrain.Terrain.GetCellContents(x - 1, y, z)].FireDuration == 0f)
        {
            num &= -9;
        }

        if (_fireData.TryGetValue(new Point3(x, y, z), out var value))
        {
            if ((num & 1) != 0 && neighborX == x && neighborY == y && neighborZ == z + 1)
            {
                InitializeFireDataTime(value, 0);
            }

            if ((num & 2) != 0 && neighborX == x + 1 && neighborY == y && neighborZ == z)
            {
                InitializeFireDataTime(value, 1);
            }

            if ((num & 4) != 0 && neighborX == x && neighborY == y && neighborZ == z - 1)
            {
                InitializeFireDataTime(value, 2);
            }

            if ((num & 8) != 0 && neighborX == x - 1 && neighborY == y && neighborZ == z)
            {
                InitializeFireDataTime(value, 3);
            }

            if (num == 0 && neighborX == x && neighborY == y - 1 && neighborZ == z)
            {
                InitializeFireDataTime(value, 5);
            }
        }

        var contents = 104;
        if (num == 0 && BlocksManager.Blocks[SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z)].FireDuration ==
            0f)
        {
            contents = 0;
        }

        var value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, contents), num);
        SubsystemTerrain.ChangeCell(x, y, z, value2);
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        AddFire(x, y, z, 1f);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        RemoveFire(x, y, z);
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        AddFire(x, y, z, 1f);
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var list = new List<Point3>();
        foreach (var key in _fireData.Keys)
        {
            if (key.X >= chunk.Origin.X && key.X < chunk.Origin.X + 16 && key.Z >= chunk.Origin.Y &&
                key.Z < chunk.Origin.Y + 16)
            {
                list.Add(key);
            }
        }

        foreach (var item in list)
        {
            RemoveFire(item.X, item.Y, item.Z);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemAmbientSounds = Project.FindSubsystem<SubsystemAmbientSounds>(true)!;
        for (var i = -2; i <= 2; i++)
        for (var j = -1; j <= 2; j++)
        for (var k = -2; k <= 2; k++)
        {
            if (i != 0 || j != 0 || k != 0)
            {
                var num = j < 0 ? 1.5f : 2.5f;
                if (MathUtils.Sqrt(i * i + j * j + k * k) <= num)
                {
                    var num2 = MathUtils.Sqrt(i * i + k * k);
                    var num3 = j > 0 ? 0.5f * j : -j;
                    _expansionProbabilities[new Point3(i, j, k)] = 0.02f / (num2 + num3);
                }
            }
        }
    }

    private void AddFire(int x, int y, int z, float expandability)
    {
        if (CommonLib.WorkType != WorkType.Client)
        {
            CommonLib.Net.QueuePackage(new ComponentOnFirePackage(x, y, z, expandability));
            AddFireNet(x, y, z, expandability);
        }
    }

    public void AddFireNet(int x, int y, int z, float expandability)
    {
        var point = new Point3(x, y, z);
        if (_fireData.ContainsKey(point))
        {
            return;
        }

        var fireData = new FireData
        {
            Point = point,
            FireExpandability = expandability
        };
        InitializeFireDataTimes(fireData);
        _fireData[point] = fireData;
    }

    private void RemoveFire(int x, int y, int z)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        CommonLib.Net.QueuePackage(new ComponentOnFirePackage(x, y, z));
        RemoveFireNet(x, y, z);
    }

    public void RemoveFireNet(int x, int y, int z)
    {
        var key = new Point3(x, y, z);
        _fireData.Remove(key);
    }

    private void InitializeFireDataTimes(FireData fireData)
    {
        InitializeFireDataTime(fireData, 0);
        InitializeFireDataTime(fireData, 1);
        InitializeFireDataTime(fireData, 2);
        InitializeFireDataTime(fireData, 3);
        InitializeFireDataTime(fireData, 5);
    }

    private void InitializeFireDataTime(FireData fireData, int face)
    {
        var point = CellFace.FaceToPoint3(face);
        var x = fireData.Point.X + point.X;
        var y = fireData.Point.Y + point.Y;
        var z = fireData.Point.Z + point.Z;
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y, z);
        var block = BlocksManager.Blocks[cellContents];
        switch (face)
        {
            case 4:
                break;
            case 0:
                fireData.Time0 = block.FireDuration * _random.Float(0.75f, 1.25f);
                break;
            case 1:
                fireData.Time1 = block.FireDuration * _random.Float(0.75f, 1.25f);
                break;
            case 2:
                fireData.Time2 = block.FireDuration * _random.Float(0.75f, 1.25f);
                break;
            case 3:
                fireData.Time3 = block.FireDuration * _random.Float(0.75f, 1.25f);
                break;
            case 5:
                fireData.Time5 = block.FireDuration * _random.Float(0.75f, 1.25f);
                break;
        }
    }

    private void QueueBurnAway(int x, int y, int z, float expandability)
    {
        var key = new Point3(x, y, z);
        _toBurnAway.TryAdd(key, expandability);
    }

    private class FireData
    {
        public float FireExpandability;

        public Point3 Point;

        public float Time0;

        public float Time1;

        public float Time2;

        public float Time3;

        public float Time5;
    }
}
