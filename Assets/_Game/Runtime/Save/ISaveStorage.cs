namespace GosipSimulator.Save
{
    /// <summary>
    /// Where the save lives. Zero game logic: read, write, keep a backup. A second implementation
    /// (cloud, encrypted) plugs in here without touching ProgressService or SaveSystem (R5).
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>
        /// The stored data, or null when there is no usable save yet.
        /// </summary>
        SaveData Load();

        void Save(SaveData data);

        /// <summary>
        /// Copies the current file aside as a versioned backup before it gets rewritten (R14).
        /// </summary>
        void Backup(int version);
    }
}
