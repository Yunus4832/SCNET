# SCNET.Content.Packaging

SCNET `.scpkg` 内容包的协议、读写与校验库，面向 .NET 10，命名空间为 `Content.Packaging`。

- `ContentPackageManifest`：内容类型、标识、版本、简介和说明。
- `ContentPackageReader.Inspect`：读取包、校验结构与载荷，并计算内容哈希。
- `ContentPackageWriter`：写入符合协议的内容包。
- `Content.Packaging.Payloads`：模组、世界、图片、家具包等载荷校验。

图片载荷接受静态 PNG 和 WebP，建议使用无损 WebP。扩展名、MIME、格式标识和实际编码须一致。
校验包括尺寸、大小、元数据和完整性。交错 PNG 可用。

图像处理由 `SCNET.Engine.Media` 提供，原生编解码库随 NuGet 依赖传递。命令行打包用 `SCNET.ContentTool`。

[内容包协议](https://gitee.com/Yunus0712/scnet/blob/main/Doc/Protocol/ContentPackageProtocol.md) · [ContentTool 命令](https://gitee.com/Yunus0712/scnet/blob/main/Tools/ContentTool/README.md)
