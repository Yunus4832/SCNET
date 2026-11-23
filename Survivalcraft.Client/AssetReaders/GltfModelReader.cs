using Engine.Graphics;
using Engine.Media.Models;

namespace Game.AssetReaders;

public sealed class GltfModelReader : AssetReader
{
    public override string Type => "Engine.Graphics.Model";

    public override string[] DefaultSuffix => ["glb", "gltf"];

    public override object Get(AssetInfo[] contents)
    {
        var content = contents[0];
        using var stream = content.Duplicate();
        return Model.Load(ModelData.Load(stream, uri => AssetManager.OpenRelativeResource(content, uri)));
    }
}
