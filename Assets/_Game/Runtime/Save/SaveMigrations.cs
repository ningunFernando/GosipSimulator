using System;
using System.Collections.Generic;
using GosipSimulator.Core;

namespace GosipSimulator.Save
{
    /// <summary>
    /// The upgrade chain. Each entry turns one version into the next; Migrate walks them until
    /// CURRENT_VERSION. With version 1 the chain is empty, which is correct, not forgotten: there
    /// is nothing to migrate yet.
    /// </summary>
    public class SaveMigrations
    {
        private static readonly Dictionary<int, Func<SaveData, SaveData>> Migrations = new()
        {
            // [1] = data => { data.newField = defaultValue; data.saveVersion = 2; return data; },
        };

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>
        /// The upgraded data, or null when no path exists. Null also covers saves NEWER than
        /// CURRENT_VERSION: downgrading is never attempted. The caller decides what to do and
        /// nothing is ever deleted here (R14, M7).
        /// </summary>
        public SaveData Migrate(SaveData data)
        {
            while (data.saveVersion < SaveData.CURRENT_VERSION)
            {
                if (!Migrations.TryGetValue(data.saveVersion, out Func<SaveData, SaveData> step))
                {
                    Log.Error($"[SaveMigrations] No migration path from v{data.saveVersion}.");
                    return null;
                }

                data = step(data);
            }

            return data.saveVersion == SaveData.CURRENT_VERSION ? data : null;
        }

        #endregion
    }
}
