using Engine.Graphics;

namespace Game.Terrains;

public class TerrainChunkGeometry
{
    public class Buffer : IDisposable
    {
        public required IndexBuffer IndexBuffer;

        public readonly int[] SubsetIndexBufferEnds = new int[7];

        public readonly int[] SubsetIndexBufferStarts = new int[7];

        public int[] SubsetVertexBufferStarts = new int[7];

        public required Texture2D Texture;

        public required VertexBuffer VertexBuffer;

        internal VertexBuffer? LightingBuffer { get; set; }

        internal (int Begin, int End) VertexRange { get; init; }

        public void Dispose()
        {
            LightingBuffer?.Dispose();
            LightingBuffer = null;
            VertexBuffer?.Dispose();
            VertexBuffer = null!;
            IndexBuffer?.Dispose();
            IndexBuffer = null!;
        }
    }
}
