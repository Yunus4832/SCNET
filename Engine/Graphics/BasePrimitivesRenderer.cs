using Engine.Core;
using Engine.Media;

namespace Engine.Graphics;

public class BasePrimitivesRenderer<T1, T2, T3>
    where T1 : BaseFlatBatch, new()
    where T2 : BaseTexturedBatch, new()
    where T3 : BaseFontBatch, new()
{
    public readonly List<BaseBatch> AllBatches = [];

    private readonly LinkedList<T1> _flatBatches = [];

    private readonly LinkedList<T3> _fontBatches = [];

    public bool SortNeeded;

    private readonly LinkedList<T2> _texturedBatches = [];

    public IEnumerable<T1> FlatBatches => _flatBatches;

    public IEnumerable<T2> TexturedBatches => _texturedBatches;

    public IEnumerable<T3> FontBatches => _fontBatches;

    public T1 FindFlatBatch(int layer, DepthStencilState depthStencilState, RasterizerState rasterizerState,
        BlendState blendState)
    {
        for (var linkedListNode = _flatBatches.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
        {
            var value = linkedListNode.Value;
            if (layer != value.Layer ||
                depthStencilState != value.DepthStencilState ||
                rasterizerState != value.RasterizerState ||
                blendState != value.BlendState)
            {
                continue;
            }

            if (linkedListNode.Previous == null)
            {
                return value;
            }

            _flatBatches.Remove(linkedListNode);
            _flatBatches.AddFirst(linkedListNode);

            return value;
        }

        SortNeeded |= AllBatches.Count > 0 && AllBatches[^1].Layer > layer;
        var val = new T1
        {
            Layer = layer,
            DepthStencilState = depthStencilState,
            RasterizerState = rasterizerState,
            BlendState = blendState
        };
        _flatBatches.AddFirst(val);
        AllBatches.Add(val);
        return val;
    }

    public T2 FindTexturedBatch(
        Texture2D texture,
        bool useAlphaTest,
        int layer,
        DepthStencilState depthStencilState,
        RasterizerState rasterizerState,
        BlendState blendState,
        SamplerState samplerState
    )
    {
        ArgumentNullException.ThrowIfNull(texture);
        for (var linkedListNode = _texturedBatches.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
        {
            var value = linkedListNode.Value;
            if (texture != value.Texture ||
                useAlphaTest != value.UseAlphaTest ||
                layer != value.Layer ||
                depthStencilState != value.DepthStencilState ||
                rasterizerState != value.RasterizerState ||
                blendState != value.BlendState ||
                samplerState != value.SamplerState)
            {
                continue;
            }

            if (linkedListNode.Previous == null)
            {
                return value;
            }

            _texturedBatches.Remove(linkedListNode);
            _texturedBatches.AddFirst(linkedListNode);

            return value;
        }

        SortNeeded |= AllBatches.Count > 0 && AllBatches[^1].Layer > layer;
        var val = new T2
        {
            Layer = layer,
            UseAlphaTest = useAlphaTest,
            Texture = texture,
            SamplerState = samplerState,
            DepthStencilState = depthStencilState,
            RasterizerState = rasterizerState,
            BlendState = blendState
        };
        _texturedBatches.AddFirst(val);
        AllBatches.Add(val);
        return val;
    }

    public T3 FindFontBatch(BitmapFont font, int layer, DepthStencilState depthStencilState,
        RasterizerState rasterizerState, BlendState blendState, SamplerState samplerState)
    {
        for (var linkedListNode = _fontBatches.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
        {
            var value = linkedListNode.Value;
            if (font != value.Font ||
                layer != value.Layer ||
                depthStencilState != value.DepthStencilState ||
                rasterizerState != value.RasterizerState ||
                blendState != value.BlendState ||
                samplerState != value.SamplerState)
            {
                continue;
            }

            if (linkedListNode.Previous == null)
            {
                return value;
            }

            _fontBatches.Remove(linkedListNode);
            _fontBatches.AddFirst(linkedListNode);

            return value;
        }

        SortNeeded |= AllBatches.Count > 0 && AllBatches[^1].Layer > layer;
        var val = new T3
        {
            Layer = layer,
            Font = font,
            SamplerState = samplerState,
            DepthStencilState = depthStencilState,
            RasterizerState = rasterizerState,
            BlendState = blendState
        };
        _fontBatches.AddFirst(val);
        AllBatches.Add(val);
        return val;
    }

    public void Flush(Matrix matrix, bool clearAfterFlush = true, int maxLayer = int.MaxValue)
    {
        Flush(matrix, Vector4.One, clearAfterFlush, maxLayer);
    }

    public void Flush(Matrix matrix, Vector4 color, bool clearAfterFlush = true, int maxLayer = int.MaxValue)
    {
        if (SortNeeded)
        {
            SortNeeded = false;
            AllBatches.Sort(delegate (BaseBatch b1, BaseBatch b2)
            {
                if (b1.Layer < b2.Layer)
                {
                    return -1;
                }

                return b1.Layer > b2.Layer ? 1 : 0;
            });
        }

        foreach (var allBatch in AllBatches)
        {
            if (allBatch.Layer > maxLayer)
            {
                break;
            }

            if (!allBatch.IsEmpty())
            {
                allBatch.Flush(matrix, color, clearAfterFlush);
            }
        }
    }

    public void Clear()
    {
        foreach (var allBatch in AllBatches)
        {
            allBatch.Clear();
        }
    }
}
