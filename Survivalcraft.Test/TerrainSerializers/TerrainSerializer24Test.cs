using System.IO.Compression;
using Engine.Core;
using Game;
using Game.Terrains;
using Game.TerrainSerializers;

namespace Survivalcraft.Test.TerrainSerializers;

public sealed class TerrainSerializer24Test
{
    [Fact]
    public void ReadToEndHandlesStreamsThatReturnPartialReads()
    {
        var source = Enumerable.Range(0, 4096).Select(i => (byte)i).ToArray();
        using var stream = new PartialReadStream(source, 17);
        var destination = new byte[source.Length];

        var count = TerrainSerializer24.ReadToEnd(stream, destination);

        Assert.Equal(source.Length, count);
        Assert.Equal(source, destination);
    }

    [Fact]
    public void SaveAndLoadRoundTripPreservesCompleteChunk()
    {
        var storage = new MemoryStorage();
        using var serializer = new TestTerrainSerializer24(storage);
        using var terrain = new Terrain();
        using var source = new TerrainChunk(terrain, 2, 3);
        source.MainThreadState = TerrainChunkState.Valid;

        for (var z = 0; z < 16; z++)
        for (var x = 0; x < 16; x++)
        {
            var shaft = Terrain.ReplaceHumidity(0, (x + z) & 15);
            shaft = Terrain.ReplaceTemperature(shaft, (x * 3 + z) & 15);
            source.SetShaftValueFast(x, z, shaft);
        }

        for (var y = 0; y < 256; y++)
        for (var z = 0; z < 16; z++)
        for (var x = 0; x < 16; x++)
        {
            var contents = 1 + (x + 17 * z + 31 * y) % 200;
            source.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(contents, (x + y) & 15, z & 3));
        }

        source.ModificationCounter = 1;
        serializer.SaveChunk(source);

        using var target = new TerrainChunk(terrain, 2, 3);
        Assert.True(serializer.LoadChunk(target));
        for (var i = 0; i < source.Cells.Length; i++)
        {
            Assert.Equal(Terrain.ReplaceLight(source.Cells[i], 0), target.Cells[i]);
        }

        for (var z = 0; z < 16; z++)
        for (var x = 0; x < 16; x++)
        {
            Assert.Equal(source.GetHumidityFast(x, z), target.GetHumidityFast(x, z));
            Assert.Equal(source.GetTemperatureFast(x, z), target.GetTemperatureFast(x, z));
        }
    }

    [Fact]
    public void LoadReturnsFalseWithoutApplyingMalformedChunk()
    {
        var malformedData = new byte[256];
        using var compressedStream = new MemoryStream();
        using (var deflateStream = new DeflateStream(compressedStream, CompressionLevel.Fastest, true))
        {
            deflateStream.Write(malformedData);
        }

        var storage = new MemoryStorage(compressedStream.ToArray());
        using var serializer = new TestTerrainSerializer24(storage);
        using var terrain = new Terrain();
        using var chunk = new TerrainChunk(terrain, 0, 0);
        chunk.SetCellValueFast(0, 0, 0, 1234);
        chunk.SetShaftValueFast(0, 0, 5678);

        Assert.False(serializer.LoadChunk(chunk));
        Assert.Equal(1234, chunk.GetCellValueFast(0, 0, 0));
        Assert.Equal(5678, chunk.GetShaftValueFast(0, 0));
    }

    [Fact]
    public void FailedSaveKeepsChunkDirtyForRetry()
    {
        var storage = new MemoryStorage { FailSaves = true };
        using var serializer = new TestTerrainSerializer24(storage);
        using var terrain = new Terrain();
        using var chunk = new TerrainChunk(terrain, 0, 0)
        {
            MainThreadState = TerrainChunkState.Valid,
            ModificationCounter = 1
        };

        serializer.SaveChunk(chunk);

        Assert.Equal(1, chunk.ModificationCounter);
    }

    [Fact]
    public void BackgroundSnapshotCanBeLoadedThroughSerializer()
    {
        var storage = new MemoryStorage();
        using var serializer = new TestTerrainSerializer24(storage);
        using var coordinator = new TerrainSaveCoordinator(serializer.SaveSnapshot);
        using var terrain = new Terrain();
        using var source = new TerrainChunk(terrain, 3, 4)
        {
            MainThreadState = TerrainChunkState.Valid,
            ModificationCounter = 1
        };
        source.SetCellValueFast(5, 60, 7, Terrain.MakeBlockValue(42, 0, 3));
        source.SetTemperatureFast(5, 7, 12);
        source.SetHumidityFast(5, 7, 9);

        Assert.True(coordinator.TryQueueChunkForUnload(source));
        coordinator.Flush();

        using var restored = new TerrainChunk(terrain, 3, 4);
        Assert.True(serializer.LoadChunk(restored));
        Assert.Equal(source.GetCellValueFast(5, 60, 7), restored.GetCellValueFast(5, 60, 7));
        Assert.Equal(12, restored.GetTemperatureFast(5, 7));
        Assert.Equal(9, restored.GetHumidityFast(5, 7));
    }

    [Fact]
    public async Task BackgroundSaveDoesNotBlockChunkDecompression()
    {
        var storage = new MemoryStorage();
        using var serializer = new TestTerrainSerializer24(storage);
        using var terrain = new Terrain();
        using var source = new TerrainChunk(terrain, 1, 2)
        {
            MainThreadState = TerrainChunkState.Valid,
            ModificationCounter = 1
        };
        source.SetCellValueFast(3, 40, 5, Terrain.MakeBlockValue(17));
        serializer.SaveChunk(source);

        using var saveStarted = new ManualResetEventSlim();
        using var allowSave = new ManualResetEventSlim();
        storage.SaveStarted = saveStarted;
        storage.AllowSave = allowSave;
        var saveTask = Task.Run(() => serializer.SaveSnapshot(
            new Point2(9, 9), source.Cells, source.Shafts));

        try
        {
            Assert.True(saveStarted.Wait(TimeSpan.FromSeconds(5)));
            using var target = new TerrainChunk(terrain, 1, 2);
            var loadTask = Task.Run(() => serializer.LoadChunk(target));
            var completedTask = await Task.WhenAny(loadTask, Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.Same(loadTask, completedTask);
            Assert.True(await loadTask);
            Assert.Equal(source.GetCellValueFast(3, 40, 5), target.GetCellValueFast(3, 40, 5));
        }
        finally
        {
            allowSave.Set();
            await saveTask;
        }
    }

    private sealed class TestTerrainSerializer24 : TerrainSerializer24
    {
        public TestTerrainSerializer24(IStorage replacementStorage)
        {
            storage.Dispose();
            storage = replacementStorage;
        }
    }

    private sealed class MemoryStorage(byte[]? initialData = null) : TerrainSerializer24.IStorage
    {
        private byte[]? _data = initialData;

        public ManualResetEventSlim? AllowSave { get; set; }

        public bool FailSaves { get; init; }

        public ManualResetEventSlim? SaveStarted { get; set; }

        public void Dispose()
        {
        }

        public void Open(string directoryName, string suffix = "")
        {
        }

        public int Load(Point2 coords, byte[] buffer)
        {
            if (_data == null)
            {
                return -1;
            }

            _data.CopyTo(buffer, 0);
            return _data.Length;
        }

        public void Save(Point2 coords, byte[] buffer, int size)
        {
            if (FailSaves)
            {
                throw new IOException("Simulated storage failure.");
            }

            SaveStarted?.Set();
            if (AllowSave != null && !AllowSave.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Timed out waiting to complete the test save.");
            }

            _data = buffer.AsSpan(0, size).ToArray();
        }

        public bool Exists(Point2 coords)
        {
            return _data != null;
        }
    }

    private sealed class PartialReadStream(byte[] buffer, int maxReadSize) : MemoryStream(buffer)
    {
        public override int Read(byte[] destination, int offset, int count)
        {
            return base.Read(destination, offset, Math.Min(count, maxReadSize));
        }
    }
}
