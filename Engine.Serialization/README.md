# SCNET.Engine.Serialization

基于 `SCNET.Engine.Core` 的序列化库，面向 .NET 10，公共命名空间为 `Engine.Serialization`。

- `InputArchive`、`OutputArchive` 及类型序列化扩展。
- `BinaryInputArchive`、`BinaryOutputArchive` 提供二进制归档。
- `XmlInputArchive`、`XmlOutputArchive` 提供 XML 归档。
- 基础类型、集合及引擎数学类型的转换器与序列化支持。

不依赖窗口、游戏或原生库。具体数据格式和版本约定由消费方定义。

Core 数学类型通过 `Engine.Core.Mathematics` 引用，集合视图通过 `Engine.Core.Collections`
引用。序列化继续使用 `Vector3`、`Point3` 等类型短名，短名映射根据当前类型建立，
不依赖旧 Core 根命名空间。
