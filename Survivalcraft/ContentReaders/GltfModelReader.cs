using Engine.Graphics;
using Engine.Media.Models;

namespace Game.ContentReaders;

public sealed class GltfModelReader : IContentReader
{
    public override string Type => "Engine.Graphics.Model";

    public override string[] DefaultSuffix => ["glb", "gltf"];

    public override object Get(ContentInfo[] contents)
    {
        var content = contents[0];
        using var stream = content.Duplicate();
        return Model.Load(ModelData.Load(stream, uri => ContentManager.OpenRelativeResource(content, uri)), true);
    }
}
