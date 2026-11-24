namespace Game.Modding;

/// <summary>在包共享的程序集作用域内创建显式指定名称的入口点。</summary>
public interface IModEntrypointLoader
{
    Type? FindLoadedType(string typeName);

    T CreateEntrypoint<T>(string entrypoint) where T : class;
}
