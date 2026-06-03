using System.Runtime.CompilerServices;

namespace Game.Terrains;

/* Chunk 数据块中未压缩的 Block 方块结构以及 Shaft 簇的结构:
 *
 *                              (0xFFFFC000)              (0x3C00)       (0x3FF)
 *                  +-------------- data ---------------+ +light+ +---- contents ----+
 *                  |                                   | |     | |                  |
 *      BlockValue: 0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0      (32 bit)
 *
 *                   (0xFF000000)                     (0xF000)              (0xFF)
 *
 * 2.3 版本的 Shaft 簇信息如下，使用 4 Byte 32 bit 存储:
 *
                    (0xFF000000)                     (0xF000)              (0xFF)
 *                 sun light height                   humidity            top height
 *                  +-------------+                   +-----+          +-------------+
 *                  |             |                   |     |          |             |
 *      ShaftValue: 0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0      (32 bit)
 *                                   |             |          |     |
 *                                   +-------------+          +-----+
 *                                    bottom height         temperature
 *                                      (0xFF0000)            (0xF00)
 *
 * 2.4 版本的 Shaft 簇信息如下，使用 8 Byte 64 bit 存储:
 *                                                               (0xFFF00000000)
 *                                                               sun light height
 *                                                           +----------------------+
 *                                                           |                      |
 *      High32Bit: 0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0      (32 bit)
 *
 *                                          (0xF0000)                (0xFFF)
 *                                          humidity                top height
 *                                          +-----+          +----------------------+
 *                                          |     |          |                      |
 *       Low32Bit: 0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0      (64 bit)
 *                 |                      |          |     |
 *                 +----------------------+          +-----+
 *                        bottom height            temperature
 *                         (0xFFF00000)              (0xF000)
 *
 * 对于 Block 方块的结构，在存储前 light 光照位的信息会被清除，并用于存储使用 RLE 算法计算出的该 Block 方块在 Chunk 中重复的次数，即:
 *
 *                                  (0xFFFFC000)              (0x3C00)       (0x3FF)
 *                      +-------------- data ---------------+ +count+ +---- contents ----+
 *                      |                                   | |     | |                  |
 *      ValueCountPair: 0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0  0 0 0 0 0 0 0 0      (32 bit)
 */

/// <summary>
/// 地形信息
/// </summary>
public class Terrain : IDisposable
{
    /// <summary>
    /// 区块高度
    /// </summary>
    private const int _height = 256;

    /// <summary>
    /// 数据块区块坐标的偏移
    /// </summary>
    private const int _chunkRegionCoordsShift = 4;

    /// <summary>
    /// 数据块坐标掩码
    /// </summary>
    private const int _chunkCoordsMask = 0xF;

    /// <summary>
    /// 内容掩码
    /// </summary>
    private const int _contentsMask = 0x3FF;

    /// <summary>
    /// 光照掩码
    /// </summary>
    private const int _lightMask = 0x3C00;

    /// <summary>
    /// 光照偏移
    /// </summary>
    private const int _lightShift = 10;

    /// <summary>
    /// 数据掩码
    /// </summary>
    private const int _dataMask = unchecked((int)0xFFFFC000);

    /// <summary>
    /// 数据偏移
    /// </summary>
    private const int _dataShift = 14;

    /// <summary>
    /// 最大高度掩码
    /// </summary>
    private const long _topHeightMask = 0xFFF;

    /// <summary>
    /// 最大高度偏移
    /// </summary>
    private const int _topHeightShift = 0;

    /// <summary>
    /// 温度掩码
    /// </summary>
    private const long _temperatureMask = 0xF000;

    /// <summary>
    /// 温度偏移
    /// </summary>
    private const int _temperatureShift = 12;

    /// <summary>
    /// 湿度掩码
    /// </summary>
    private const int _humidityMask = 0xF0000;

    /// <summary>
    /// 湿度偏移
    /// </summary>
    private const int _humidityShift = 16;

    /// <summary>
    /// 最小高度掩码
    /// </summary>
    private const long _bottomHeightMask = 0xFFF00000;

    /// <summary>
    /// 最小高度偏移
    /// </summary>
    private const int _bottomHeightShift = 20;

    /// <summary>
    /// 阳光高度掩码
    /// </summary>
    private const long _sunlightHeightMask = 0xFFF00000000;

    /// <summary>
    /// 阳光高度偏移
    /// </summary>
    private const int _sunlightHeightShift = 32;

    private readonly ChunksStorage _allChunks = new();

    private readonly HashSet<TerrainChunk> _allocatedChunks = [];

    private TerrainChunk[] _allocatedChunksArray = [];

    /// <summary>
    /// 季节湿度偏移值
    /// </summary>
    public int SeasonHumidity;

    /// <summary>
    /// 季节温度偏移值
    /// </summary>
    public int SeasonTemperature;

    /// <summary>
    /// 已分配的所有区块数组（缓存优化）
    /// </summary>
    public TerrainChunk[] AllocatedChunks
    {
        get
        {
            if (_allocatedChunksArray.Length == 0)
            {
                _allocatedChunksArray = _allocatedChunks.ToArray();
            }

            return _allocatedChunksArray;
        }
    }

    /// <summary>
    /// 释放所有已分配的区块资源
    /// </summary>
    public void Dispose()
    {
        foreach (var allocatedChunk in _allocatedChunks)
        {
            allocatedChunk.Dispose();
        }
    }

    /// <summary>
    /// 获取下一个可用的区块（按坐标顺序）
    /// </summary>
    /// <param name="chunkX">区块 X 坐标</param>
    /// <param name="chunkZ">区块 Z 坐标</param>
    /// <returns>找到的区块，如果不存在则返回 null</returns>
    public TerrainChunk? GetNextChunk(int chunkX, int chunkZ)
    {
        var terrainChunk = GetChunkAtCoords(chunkX, chunkZ);
        if (terrainChunk != null)
        {
            return terrainChunk;
        }

        var allocatedChunks = AllocatedChunks;
        foreach (var chunk in allocatedChunks)
        {
            if (ComparePoints(chunk.Coords, new Point2(chunkX, chunkZ)) >= 0 && (terrainChunk == null ||
                    ComparePoints(chunk.Coords, terrainChunk.Coords) < 0))
            {
                terrainChunk = chunk;
            }
        }

        if (terrainChunk != null)
        {
            return terrainChunk;
        }

        foreach (var chunk in allocatedChunks)
        {
            if (terrainChunk == null || ComparePoints(chunk.Coords, terrainChunk.Coords) < 0)
            {
                terrainChunk = chunk;
            }
        }

        return terrainChunk;
    }

    /// <summary>
    /// 根据区块坐标获取区块
    /// </summary>
    /// <param name="chunkX">区块 X 坐标</param>
    /// <param name="chunkZ">区块 Z 坐标</param>
    /// <param name="throwIfNull">如果不存在是否抛出异常</param>
    /// <returns>找到的区块</returns>
    public TerrainChunk? GetChunkAtCoords(int chunkX, int chunkZ, bool throwIfNull = false)
    {
        return _allChunks.Get(chunkX, chunkZ, throwIfNull);
    }

    /// <summary>
    /// 根据单元格坐标获取所在区块
    /// </summary>
    /// <param name="x">单元格 X 坐标</param>
    /// <param name="z">单元格 Z 坐标</param>
    /// <param name="throwIfNull">如果不存在是否抛出异常</param>
    /// <returns>找到的区块</returns>
    public TerrainChunk? GetChunkAtCell(int x, int z, bool throwIfNull = true)
    {
        return GetChunkAtCoords(x >> 4, z >> 4, throwIfNull);
    }

    /// <summary>
    /// 根据三维单元格坐标获取所在区块
    /// </summary>
    /// <param name="x">单元格 X 坐标</param>
    /// <param name="y">单元格 Y 坐标</param>
    /// <param name="z">单元格 Z 坐标</param>
    /// <returns>找到的区块，如果 Y 坐标超出范围则返回 null</returns>
    public TerrainChunk? GetChunkAtCell(int x, int y, int z)
    {
        return y is >= 0 and < _height ? _allChunks.Get(x >> _chunkRegionCoordsShift, z >> _chunkRegionCoordsShift) : null;
    }

    /// <summary>
    /// 分配一个新的区块
    /// </summary>
    /// <param name="chunkX">区块 X 坐标</param>
    /// <param name="chunkZ">区块 Z 坐标</param>
    /// <returns>新分配的区块</returns>
    /// <exception cref="InvalidOperationException">如果该坐标区块已存在则抛出异常</exception>
    public TerrainChunk AllocateChunk(int chunkX, int chunkZ)
    {
        if (GetChunkAtCoords(chunkX, chunkZ) != null)
        {
            throw new InvalidOperationException("Chunk already allocated.");
        }

        var terrainChunk = new TerrainChunk(this, chunkX, chunkZ);
        _allocatedChunks.Add(terrainChunk);
        _allChunks.Add(chunkX, chunkZ, terrainChunk);
        _allocatedChunksArray = [];
        return terrainChunk;
    }

    /// <summary>
    /// 释放指定区块
    /// </summary>
    /// <param name="chunk">要释放的区块</param>
    /// <exception cref="InvalidOperationException">如果该区块未被分配则抛出异常</exception>
    public void FreeChunk(TerrainChunk chunk)
    {
        if (!_allocatedChunks.Remove(chunk))
        {
            throw new InvalidOperationException("Chunk not allocated.");
        }

        _allChunks.Remove(chunk.Coords.X, chunk.Coords.Y);
        _allocatedChunksArray = [];
    }

    /// <summary>
    /// 比较两个点的坐标顺序（先比较 Y，再比较 X）
    /// </summary>
    /// <param name="c1">第一个点</param>
    /// <param name="c2">第二个点</param>
        /// <returns>差值，负数表示 c1 &lt; c2，正数表示 c1 &gt; c2</returns>
    public static int ComparePoints(Point2 c1, Point2 c2)
    {
        if (c1.Y == c2.Y)
        {
            return c1.X - c2.X;
        }

        return c1.Y - c2.Y;
    }

    /// <summary>
    /// 将浮点坐标转换为区块坐标
    /// </summary>
    public static Point2 ToChunk(Vector2 p)
    {
        return ToChunk(ToCell(p.X), ToCell(p.Y));
    }

    /// <summary>
    /// 将单元格坐标转换为区块坐标
    /// </summary>
    public static Point2 ToChunk(int x, int z)
    {
        return new Point2(x >> _chunkRegionCoordsShift, z >> _chunkRegionCoordsShift);
    }

    /// <summary>
    /// 将浮点坐标向下取整为单元格坐标
    /// </summary>
    public static int ToCell(float x)
    {
        return (int)MathUtils.Floor(x);
    }

    /// <summary>
    /// 将浮点坐标转换为二维单元格坐标
    /// </summary>
    public static Point2 ToCell(float x, float y)
    {
        return new Point2((int)MathUtils.Floor(x), (int)MathUtils.Floor(y));
    }

    /// <summary>
    /// 将二维向量转换为单元格坐标
    /// </summary>
    public static Point2 ToCell(Vector2 p)
    {
        return new Point2((int)MathUtils.Floor(p.X), (int)MathUtils.Floor(p.Y));
    }

    /// <summary>
    /// 将浮点坐标转换为三维单元格坐标
    /// </summary>
    public static Point3 ToCell(float x, float y, float z)
    {
        return new Point3((int)MathUtils.Floor(x), (int)MathUtils.Floor(y), (int)MathUtils.Floor(z));
    }

    /// <summary>
    /// 将三维向量转换为单元格坐标
    /// </summary>
    public static Point3 ToCell(Vector3 p)
    {
        return new Point3((int)MathUtils.Floor(p.X), (int)MathUtils.Floor(p.Y), (int)MathUtils.Floor(p.Z));
    }

    /// <summary>
    /// 检查单元格坐标是否有效（Y坐标范围检查）
    /// </summary>
    public bool IsCellValid(int x, int y, int z)
    {
        if (y >= 0)
        {
            return y < _height;
        }

        return false;
    }

    /// <summary>
    /// 获取单元格的值（带边界检查）
    /// </summary>
    public int GetCellValue(int x, int y, int z)
    {
        return !IsCellValid(x, y, z) ? 0 : GetCellValueFast(x, y, z);
    }

    /// <summary>
    /// 获取单元格的内容（带边界检查）
    /// </summary>
    public int GetCellContents(int x, int y, int z)
    {
        return !IsCellValid(x, y, z) ? 0 : GetCellContentsFast(x, y, z);
    }

    /// <summary>
    /// 获取单元格的光照值（带边界检查）
    /// </summary>
    public int GetCellLight(int x, int y, int z)
    {
        return !IsCellValid(x, y, z) ? 0 : GetCellLightFast(x, y, z);
    }

    /// <summary>
    /// 快速获取单元格的值（无边界检查）
    /// </summary>
    public int GetCellValueFast(int x, int y, int z)
    {
        return GetChunkAtCell(x, z, false)?.GetCellValueFast(x & _chunkCoordsMask, y, z & _chunkCoordsMask) ?? 0;
    }

    /// <summary>
    /// 快速获取单元格的值（假设区块已存在）
    /// </summary>
    public int GetCellValueFastChunkExists(int x, int y, int z)
    {
        return GetChunkAtCell(x, z)!.GetCellValueFast(x & _chunkCoordsMask, y, z & _chunkCoordsMask);
    }

    /// <summary>
    /// 快速获取单元格的内容类型
    /// </summary>
    public int GetCellContentsFast(int x, int y, int z)
    {
        return ExtractContents(GetCellValueFast(x, y, z));
    }

    /// <summary>
    /// 快速获取单元格的光照值
    /// </summary>
    public int GetCellLightFast(int x, int y, int z)
    {
        return ExtractLight(GetCellValueFast(x, y, z));
    }

    /// <summary>
    /// 快速设置单元格的值
    /// </summary>
    public void SetCellValueFast(int x, int y, int z, int value)
    {
        GetChunkAtCell(x, z, false)?.SetCellValueFast(x & _chunkCoordsMask, y, z & _chunkCoordsMask, value);
    }

    /// <summary>
    /// 计算指定列的最高单元格高度
    /// </summary>
    public int CalculateTopmostCellHeight(int x, int z)
    {
        return GetChunkAtCell(x, z, false)?.CalculateTopmostCellHeight(x & _chunkCoordsMask, z & _chunkCoordsMask) ?? 0;
    }

    /// <summary>
    /// 获取列数据（Shaft）的原始值
    /// </summary>
    /// <remarks>Shaft 存储了该列的温度、湿度、高度等信息</remarks>
    public long GetShaftValue(int x, int z)
    {
        return GetChunkAtCell(x, z, false)?.GetShaftValueFast(x & _chunkCoordsMask, z & _chunkCoordsMask) ?? 0;
    }

    /// <summary>
    /// 设置列数据（Shaft）的原始值
    /// </summary>
    public void SetShaftValue(int x, int z, long value)
    {
        GetChunkAtCell(x, z, false)?.SetShaftValueFast(x & _chunkCoordsMask, z & _chunkCoordsMask, value);
    }

    /// <summary>
    /// 获取指定位置的温度值
    /// </summary>
    public int GetTemperature(int x, int z)
    {
        return ExtractTemperature(GetShaftValue(x, z));
    }

    /// <summary>
    /// 设置指定位置的温度值
    /// </summary>
    public void SetTemperature(int x, int z, int temperature)
    {
        SetShaftValue(x, z, ReplaceTemperature(GetShaftValue(x, z), temperature));
    }

    /// <summary>
    /// 获取指定位置的湿度值
    /// </summary>
    public int GetHumidity(int x, int z)
    {
        return ExtractHumidity(GetShaftValue(x, z));
    }

    /// <summary>
    /// 设置指定位置的湿度值
    /// </summary>
    public void SetHumidity(int x, int z, int humidity)
    {
        SetShaftValue(x, z, ReplaceHumidity(GetShaftValue(x, z), humidity));
    }

    /// <summary>
    /// 获取指定列的最高方块高度
    /// </summary>
    public int GetTopHeight(int x, int z)
    {
        return ExtractTopHeight(GetShaftValue(x, z));
    }

    /// <summary>
    /// 设置指定列的最高方块高度
    /// </summary>
    public void SetTopHeight(int x, int z, int topHeight)
    {
        SetShaftValue(x, z, ReplaceTopHeight(GetShaftValue(x, z), topHeight));
    }

    /// <summary>
    /// 获取指定列的最低方块高度
    /// </summary>
    public int GetBottomHeight(int x, int z)
    {
        return ExtractBottomHeight(GetShaftValue(x, z));
    }

    /// <summary>
    /// 设置指定列的最低方块高度
    /// </summary>
    public void SetBottomHeight(int x, int z, int bottomHeight)
    {
        SetShaftValue(x, z, ReplaceBottomHeight(GetShaftValue(x, z), bottomHeight));
    }

    /// <summary>
    /// 获取指定列的阳光能到达的最高高度
    /// </summary>
    public int GetSunlightHeight(int x, int z)
    {
        return ExtractSunlightHeight(GetShaftValue(x, z));
    }

    /// <summary>
    /// 设置指定列的阳光能到达的最高高度
    /// </summary>
    public void SetSunlightHeight(int x, int z, int sunlightHeight)
    {
        SetShaftValue(x, z, ReplaceSunlightHeight(GetShaftValue(x, z), sunlightHeight));
    }

    /// <summary>
    /// 创建方块值（仅包含内容类型）
    /// </summary>
    /// <param name="contents">方块内容类型 ID</param>
    public static int MakeBlockValue(int contents)
    {
        return contents & _contentsMask;
    }

    /// <summary>
    /// 创建完整的方块值
    /// </summary>
    /// <param name="contents">方块内容类型 ID（10位）</param>
    /// <param name="light">光照值（4位）</param>
    /// <param name="data">方块数据（18位）</param>
    public static int MakeBlockValue(int contents, int light, int data)
    {
        return (contents & _contentsMask) | ((light << _lightShift) & _lightMask) | ((data << _dataShift) & _dataMask);
    }

    /// <summary>
    /// 从方块值中提取内容类型 ID
    /// </summary>
    public static int ExtractContents(int value)
    {
        return value & _contentsMask;
    }

    /// <summary>
    /// 从方块值中提取光照值
    /// </summary>
    public static int ExtractLight(int value)
    {
        return (value & _lightMask) >> _lightShift;
    }

    /// <summary>
    /// 从方块值中提取数据
    /// </summary>
    public static int ExtractData(int value)
    {
        return (value & _dataMask) >> _dataShift;
    }

    /// <summary>
    /// 从 Shaft 值中提取最高方块高度
    /// </summary>
    /// <param name="value">Shaft 数据值</param>
    /// <param name="topHeightMask">最高高度位掩码</param>
    /// <returns>最高方块高度</returns>
    public static int ExtractTopHeight(long value, long topHeightMask = _topHeightMask)
    {
        return (int)((value & topHeightMask) >> _topHeightShift);
    }

    /// <summary>
    /// 从 Shaft 值中提取最低方块高度
    /// </summary>
    /// <param name="value">Shaft 数据值</param>
    /// <param name="bottomHeightMask">最低高度位掩码</param>
    /// <param name="bottomHeightShift">最低高度位偏移</param>
    /// <returns>最低方块高度</returns>
    public static int ExtractBottomHeight(long value, long bottomHeightMask = _bottomHeightMask,
        int bottomHeightShift = _bottomHeightShift)
    {
        return (int)((value & bottomHeightMask) >> bottomHeightShift);
    }

    /// <summary>
    /// 从 Shaft 值中提取阳光照射高度
    /// </summary>
    /// <param name="value">Shaft 数据值</param>
    /// <param name="sunlightHeightMask">阳光高度位掩码</param>
    /// <param name="sunlightHeightShift">阳光高度位偏移</param>
    /// <returns>阳光能到达的最高高度</returns>
    public static int ExtractSunlightHeight(long value, long sunlightHeightMask = _sunlightHeightMask,
        int sunlightHeightShift = _sunlightHeightShift)
    {
        return (int)((value & sunlightHeightMask) >> sunlightHeightShift);
    }

    /// <summary>
    /// 从 Shaft 值中提取湿度值
    /// </summary>
    /// <param name="value">Shaft 数据值</param>
    /// <param name="humidityMask">湿度位掩码</param>
    /// <param name="humidityShift">湿度位偏移</param>
    /// <returns>湿度值</returns>
    public static int ExtractHumidity(long value, long humidityMask = _humidityMask, int humidityShift = _humidityShift)
    {
        return (int)((value & humidityMask) >> humidityShift);
    }

    /// <summary>
    /// 从 Shaft 值中提取温度值
    /// </summary>
    /// <param name="value">Shaft 数据值</param>
    /// <param name="temperatureMask">温度位掩码</param>
    /// <param name="temperatureShift">温度位偏移</param>
    /// <returns>温度值</returns>
    public static int ExtractTemperature(long value, long temperatureMask = _temperatureMask,
        int temperatureShift = _temperatureShift)
    {
        return (int)((value & temperatureMask) >> temperatureShift);
    }

    /// <summary>
    /// 替换方块值中的内容类型
    /// </summary>
    public static int ReplaceContents(int value, int contents)
    {
        return value ^ ((value ^ contents) & _contentsMask);
    }

    /// <summary>
    /// 替换方块值中的光照值
    /// </summary>
    public static int ReplaceLight(int value, int light)
    {
        return value ^ ((value ^ (light << _lightShift)) & _lightMask);
    }

    /// <summary>
    /// 替换方块值中的数据
    /// </summary>
    public static int ReplaceData(int value, int data)
    {
        return value ^ ((value ^ (data << _dataShift)) & _dataMask);
    }

    /// <summary>
    /// 替换 Shaft 值中的最高高度
    /// </summary>
    /// <param name="value">原始 Shaft 值</param>
    /// <param name="topHeight">新的最高高度</param>
    /// <returns>更新后的 Shaft 值</returns>
    public static long ReplaceTopHeight(long value, int topHeight)
    {
        return value ^ ((value ^ (topHeight << _topHeightShift)) & _topHeightMask);
    }

    /// <summary>
    /// 替换 Shaft 值中的最低高度
    /// </summary>
    /// <param name="value">原始 Shaft 值</param>
    /// <param name="bottomHeight">新的最低高度</param>
    /// <returns>更新后的 Shaft 值</returns>
    public static long ReplaceBottomHeight(long value, int bottomHeight)
    {
        return value ^ ((value ^ (bottomHeight << _bottomHeightShift)) & _bottomHeightMask);
    }

    /// <summary>
    /// 替换 Shaft 值中的阳光高度
    /// </summary>
    /// <param name="value">原始 Shaft 值</param>
    /// <param name="sunlightHeight">新的阳光高度</param>
    /// <returns>更新后的 Shaft 值</returns>
    public static long ReplaceSunlightHeight(long value, int sunlightHeight)
    {
        return value ^ ((value ^ ((long)sunlightHeight << _sunlightHeightShift)) & _sunlightHeightMask);
    }

    /// <summary>
    /// 替换 Shaft 值中的湿度
    /// </summary>
    /// <param name="value">原始 Shaft 值</param>
    /// <param name="humidity">新的湿度值</param>
    /// <returns>更新后的 Shaft 值</returns>
    public static long ReplaceHumidity(long value, int humidity)
    {
        return value ^ ((value ^ (humidity << _humidityShift)) & _humidityMask);
    }

    /// <summary>
    /// 替换 Shaft 值中的温度
    /// </summary>
    /// <param name="value">原始 Shaft 值</param>
    /// <param name="temperature">新的温度值</param>
    /// <returns>更新后的 Shaft 值</returns>
    public static long ReplaceTemperature(long value, int temperature)
    {
        return value ^ ((value ^ (temperature << _temperatureShift)) & _temperatureMask);
    }

    /// <summary>
    /// 获取指定位置的含季节偏移的温度值
    /// </summary>
    public int GetSeasonalTemperature(int x, int z)
    {
        return GetTemperature(x, z) + SeasonTemperature;
    }

    /// <summary>
    /// 从 Shaft 值获取含季节偏移的温度值
    /// </summary>
    public int GetSeasonalTemperature(long shaftValue)
    {
        return ExtractTemperature(shaftValue) + SeasonTemperature;
    }

    /// <summary>
    /// 获取指定位置的含季节偏移的湿度值
    /// </summary>
    public int GetSeasonalHumidity(int x, int z)
    {
        return GetHumidity(x, z) + SeasonHumidity;
    }

    /// <summary>
    /// 从 Shaft 值获取含季节偏移的湿度值
    /// </summary>
    public int GetSeasonalHumidity(long shaftValue)
    {
        return ExtractHumidity(shaftValue) + SeasonHumidity;
    }

    /// <summary>
    /// 区块存储器（使用开放寻址法实现的哈希表）
    /// </summary>
    private class ChunksStorage
    {
        /// <summary>
        /// 坐标位移位数（用于哈希计算）
        /// </summary>
        private const int _shift = 8;

        /// <summary>
        /// 存储容量（固定大小）
        /// </summary>
        private const int _capacity = 65536;

        /// <summary>
        /// 容量掩码（用于快速取模）
        /// </summary>
        private const int _capacityMinusOne = 0xFFFF;

        /// <summary>
        /// 存储数组
        /// </summary>
        private readonly TerrainChunk?[] _array = new TerrainChunk[_capacity];

        /// <summary>
        /// 根据坐标获取区块
        /// </summary>
        /// <param name="x">区块 X 坐标</param>
        /// <param name="y">区块 Y（Z）坐标</param>
        /// <param name="throwIfNull">如果不存在是否抛出异常</param>
        /// <returns>找到的区块</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TerrainChunk? Get(int x, int y, bool throwIfNull = false)
        {
            var index = (x + (y << _shift)) & _capacityMinusOne;
            TerrainChunk? terrainChunk;
            while (true)
            {
                terrainChunk = _array[index];
                if (terrainChunk == null)
                {
                    return throwIfNull ? throw new InvalidOperationException("TerrainChunk not found") : null;
                }

                if (terrainChunk.Coords.X == x && terrainChunk.Coords.Y == y)
                {
                    break;
                }

                index = (index + 1) & _capacityMinusOne;
            }

            return terrainChunk;
        }

        /// <summary>
        /// 添加区块到存储
        /// </summary>
        public void Add(int x, int y, TerrainChunk chunk)
        {
            var index = (x + (y << _shift)) & _capacityMinusOne;
            while (_array[index] != null)
            {
                index = (index + 1) & _capacityMinusOne;
            }

            _array[index] = chunk;
        }

        /// <summary>
        /// 从存储中移除区块
        /// </summary>
        public void Remove(int x, int y)
        {
            var index = (x + (y << _shift)) & _capacityMinusOne;
            while (true)
            {
                var terrainChunk = _array[index];
                if (terrainChunk == null)
                {
                    return;
                }

                if (terrainChunk.Coords.X == x && terrainChunk.Coords.Y == y)
                {
                    break;
                }

                index = (index + 1) & _capacityMinusOne;
            }

            _array[index]?.Dispose();
            _array[index] = null;
        }
    }
}
