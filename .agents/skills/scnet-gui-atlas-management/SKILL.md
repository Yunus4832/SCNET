---
name: scnet-gui-atlas-management
description: Manage SCNET GUI atlas SVG and PNG source assets, import historical premultiplied images, generate the atlas, and validate visual or alpha-sensitive replacements. Use when adding, replacing, extracting, reorganizing, or diagnosing resources under GuiAtlasTool/Assets or the generated GUI atlas.
---

# SCNET GUI Atlas Management

Treat `GuiAtlasTool/Assets/` as the source of truth. `Content/Assets/Atlases/AtlasTexture.png` and
`Atlas.txt` are ignored build products; never edit or commit them as source assets.

## Choose the source format

- Prefer SVG for large controls, button frames, touch controls, and shapes that benefit from later
  geometric editing or scaling.
- Prefer PNG for small icons whose final-size pixel alignment, hinting, or antialiasing is part of
  their visual identity. This includes status symbols, cursors, compact flags, and small glyphs
  recovered from a visually verified atlas.
- A nominally large canvas does not make an icon suitable for SVG when the visible symbol occupies
  only a small area. Judge the rendered feature size.
- Do not keep SVG and PNG files with the same stem. The filename stem is the atlas entry name.

## Preserve the Alpha contract

Source PNG files must contain standard straight (unassociated) RGBA. The generator premultiplies
every SVG and PNG exactly once when writing the atlas.

- Normalize an ordinary straight-alpha PNG with `prepare-png`.
- Pass `--premultiplied` when importing a PNG recovered from an existing game atlas or another
  source known to contain premultiplied channels.
- Use `extract` for an existing generated atlas; it reads the mapping, crops requested entries, and
  automatically converts the premultiplied pixels back to straight RGBA.
- Do not manually copy a premultiplied crop into `Assets/`, and do not change the generator to accept
  mixed Alpha conventions.

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- prepare-png \
  input.png GuiAtlasTool/Assets/EntryName.png

dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- prepare-png \
  historical.png GuiAtlasTool/Assets/EntryName.png --premultiplied

dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- extract \
  AtlasTexture.png Atlas.txt GuiAtlasTool/Assets EntryName AnotherEntry
```

These commands refuse to replace files unless `--overwrite` is present. Resolve and inspect a
same-name source-format replacement before authorizing the overwrite; remove the superseded source
format in the same change.

## Generate and validate

Generate the atlas directly when focused inspection is useful:

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- generate \
  GuiAtlasTool/Assets \
  Content/Assets/Atlases/AtlasTexture.png \
  Content/Assets/Atlases/Atlas.txt
```

After changing sources or the tool:

1. Build `GuiAtlasTool` and generate the atlas.
2. Confirm the entry dimensions and mapping name are unchanged unless the change is intentional.
3. For a recovered PNG, compare the generated atlas crop with its premultiplied reference. Exact
   equality is preferred; a maximum channel difference of one can result from reversible integer
   rounding and is visually equivalent.
4. Inspect transparent edges, target-size line weight, normal/pressed pairs, and any attached-edge
   orientation affected by the asset.
5. Run `git diff --check`. When C# changed, also use `scnet-code-style` and proportional build
   validation.

Update `GuiAtlasTool/README.md`, `Doc/BuildAndConfig.md`, and `Directory.Build.targets` together when
the source contract, commands, paths, or build integration changes. Use `scnet-ui-design` as well
when the work changes layout, interaction semantics, or shared UI component behavior rather than
only atlas resource management.
