using System;
using System.IO;
using UnityEngine;
using GosipSimulator.Core;
using GosipSimulator.Core.State;

namespace GosipSimulator.Save
{
    /// <summary>
    /// The Unity adapter: owns the lifecycle, resolves persistentDataPath and reacts to pause and
    /// quit. Every rule lives in ProgressService and SaveMigrations; this class stays thin (R5, M6).
    /// </summary>
    public class SaveSystem : MonoBehaviour, ISaveLifecycle
    {
        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("Save")]
        [Tooltip("File name inside Application.persistentDataPath.")]
        [SerializeField] private string _fileName = "save.json";

        #endregion

        private ISaveStorage    _storage;
        private SaveMigrations  _migrations;
        private ProgressService _progress;
        private SaveData        _data;
        private bool            _dirty;

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(_fileName))
            {
                throw new InvalidOperationException("[SaveSystem] Save file name is empty.");
            }

            _storage = new JsonSaveStorage(Path.Combine(Application.persistentDataPath, _fileName));
            _migrations = new SaveMigrations();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<OnProgressChanged>(MarkDirty);
            EventBus.Subscribe<OnPickupCollected>(HandlePickupCollected);
            EventBus.Subscribe<OnGameStateChanged>(HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OnProgressChanged>(MarkDirty);
            EventBus.Unsubscribe<OnPickupCollected>(HandlePickupCollected);
            EventBus.Unsubscribe<OnGameStateChanged>(HandleGameStateChanged);
        }

        /// <summary>
        /// OnApplicationQuit is not reliable on Android and iOS; OnApplicationPause(true) is the
        /// primary trigger and quit is only a second chance (C6).
        /// </summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveIfDirty();
        }

        private void OnApplicationQuit()
        {
            SaveIfDirty();
        }

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>
        /// The access point for gameplay and for the debug HUD. The SaveData itself is never
        /// exposed: a public mutable CurrentData compiled in the reference and left no trace (M6).
        /// </summary>
        public ProgressService Progress => _progress;

        public void Load()
        {
            SaveData stored = _storage.Load();

            if (stored == null)
            {
                _data = new SaveData();
            }
            else if (stored.saveVersion != SaveData.CURRENT_VERSION)
            {
                _data = LoadMigrated(stored);
            }
            else
            {
                _data = stored;
            }

            _progress = new ProgressService(_data);
            _dirty = false;

            Log.Trace($"[SaveSystem] Loaded save v{_data.saveVersion} with currency {_data.currency}.");
        }

        public void Save()
        {
            if (_data == null)
            {
                // Loud but not fatal: this also runs from OnApplicationPause, where throwing
                // would interrupt the platform's own pause handling.
                Log.Error("[SaveSystem] Save called before Load. Nothing written.");
                return;
            }

            _storage.Save(_data);
            _dirty = false;

            Log.Trace($"[SaveSystem] Save written with currency {_data.currency}.");
        }

        #endregion

        // ────────────────────────────────
        // EVENT HANDLERS
        // ────────────────────────────────
        #region Event Handlers

        private void MarkDirty(OnProgressChanged e)
        {
            _dirty = true;
        }

        /// <summary>
        /// Save owns progress, so it is the module that turns a collected pickup into currency; the
        /// Pickups module only reports what happened (R4). Earn publishes OnProgressChanged, which
        /// marks the save dirty through MarkDirty like any other mutation.
        /// </summary>
        private void HandlePickupCollected(OnPickupCollected e)
        {
            if (_progress == null)
            {
                // Nothing was mutated, so the save stays consistent; the error names the ordering bug.
                Log.Error("[SaveSystem] Pickup collected before Load. Currency not granted.");
                return;
            }

            _progress.Earn(e.value);
        }

        /// <summary>
        /// Pausing is a checkpoint the player chose and, unlike quitting, it is delivered on every
        /// platform, so it is where the in-game save happens.
        /// </summary>
        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            if (e.newState == GameState.Paused) SaveIfDirty();
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private SaveData LoadMigrated(SaveData stored)
        {
            _storage.Backup(stored.saveVersion);

            SaveData migrated = _migrations.Migrate(stored);

            if (migrated == null)
            {
                Log.Error("[SaveSystem] Migration failed. Starting a new save; the old file is kept as .bak.");
                return new SaveData();
            }

            return migrated;
        }

        private void SaveIfDirty()
        {
            if (_dirty) Save();
        }

        #endregion
    }
}
