# Blender glTF regression fixtures

These four small, original fixtures were authored with Blender 5.2.0 LTS and exported directly
through its standard glTF 2.0 exporter. They cover shared meshes and transformed parents,
multiple materials, skinning/animation, and a shape key with a nonzero default weight.
They contain no third-party assets and are covered by the repository license.

Regenerate the GLB files with Blender's built-in Python API (no add-on required):

```sh
flatpak run --filesystem="$PWD/Engine.Test/TestAssets/Models" org.blender.Blender \
  --background --factory-startup \
  --python "$PWD/Engine.Test/TestAssets/Models/generate_blender_fixtures.py" \
  -- "$PWD/Engine.Test/TestAssets/Models"
```

The script writes only these fixtures and a temporary checker texture in the specified output
directory. Later Blender versions may produce different binary layouts; tests assert model
semantics rather than binary identity. The skin fixture has an identity armature parent;
Khronos Validator reports the informational warning `NODE_SKINNED_MESH_NON_ROOT` about the
standard rule that parent transforms do not affect skinned meshes.
