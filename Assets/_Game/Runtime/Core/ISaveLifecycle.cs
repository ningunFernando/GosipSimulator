namespace GosipSimulator.Core
{
    /// <summary>
    /// The only contract Core uses to drive the save system. It exists because R3 forbids Core
    /// from referencing the Save assembly: the Bootstrapper instantiates the SaveSystem prefab
    /// as an untyped GameObject and resolves this interface, so a wrongly assigned prefab throws
    /// instead of silently never loading.
    /// </summary>
    public interface ISaveLifecycle
    {
        void Load();

        void Save();
    }
}
