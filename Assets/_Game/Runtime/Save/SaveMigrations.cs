using System;
using System.Collections.Generic;
using GosipSimulator.Core;

namespace GosipSimulator.Save
{
    /// <summary>
    /// The upgrade chain. Each entry turns one version into the next; Migrate walks them until
    /// CURRENT_VERSION. A save is migrated and never replaced (R14, M7).
    /// </summary>
    public class SaveMigrations
    {
        private static readonly Dictionary<int, Func<SaveData, SaveData>> Migrations = new()
        {
            // v1 knew nothing about opinions. An empty list is the right v2 value and not a
            // placeholder: a village that was never played against has wronged nobody, so the
            // player loses nothing here. Currency and totalEarned are carried over untouched,
            // which is the whole point of migrating instead of starting fresh.
            [1] = data =>
            {
                data.relationships ??= new List<RelationshipRow>();
                data.saveVersion = 2;
                return data;
            },
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
