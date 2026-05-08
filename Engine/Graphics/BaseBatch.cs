using Engine.Core;

namespace Engine.Graphics;

public abstract class BaseBatch
{
    public int Layer { get; set; }

    public DepthStencilState DepthStencilState { get; set; } = DepthStencilState.None;

    public RasterizerState RasterizerState { get; set; } =  RasterizerState.CullNoneScissor;

    public BlendState BlendState { get; set; } = BlendState.Opaque;

    public abstract bool IsEmpty();

    public abstract void Clear();

    public abstract void Flush(Matrix matrix, Vector4 color, bool clearAfterFlush = true);
}
