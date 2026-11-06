namespace Game.Modding;

/// <summary>Creates an explicitly named entrypoint in the package's shared assembly scope.</summary>
public interface IModEntrypointLoader
{
    T CreateEntrypoint<T>(string entrypoint) where T : class;
}
