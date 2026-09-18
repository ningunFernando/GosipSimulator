using System;

namespace GosipSimulator.Save
{
    /// <summary>
    /// Everything that survives between sessions. Versioned from day one (R14): renaming or
    /// retyping a field changes how JsonUtility reads old files without any warning, so version
    /// plus explicit migrations is the only safe way to evolve this class.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CURRENT_VERSION = 1;

        public int saveVersion = CURRENT_VERSION;

        public int currency;

        /// <summary>
        /// Lifetime accumulator. Monotonic by contract: spending never touches it (M6).
        /// </summary>
        public int totalEarned;
    }
}
