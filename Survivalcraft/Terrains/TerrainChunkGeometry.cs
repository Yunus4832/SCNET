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

        public void Dispose()
        {
            Utilities.Dispose(ref VertexBuffer!);
            Utilities.Dispose(ref IndexBuffer!);
        }
    }
}
