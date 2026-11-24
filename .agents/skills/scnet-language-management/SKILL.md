---
name: scnet-language-management
description: Manage SCNET localization files through the repository C# LanguageTool. Use when adding, updating, removing, renaming, listing, reading, or validating keys in Assets/Shared/Lang/*.json, and avoid direct JSON text edits for localization changes.
---

# SCNET Language Management

Use the repository tool instead of manually editing `Assets/Shared/Lang/*.json`.

Run commands from the repository root. Use `rules` for key conventions and `overview . --depth 1`
to locate the section before inspecting individual keys.

## 玩家帮助的顺序与排版

- 目录按玩家查阅任务排列：基础操作与物品、生存与角色、采集制作与建造、联机与内容，最后是关于与免责声明。新增内置主题时在 `HelpTopic` 的阅读顺序中选定位置，保留现有数字键和用于直达页面的 `Name`。四种语言共用这个顺序。
- 页面标题使用 `Title`，由帮助界面居中显示并留出固定间距。正文各行居中、按屏宽自动折行；首尾不加空行，也不在正文重复标题。
- 一个主题或操作阶段写成一段。段内句子按本语言正常连接，段落之间空一行。列表项、逐行对照或必须逐行执行的步骤可用单换行。向 LanguageTool 传实际换行，JSON 序列化负责转义。
- 排版修改保留原有解释、例子、步骤、数值及警告。各语言按自身语义分段，无需强行对齐句数。动态“关于”页和专用控制对话框按各自组件排版。
- 纯排版改动须逐语言对照正文：忽略布局空白后文字应完全一致。用 `LanguageTool check` 核对键，再预览一篇长教程和一篇分步骤帮助，检查窄屏折行、段落和标题间距。

## Workflow

1. Inspect the key rules and nearby key structure before adding new ones:

```bash
dotnet run --project Tools/LanguageTool -- rules
dotnet run --project Tools/LanguageTool -- overview . --depth 1
dotnet run --project Tools/LanguageTool -- overview ContentWidgets --depth 2 --limit 120
dotnet run --project Tools/LanguageTool -- overview Help --culture all --depth 2 --limit 80
dotnet run --project Tools/LanguageTool -- children ContentWidgets.ModManagementScreen
dotnet run --project Tools/LanguageTool -- show ContentWidgets.ModManagementScreen --culture zh-CN --depth 1 --limit 80
dotnet run --project Tools/LanguageTool -- list --culture zh-CN --prefix ContentWidgets.ModManagementScreen
dotnet run --project Tools/LanguageTool -- get ContentWidgets.ModManagementScreen.Title --culture en-US
```

2. Search without opening large JSON files:

```bash
dotnet run --project Tools/LanguageTool -- search server --culture all --prefix ContentWidgets --limit 50
dotnet run --project Tools/LanguageTool -- search WorldServerSettings --culture en-US --in path
```

3. Audit structured sections across cultures without opening large JSON files:

```bash
dotnet run --project Tools/LanguageTool -- overview Help --culture all --depth 2 --limit 80
dotnet run --project Tools/LanguageTool -- table Help --fields Title,Name --limit 20
dotnet run --project Tools/LanguageTool -- table ContentWidgets.ModManagementScreen --fields value --cultures zh-CN,en-US
```

Use `overview` first when you need a level/depth map of a language subtree. It reports node kind, scalar fields, child keys, previews and structural issues. Use `.` or `/` for the root path.
Use `table` to compare numbered topics and metadata across languages. Table values are previews.
Use `get Help.<topic>.value --culture <locale>` to read the full help text before revising it.
For control instructions, check the input binding and help dialog for the relevant device.

4. Synchronize invariant metadata fields from the canonical language:

```bash
dotnet run --project Tools/LanguageTool -- sync-field Help Name --from zh-CN --remove-extra --dry-run
dotnet run --project Tools/LanguageTool -- sync-field Help Name --from zh-CN --remove-extra
```

Use `sync-field` for runtime IDs or non-translatable metadata such as `Help.*.Name`. Do not use it for localized display text such as `Title` or `value`.

5. Add or update one string key in all supported languages:

```bash
dotnet run --project Tools/LanguageTool -- set ContentWidgets.SomeScreen.SomeKey \
  --zh-CN 中文 \
  --en-US English \
  --pt-PT Português \
  --ru-RU Русский
```

6. Rename a key in all language files:

```bash
dotnet run --project Tools/LanguageTool -- rename OldScreen.OldKey NewScreen.NewKey
```

7. Remove a key from all language files:

```bash
dotnet run --project Tools/LanguageTool -- remove SomeScreen.ObsoleteKey
```

8. Always validate after a localization change:

```bash
dotnet run --project Tools/LanguageTool -- check
```

## Rules

- Do not directly edit language JSON for normal localization key changes.
- Prefer complete four-language updates. `set` requires all four languages unless `--allow-partial` is intentionally used.
- Use `overview` for level/depth navigation before opening large language files.
- Use `table` before reading large language files when diagnosing cross-language section mapping.
- Treat fields like `Help.*.Name` as invariant runtime identifiers; use `sync-field` to keep them identical across cultures.
- Use JSON path dots only as path separators, for example `ContentWidgets.PlayScreen.12` or `ContentWidgets.WorldServerSettingsScreen.RunServerDescription`.
- `ClientLanguage.Catalog.Get("PlayScreen", 12)` reads `PlayScreen.12`; it does not prepend `ContentWidgets`. To read `ContentWidgets.PlayScreen.12`, use `ClientLanguage.Catalog.GetContentWidgets("PlayScreen", 12)`.
- `ClientLanguage.Catalog.Get(section, key)` corresponds to `{section}.{key}`.
- `ClientLanguage.Catalog.GetContentWidgets(name, key)` corresponds to `ContentWidgets.{name}.{key}`.
- `Game.Localization.LanguageCatalog` owns language data in Core; client display code reads the published catalog through `Game.Localization.ClientLanguage.Catalog`.
- XML text like `[ScreenName:Key]` is resolved through `ContentWidgets.ScreenName.Key`.
- Numeric keys such as `12` are object keys, not array indexes. Array indexes use `[0]`.
- Do not create real JSON key names containing `.`. Use `_` for compound flat keys, for example `Strings.GameMode_Creative_Description`.
- Existing dotted key names are still addressable with escaped dots, but treat that as compatibility only.
- For `Strings` keys, prefer `StringsManager.GetString("GameMode", gameMode, "Description")` in C# instead of manually composing `GameMode_Creative_Description`.
- Keep UI code references aligned with language keys before finishing.
