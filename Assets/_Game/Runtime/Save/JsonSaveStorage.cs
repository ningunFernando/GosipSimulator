using System;
using System.IO;
using UnityEngine;
using GosipSimulator.Core;

namespace GosipSimulator.Save
{
    /// <summary>
    /// JSON on disk. The path is injected, never resolved here, so this class stays free of
    /// Application.persistentDataPath and can be pointed at a temp folder from a test (R5).
    /// </summary>
    public class JsonSaveStorage : ISaveStorage
    {
        private readonly string _filePath;

        public JsonSaveStorage(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            _filePath = filePath;
        }

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public SaveData Load()
        {
            if (!File.Exists(_filePath)) return null;

            string json = File.ReadAllText(_filePath);

            try
            {
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (ArgumentException e)
            {
                // A corrupt file must not brick the game, and must not be deleted either (R14):
                // it is moved aside and the next Save writes a fresh one.
                Log.Error($"[JsonSaveStorage] Save file is corrupt and will be kept as .corrupt: {e.Message}");

                string corrupt = _filePath + ".corrupt";
                MoveOverwriting(_filePath, corrupt);
                return null;
            }
        }

        public void Save(SaveData data)
        {
            string json = JsonUtility.ToJson(data, true);

            // Transactional: write to .tmp then move. A direct WriteAllText can leave the file
            // truncated if the process dies mid-write, which on mobile is a real scenario (C6).
            // The 3-argument File.Move is not in this API compatibility level, so the target is
            // removed first: the final file is never partially written, and if the process dies
            // in between the .tmp still holds the payload.
            string tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            MoveOverwriting(tmp, _filePath);
        }

        public void Backup(int version)
        {
            if (!File.Exists(_filePath)) return;

            File.Copy(_filePath, $"{_filePath}.v{version}.bak", true);

            Log.Trace($"[JsonSaveStorage] Backup written for v{version}.");
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private static void MoveOverwriting(string source, string destination)
        {
            if (File.Exists(destination)) File.Delete(destination);

            File.Move(source, destination);
        }

        #endregion
    }
}
