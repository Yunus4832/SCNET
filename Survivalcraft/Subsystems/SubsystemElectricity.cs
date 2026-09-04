using System.Globalization;
using System.Text;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemElectricity : Subsystem, IUpdateable
{
    public const float CircuitStepDuration = 0.01f;

    private static readonly ElectricConnectionPath?[] _connectionPathsTable =
    [
        new(0, 1, -1, 4, 4, 0),
        new(0, 1, 0, 0, 4, 5),
        new(0, 1, -1, 2, 4, 5),
        new(0, 0, 0, 5, 4, 2),
        new(-1, 0, -1, 3, 3, 0),
        new(-1, 0, 0, 0, 3, 1),
        new(-1, 0, -1, 2, 3, 1),
        new(0, 0, 0, 1, 3, 2),
        new(0, -1, -1, 5, 5, 0),
        new(0, -1, 0, 0, 5, 4),
        new(0, -1, -1, 2, 5, 4),
        new(0, 0, 0, 4, 5, 2),
        new(1, 0, -1, 1, 1, 0),
        new(1, 0, 0, 0, 1, 3),
        new(1, 0, -1, 2, 1, 3),
        new(0, 0, 0, 3, 1, 2),
        new(0, 0, -1, 2, 2, 0),
        null,
        null,
        null,
        new(-1, 1, 0, 4, 4, 1),
        new(0, 1, 0, 1, 4, 5),
        new(-1, 1, 0, 3, 4, 5),
        new(0, 0, 0, 5, 4, 3),
        new(-1, 0, 1, 0, 0, 1),
        new(0, 0, 1, 1, 0, 2),
        new(-1, 0, 1, 3, 0, 2),
        new(0, 0, 0, 2, 0, 3),
        new(-1, -1, 0, 5, 5, 1),
        new(0, -1, 0, 1, 5, 4),
        new(-1, -1, 0, 3, 5, 4),
        new(0, 0, 0, 4, 5, 3),
        new(-1, 0, -1, 2, 2, 1),
        new(0, 0, -1, 1, 2, 0),
        new(-1, 0, -1, 3, 2, 0),
        new(0, 0, 0, 0, 2, 3),
        new(-1, 0, 0, 3, 3, 1),
        null,
        null,
        null,
        new(0, 1, 1, 4, 4, 2),
        new(0, 1, 0, 2, 4, 5),
        new(0, 1, 1, 0, 4, 5),
        new(0, 0, 0, 5, 4, 0),
        new(1, 0, 1, 1, 1, 2),
        new(1, 0, 0, 2, 1, 3),
        new(1, 0, 1, 0, 1, 3),
        new(0, 0, 0, 3, 1, 0),
        new(0, -1, 1, 5, 5, 2),
        new(0, -1, 0, 2, 5, 4),
        new(0, -1, 1, 0, 5, 4),
        new(0, 0, 0, 4, 5, 0),
        new(-1, 0, 1, 3, 3, 2),
        new(-1, 0, 0, 2, 3, 1),
        new(-1, 0, 1, 0, 3, 1),
        new(0, 0, 0, 1, 3, 0),
        new(0, 0, 1, 0, 0, 2),
        null,
        null,
        null,
        new(1, 1, 0, 4, 4, 3),
        new(0, 1, 0, 3, 4, 5),
        new(1, 1, 0, 1, 4, 5),
        new(0, 0, 0, 5, 4, 1),
        new(1, 0, -1, 2, 2, 3),
        new(0, 0, -1, 3, 2, 0),
        new(1, 0, -1, 1, 2, 0),
        new(0, 0, 0, 0, 2, 1),
        new(1, -1, 0, 5, 5, 3),
        new(0, -1, 0, 3, 5, 4),
        new(1, -1, 0, 1, 5, 4),
        new(0, 0, 0, 4, 5, 1),
        new(1, 0, 1, 0, 0, 3),
        new(0, 0, 1, 3, 0, 2),
        new(1, 0, 1, 1, 0, 2),
        new(0, 0, 0, 2, 0, 1),
        new(1, 0, 0, 1, 1, 3),
        null,
        null,
        null,
        new(0, -1, -1, 2, 2, 4),
        new(0, 0, -1, 4, 2, 0),
        new(0, -1, -1, 5, 2, 0),
        new(0, 0, 0, 0, 2, 5),
        new(-1, -1, 0, 3, 3, 4),
        new(-1, 0, 0, 4, 3, 1),
        new(-1, -1, 0, 5, 3, 1),
        new(0, 0, 0, 1, 3, 5),
        new(0, -1, 1, 0, 0, 4),
        new(0, 0, 1, 4, 0, 2),
        new(0, -1, 1, 5, 0, 2),
        new(0, 0, 0, 2, 0, 5),
        new(1, -1, 0, 1, 1, 4),
        new(1, 0, 0, 4, 1, 3),
        new(1, -1, 0, 5, 1, 3),
        new(0, 0, 0, 3, 1, 5),
        new(0, -1, 0, 5, 5, 4),
        null,
        null,
        null,
        new(0, 1, -1, 2, 2, 5),
        new(0, 0, -1, 5, 2, 0),
        new(0, 1, -1, 4, 2, 0),
        new(0, 0, 0, 0, 2, 4),
        new(1, 1, 0, 1, 1, 5),
        new(1, 0, 0, 5, 1, 3),
        new(1, 1, 0, 4, 1, 3),
        new(0, 0, 0, 3, 1, 4),
        new(0, 1, 1, 0, 0, 5),
        new(0, 0, 1, 5, 0, 2),
        new(0, 1, 1, 4, 0, 2),
        new(0, 0, 0, 2, 0, 4),
        new(-1, 1, 0, 3, 3, 5),
        new(-1, 0, 0, 5, 3, 1),
        new(-1, 1, 0, 4, 3, 1),
        new(0, 0, 0, 1, 3, 4),
        new(0, 1, 0, 4, 4, 5),
        null,
        null,
        null
    ];

    private static readonly ElectricConnectorDirection?[] _connectorDirectionsTable =
    [
        null,
        ElectricConnectorDirection.Right,
        ElectricConnectorDirection.In,
        ElectricConnectorDirection.Left,
        ElectricConnectorDirection.Top,
        ElectricConnectorDirection.Bottom,
        ElectricConnectorDirection.Left,
        null,
        ElectricConnectorDirection.Right,
        ElectricConnectorDirection.In,
        ElectricConnectorDirection.Top,
        ElectricConnectorDirection.Bottom,
        ElectricConnectorDirection.In,
        ElectricConnectorDirection.Left,
        null,
        ElectricConnectorDirection.Right,
        ElectricConnectorDirection.Top,
        ElectricConnectorDirection.Bottom,
        ElectricConnectorDirection.Right,
        ElectricConnectorDirection.In,
        ElectricConnectorDirection.Left,
        null,
        ElectricConnectorDirection.Top,
        ElectricConnectorDirection.Bottom,
        ElectricConnectorDirection.Bottom,
        ElectricConnectorDirection.Right,
        ElectricConnectorDirection.Top,
        ElectricConnectorDirection.Left,
        null,
        ElectricConnectorDirection.In,
        ElectricConnectorDirection.Top,
        ElectricConnectorDirection.Right,
        ElectricConnectorDirection.Bottom,
        ElectricConnectorDirection.Left,
        ElectricConnectorDirection.In,
        null
    ];

    private static readonly int[] _connectorFacesTable =
    [
        4,
        3,
        5,
        1,
        2,
        4,
        0,
        5,
        2,
        3,
        4,
        1,
        5,
        3,
        0,
        4,
        2,
        5,
        0,
        1,
        2,
        1,
        0,
        3,
        5,
        0,
        1,
        2,
        3,
        4
    ];

    public static bool DebugDrawElectrics = false;

    public static int SimulatedElectricElements;

    private readonly Dictionary<ElectricElement, bool> _electricElements = new();

    private readonly Dictionary<CellFace, ElectricElement> _electricElementsByCellFace = new();

    private readonly Dictionary<Point3, ElectricElement> _electricElementsToAdd = new();

    private readonly Dictionary<ElectricElement, bool> _electricElementsToRemove = new();

    private readonly Dictionary<int, Dictionary<ElectricElement, bool>> _futureSimulateLists = new();

    private readonly List<Dictionary<ElectricElement, bool>> _listsCache = [];

    private readonly Dictionary<Point3, float> _persistentElementsVoltages = new();

    private readonly Dictionary<Point3, bool> _pointsToUpdate = new();

    private readonly DynamicArray<ElectricConnectionPath> _tmpConnectionPaths = [];

    private readonly Dictionary<CellFace, bool> _tmpResult = new();

    private readonly Dictionary<CellFace, bool> _tmpVisited = new();

    private readonly Dictionary<Point3, bool> _wiresToUpdate = new();

    public readonly List<NetSimulate> List = [];

    private Dictionary<ElectricElement, bool>? _nextStepSimulateList;

    private float _remainingSimulationTime;

    public SubsystemTime SubsystemTime { get; private set; } = null!;

    public SubsystemTerrain SubsystemTerrain { get; private set; } = null!;

    public SubsystemAudio SubsystemAudio { get; private set; } = null!;

    public int FrameStartCircuitStep { get; private set; }

    public int CircuitStep { get; private set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType != WorkType.Client)
        {
            FrameStartCircuitStep = CircuitStep;
            SimulatedElectricElements = 0;
            _remainingSimulationTime = MathUtils.Min(_remainingSimulationTime + dt, 0.1f);
            var sendFlag = Time.PeriodicEvent(0.05, 0.0);
            if (sendFlag)
            {
                var netSimulate = new NetSimulate
                {
                    StartStep = CircuitStep
                };
                foreach (var c in _persistentElementsVoltages)
                {
                    netSimulate.SaveData.Add(c.Key, c.Value);
                }

                List.Add(netSimulate);
                CommonLib.Net.QueuePackage(new SubsystemElectricityPackage(List));
                List.Clear();
            }

            while (_remainingSimulationTime >= 0.01f)
            {
                UpdateElectricElements();
                ++CircuitStep;
                _remainingSimulationTime -= 0.01f;
                _nextStepSimulateList = null;
                if (!_futureSimulateLists.Remove(CircuitStep, out var value))
                {
                    continue;
                }

                SimulatedElectricElements += value.Count;
                foreach (var key in value.Keys)
                {
                    if (_electricElements.ContainsKey(key))
                    {
                        SimulateElectricElement(key);
                    }
                }

                ReturnListToCache(value);
            }
        }
        else
        {
            _remainingSimulationTime = MathUtils.Min(_remainingSimulationTime + dt, 0.1f);
            FrameStartCircuitStep = CircuitStep;
            while (_remainingSimulationTime >= 0.01f)
            {
                //更新元件信息
                UpdateElectricElements();
                _nextStepSimulateList = null;
                if (List.Count > 0 && CircuitStep <= List[0].StartStep)
                {
                    if (List[0].StartStep == CircuitStep)
                    {
                        foreach (var (point, f) in List[0].SaveData)
                        {
                            _persistentElementsVoltages[point] = f;
                        }

                        List.RemoveAt(0);
                        _remainingSimulationTime -= 0.01f;
                    }

                    if (_futureSimulateLists.Remove(CircuitStep, out var value))
                    {
                        SimulatedElectricElements += value.Count;
                        foreach (var key in value.Keys)
                        {
                            if (_electricElements.ContainsKey(key))
                            {
                                SimulateElectricElement(key);
                            }
                        }

                        ReturnListToCache(value);
                    }

                    ++CircuitStep;
                }
                else
                {
                    _remainingSimulationTime -= 0.01f;
                }
            }

            List.Clear();
        }
    }

    public void OnElectricElementBlockGenerated(int x, int y, int z)
    {
        _pointsToUpdate[new Point3(x, y, z)] = false;
    }

    public void OnElectricElementBlockAdded(int x, int y, int z)
    {
        _pointsToUpdate[new Point3(x, y, z)] = true;
    }

    public void OnElectricElementBlockRemoved(int x, int y, int z)
    {
        _pointsToUpdate[new Point3(x, y, z)] = true;
    }

    public void OnElectricElementBlockModified(int x, int y, int z)
    {
        _pointsToUpdate[new Point3(x, y, z)] = true;
    }

    public void OnChunkDiscarding(TerrainChunk chunk)
    {
        foreach (var key in _electricElementsByCellFace.Keys)
        {
            if (key.X >= chunk.Origin.X && key.X < chunk.Origin.X + 16 && key.Z >= chunk.Origin.Y &&
                key.Z < chunk.Origin.Y + 16)
            {
                _pointsToUpdate[new Point3(key.X, key.Y, key.Z)] = false;
            }
        }
    }

    public static ElectricConnectorDirection? GetConnectorDirection(int mountingFace, int rotation, int connectorFace)
    {
        var result = _connectorDirectionsTable[6 * mountingFace + connectorFace];
        return result switch
        {
            null => null,
            < ElectricConnectorDirection.In => (ElectricConnectorDirection)((int)(result.Value + rotation) % 4),
            _ => result
        };
    }

    public static int GetConnectorFace(int mountingFace, ElectricConnectorDirection connectionDirection)
    {
        return _connectorFacesTable[(int)(5 * mountingFace + connectionDirection)];
    }

    public void GetAllConnectedNeighbors(int x, int y, int z, int mountingFace,
        DynamicArray<ElectricConnectionPath> list)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        if (BlocksManager.Blocks[Terrain.ExtractContents(cellValue)] is not IElectricElementBlock electricElementBlock)
        {
            return;
        }

        for (var electricConnectorDirection = ElectricConnectorDirection.Top;
             electricConnectorDirection < (ElectricConnectorDirection)5;
             electricConnectorDirection++)
        {
            for (var i = 0; i < 4; i++)
            {
                var electricConnectionPath =
                    _connectionPathsTable[20 * mountingFace + 4 * (int)electricConnectorDirection + i];
                if (electricConnectionPath == null)
                {
                    break;
                }

                var connectorType = electricElementBlock.GetConnectorType(SubsystemTerrain, cellValue, mountingFace,
                    electricConnectionPath.ConnectorFace, x, y, z);
                if (!connectorType.HasValue)
                {
                    break;
                }

                var x2 = x + electricConnectionPath.NeighborOffsetX;
                var y2 = y + electricConnectionPath.NeighborOffsetY;
                var z2 = z + electricConnectionPath.NeighborOffsetZ;
                var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(x2, y2, z2);
                var electricElementBlock2 =
                    BlocksManager.Blocks[Terrain.ExtractContents(cellValue2)] as IElectricElementBlock;
                var connectorType2 = electricElementBlock2?.GetConnectorType(SubsystemTerrain, cellValue2,
                    electricConnectionPath.NeighborFace, electricConnectionPath.NeighborConnectorFace, x2, y2, z2);
                if (!connectorType2.HasValue ||
                    ((connectorType.Value == 0 || connectorType2.Value == ElectricConnectorType.Output) &&
                     (connectorType.Value == ElectricConnectorType.Output || connectorType2.Value == 0)))
                {
                    continue;
                }

                var connectionMask = electricElementBlock.GetConnectionMask(cellValue);
                var connectionMask2 = electricElementBlock2?.GetConnectionMask(cellValue2);
                if ((connectionMask & connectionMask2) != 0)
                {
                    list.Add(electricConnectionPath);
                }
            }
        }
    }

    public ElectricElement? GetElectricElement(int x, int y, int z, int mountingFace)
    {
        _electricElementsByCellFace.TryGetValue(new CellFace(x, y, z, mountingFace), out var value);
        return value;
    }

    public void QueueElectricElementForSimulation(ElectricElement electricElement, int circuitStep)
    {
        if (circuitStep == CircuitStep + 1)
        {
            if (_nextStepSimulateList == null &&
                !_futureSimulateLists.TryGetValue(CircuitStep + 1, out _nextStepSimulateList))
            {
                _nextStepSimulateList = GetListFromCache();
                _futureSimulateLists.Add(CircuitStep + 1, _nextStepSimulateList);
            }

            _nextStepSimulateList[electricElement] = true;
        }
        else if (circuitStep > CircuitStep + 1)
        {
            if (!_futureSimulateLists.TryGetValue(circuitStep, out var value))
            {
                value = GetListFromCache();
                _futureSimulateLists.Add(circuitStep, value);
            }

            value[electricElement] = true;
        }
    }

    private void QueueElectricElementConnectionsForSimulation(ElectricElement electricElement, int circuitStep)
    {
        foreach (var connection in electricElement.Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Input &&
                connection.NeighborConnectorType != ElectricConnectorType.Output)
            {
                QueueElectricElementForSimulation(connection.NeighborElectricElement, circuitStep);
            }
        }
    }

    public float? ReadPersistentVoltage(Point3 point)
    {
        if (_persistentElementsVoltages.TryGetValue(point, out var value))
        {
            return value;
        }

        return null;
    }

    public void WritePersistentVoltage(Point3 point, float voltage)
    {
        if (CommonLib.WorkType != WorkType.Client)
        {
            _persistentElementsVoltages[point] = voltage;
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        SubsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        SubsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        SubsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        CircuitStep = valuesDictionary.GetValue("Step", 0);
        var array = valuesDictionary.GetValue<string>("VoltagesByCell")
            .Split([';'], StringSplitOptions.RemoveEmptyEntries);
        var num = 0;
        while (true)
        {
            if (num >= array.Length)
            {
                return;
            }

            var array2 = array[num].Split(',');
            if (array2.Length != 4)
            {
                break;
            }

            var x = int.Parse(array2[0], CultureInfo.InvariantCulture);
            var y = int.Parse(array2[1], CultureInfo.InvariantCulture);
            var z = int.Parse(array2[2], CultureInfo.InvariantCulture);
            var value = float.Parse(array2[3], CultureInfo.InvariantCulture);
            _persistentElementsVoltages[new Point3(x, y, z)] = value;
            num++;
        }

        throw new InvalidOperationException("Invalid number of tokens.");
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var num = 0;
        var stringBuilder = new StringBuilder();
        foreach (var persistentElementsVoltage in _persistentElementsVoltages)
        {
            if (num > 500)
            {
                break;
            }

            stringBuilder.Append(persistentElementsVoltage.Key.X.ToString(CultureInfo.InvariantCulture));
            stringBuilder.Append(',');
            stringBuilder.Append(persistentElementsVoltage.Key.Y.ToString(CultureInfo.InvariantCulture));
            stringBuilder.Append(',');
            stringBuilder.Append(persistentElementsVoltage.Key.Z.ToString(CultureInfo.InvariantCulture));
            stringBuilder.Append(',');
            stringBuilder.Append(persistentElementsVoltage.Value.ToString(CultureInfo.InvariantCulture));
            stringBuilder.Append(';');
            num++;
        }

        valuesDictionary.SetValue("VoltagesByCell", stringBuilder.ToString());
        valuesDictionary.SetValue("Step", CircuitStep);
    }

    private static ElectricConnectionPath? GetConnectionPath(
        int mountingFace,
        ElectricConnectorDirection localConnector,
        int neighborIndex
    )
    {
        return _connectionPathsTable[16 * mountingFace + 4 * (int)localConnector + neighborIndex];
    }

    private void SimulateElectricElement(ElectricElement electricElement)
    {
        if (electricElement.Simulate())
        {
            QueueElectricElementConnectionsForSimulation(electricElement, CircuitStep + 1);
        }
    }

    private void AddElectricElement(ElectricElement electricElement)
    {
        _electricElements.Add(electricElement, true);
        foreach (var cellFace2 in electricElement.CellFaces)
        {
            _electricElementsByCellFace.Add(cellFace2, electricElement);
            _tmpConnectionPaths.Clear();
            GetAllConnectedNeighbors(cellFace2.X, cellFace2.Y, cellFace2.Z, cellFace2.Face, _tmpConnectionPaths);
            foreach (var tmpConnectionPath in _tmpConnectionPaths)
            {
                var cellFace = new CellFace(cellFace2.X + tmpConnectionPath.NeighborOffsetX,
                    cellFace2.Y + tmpConnectionPath.NeighborOffsetY, cellFace2.Z + tmpConnectionPath.NeighborOffsetZ,
                    tmpConnectionPath.NeighborFace);
                if (!_electricElementsByCellFace.TryGetValue(cellFace, out var value) ||
                    value == electricElement)
                {
                    continue;
                }

                var cellValue = SubsystemTerrain.Terrain.GetCellValue(cellFace2.X, cellFace2.Y, cellFace2.Z);
                var num = Terrain.ExtractContents(cellValue);
                var value2 = ((IElectricElementBlock)BlocksManager.Blocks[num]).GetConnectorType(
                    SubsystemTerrain,
                    cellValue,
                    cellFace2.Face,
                    tmpConnectionPath.ConnectorFace,
                    cellFace2.X,
                    cellFace2.Y,
                    cellFace2.Z
                );
                var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
                var num2 = Terrain.ExtractContents(cellValue2);
                var value3 = ((IElectricElementBlock)BlocksManager.Blocks[num2]).GetConnectorType(
                    SubsystemTerrain,
                    cellValue2,
                    cellFace.Face,
                    tmpConnectionPath.NeighborConnectorFace,
                    cellFace.X,
                    cellFace.Y,
                    cellFace.Z
                );
                electricElement.Connections.Add(new ElectricConnection
                {
                    CellFace = cellFace2,
                    ConnectorFace = tmpConnectionPath.ConnectorFace,
                    ConnectorType = value2 ?? ElectricConnectorType.None,
                    NeighborElectricElement = value,
                    NeighborCellFace = cellFace,
                    NeighborConnectorFace = tmpConnectionPath.NeighborConnectorFace,
                    NeighborConnectorType = value3 ?? ElectricConnectorType.None,
                });
                value.Connections.Add(new ElectricConnection
                {
                    CellFace = cellFace,
                    ConnectorFace = tmpConnectionPath.NeighborConnectorFace,
                    ConnectorType = value3 ?? ElectricConnectorType.None,
                    NeighborElectricElement = electricElement,
                    NeighborCellFace = cellFace2,
                    NeighborConnectorFace = tmpConnectionPath.ConnectorFace,
                    NeighborConnectorType = value2 ?? ElectricConnectorType.None,
                });
            }
        }

        QueueElectricElementForSimulation(electricElement, CircuitStep + 1);
        QueueElectricElementConnectionsForSimulation(electricElement, CircuitStep + 2);
        electricElement.OnAdded();
    }

    private void RemoveElectricElement(ElectricElement electricElement)
    {
        electricElement.OnRemoved();
        QueueElectricElementConnectionsForSimulation(electricElement, CircuitStep + 1);
        _electricElements.Remove(electricElement);
        foreach (var cellFace in electricElement.CellFaces)
        {
            _electricElementsByCellFace.Remove(cellFace);
        }

        foreach (var connection in electricElement.Connections)
        {
            var num = connection.NeighborElectricElement.Connections.FirstIndex(c =>
                c.NeighborElectricElement == electricElement);
            if (num >= 0)
            {
                connection.NeighborElectricElement.Connections.RemoveAt(num);
            }
        }
    }

    private void UpdateElectricElements()
    {
        foreach (var item in _pointsToUpdate)
        {
            var key = item.Key;
            var cellValue = SubsystemTerrain.Terrain.GetCellValue(key.X, key.Y, key.Z);
            for (var i = 0; i < 6; i++)
            {
                var electricElement = GetElectricElement(key.X, key.Y, key.Z, i);
                if (electricElement != null)
                {
                    if (electricElement is WireDomainElectricElement)
                    {
                        _wiresToUpdate[key] = true;
                    }
                    else
                    {
                        _electricElementsToRemove[electricElement] = true;
                    }
                }
            }

            if (item.Value)
            {
                _persistentElementsVoltages.Remove(key);
            }

            var num = Terrain.ExtractContents(cellValue);
            if (BlocksManager.Blocks[num] is IElectricWireElementBlock)
            {
                _wiresToUpdate[key] = true;
            }
            else
            {
                var electricElementBlock = BlocksManager.Blocks[num] as IElectricElementBlock;
                if (electricElementBlock != null)
                {
                    var electricElement2 =
                        electricElementBlock.CreateElectricElement(this, cellValue, key.X, key.Y, key.Z);
                    if (electricElement2 != null)
                    {
                        _electricElementsToAdd[key] = electricElement2;
                    }
                }
            }
        }

        RemoveWireDomains();
        foreach (var item2 in _electricElementsToRemove)
        {
            RemoveElectricElement(item2.Key);
        }

        AddWireDomains();
        foreach (var value in _electricElementsToAdd.Values)
        {
            AddElectricElement(value);
        }

        _pointsToUpdate.Clear();
        _wiresToUpdate.Clear();
        _electricElementsToAdd.Clear();
        _electricElementsToRemove.Clear();
    }

    private void AddWireDomains()
    {
        _tmpVisited.Clear();
        foreach (var key in _wiresToUpdate.Keys)
        {
            for (var i = key.X - 1; i <= key.X + 1; i++)
            {
                for (var j = key.Y - 1; j <= key.Y + 1; j++)
                {
                    for (var k = key.Z - 1; k <= key.Z + 1; k++)
                    {
                        for (var l = 0; l < 6; l++)
                        {
                            _tmpResult.Clear();
                            ScanWireDomain(new CellFace(i, j, k, l), _tmpVisited, _tmpResult);
                            if (_tmpResult.Count > 0)
                            {
                                var electricElement = new WireDomainElectricElement(this, _tmpResult.Keys);
                                AddElectricElement(electricElement);
                            }
                        }
                    }
                }
            }
        }
    }

    private void RemoveWireDomains()
    {
        foreach (var key in _wiresToUpdate.Keys)
        {
            for (var i = key.X - 1; i <= key.X + 1; i++)
            {
                for (var j = key.Y - 1; j <= key.Y + 1; j++)
                {
                    for (var k = key.Z - 1; k <= key.Z + 1; k++)
                    {
                        for (var l = 0; l < 6; l++)
                        {
                            if (_electricElementsByCellFace.TryGetValue(new CellFace(i, j, k, l), out var value) &&
                                value is WireDomainElectricElement)
                            {
                                RemoveElectricElement(value);
                            }
                        }
                    }
                }
            }
        }
    }

    private void ScanWireDomain(CellFace startCellFace, Dictionary<CellFace, bool> visited,
        Dictionary<CellFace, bool> result)
    {
        var dynamicArray = new DynamicArray<CellFace>();
        dynamicArray.Add(startCellFace);
        while (dynamicArray.Count > 0)
        {
            var key = dynamicArray.Array[--dynamicArray.Count];
            if (visited.ContainsKey(key))
            {
                continue;
            }

            var chunkAtCell = SubsystemTerrain.Terrain.GetChunkAtCell(key.X, key.Z, false);
            if (chunkAtCell is not { AreBehaviorsNotified: true })
            {
                continue;
            }

            var cellValue = SubsystemTerrain.Terrain.GetCellValue(key.X, key.Y, key.Z);
            var num = Terrain.ExtractContents(cellValue);
            var electricWireElementBlock = BlocksManager.Blocks[num] as IElectricWireElementBlock;
            if (electricWireElementBlock == null)
            {
                continue;
            }

            var connectedWireFacesMask = electricWireElementBlock.GetConnectedWireFacesMask(cellValue, key.Face);
            if (connectedWireFacesMask == 0)
            {
                continue;
            }

            for (var i = 0; i < 6; i++)
            {
                if ((connectedWireFacesMask & (1 << i)) != 0)
                {
                    var key2 = new CellFace(key.X, key.Y, key.Z, i);
                    visited.Add(key2, true);
                    result.Add(key2, true);
                    _tmpConnectionPaths.Clear();
                    GetAllConnectedNeighbors(key2.X, key2.Y, key2.Z, key2.Face, _tmpConnectionPaths);
                    foreach (var tmpConnectionPath in _tmpConnectionPaths)
                    {
                        var x = key2.X + tmpConnectionPath.NeighborOffsetX;
                        var y = key2.Y + tmpConnectionPath.NeighborOffsetY;
                        var z = key2.Z + tmpConnectionPath.NeighborOffsetZ;
                        dynamicArray.Add(new CellFace(x, y, z, tmpConnectionPath.NeighborFace));
                    }
                }
            }
        }
    }

    private Dictionary<ElectricElement, bool> GetListFromCache()
    {
        if (_listsCache.Count <= 0)
        {
            return new Dictionary<ElectricElement, bool>();
        }

        var result = _listsCache[^1];
        _listsCache.RemoveAt(_listsCache.Count - 1);
        return result;
    }

    private void ReturnListToCache(Dictionary<ElectricElement, bool> list)
    {
        list.Clear();
        _listsCache.Add(list);
    }

    public class NetSimulate
    {
        public readonly Dictionary<Point3, float> SaveData = new();

        public int StartStep;
    }
}
