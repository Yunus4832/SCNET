# SCNET.Engine.Serialization

基于 `SCNET.Engine.Core` 的序列化库，面向 .NET 10，公共命名空间为 `Engine.Serialization`。

- `InputArchive`、`OutputArchive` 及类型序列化扩展。
- `BinaryInputArchive`、`BinaryOutputArchive` 提供二进制归档。
- `XmlInputArchive`、`XmlOutputArchive` 提供 XML 归档。
- 基础类型、集合及引擎数学类型的转换器与序列化支持。

不依赖窗口、游戏或原生库。具体数据格式和版本约定由消费方定义。
