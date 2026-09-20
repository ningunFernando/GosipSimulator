using System;
using System.Collections.Generic;

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
        public const int CURRENT_VERSION = 2;

        public int saveVersion = CURRENT_VERSION;

        public int currency;

        /// <summary>
        /// Lifetime accumulator. Monotonic by contract: spending never touches it (M6).
        /// </summary>
        public int totalEarned;

        /// <summary>
        /// What the village thinks of whom, added in v2. Sparse: only opinions that actually moved
        /// have a row, so a fresh game persists an empty list rather than a full matrix of zeros.
        /// Initialised here as well as in RelationshipStore because JsonUtility leaves a field the
        /// file does not mention at whatever the constructor set, and a v1 file mentions none.
        /// </summary>
        public List<RelationshipRow> relationships = new List<RelationshipRow>();
    }
}
