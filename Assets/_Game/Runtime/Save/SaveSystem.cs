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

        private ISaveStorage      _storage;
        private SaveMigrations    _migrations;
        private ProgressService   _progress;
        private RelationshipStore _relationships;
        private SaveData          _data;
        private bool              _dirty;

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
            EventBus.Subscribe<OnRelationshipChanged>(HandleRelationshipChanged);
            EventBus.Subscribe<OnBootstrapComplete>(HandleBootstrapComplete);
            EventBus.Subscribe<OnPurchaseApproved>(HandlePurchaseApproved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OnProgressChanged>(MarkDirty);
            EventBus.Unsubscribe<OnPickupCollected>(HandlePickupCollected);
            EventBus.Unsubscribe<OnGameStateChanged>(HandleGameStateChanged);
            EventBus.Unsubscribe<OnRelationshipChanged>(HandleRelationshipChanged);
            EventBus.Unsubscribe<OnBootstrapComplete>(HandleBootstrapComplete);
            EventBus.Unsubscribe<OnPurchaseApproved>(HandlePurchaseApproved);
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

        /// <summary>
        /// The persisted opinions, for the tests and for anything that has to read what is on file
        /// rather than what Gossip currently holds. Gossip stays the owner of the live value (R7).
        /// </summary>
        public RelationshipStore Relationships => _relationships;

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
            _relationships = new RelationshipStore(_data);
            _dirty = false;

            Log.Trace($"[SaveSystem] Loaded save v{_data.saveVersion} with currency {_data.currency} " +
                      $"and {_relationships.Count} opinions.");
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

        /// <summary>
        /// Save owns what is on disk, so it turns an opinion change into a persisted row without
        /// Gossip ever learning that a file exists, the same deal Pickups already has (R4).
        /// </summary>
        private void HandleRelationshipChanged(OnRelationshipChanged e)
        {
            if (_relationships == null)
            {
                Log.Error("[SaveSystem] Relationship changed before Load. The opinion was not persisted.");
                return;
            }

            if (!_relationships.Record(e.npcId, e.aboutId, e.current, e.reason))
            {
                Log.Error($"[SaveSystem] Refused an opinion row for npc '{e.npcId}' about '{e.aboutId}'. " +
                          "Not persisted.");
                return;
            }

            _dirty = true;
        }

        /// <summary>
        /// Hands Gossip what was on file, once the game scene is loaded and its objects have
        /// subscribed in OnEnable (R10). Pushed rather than pulled because Gossip cannot reference
        /// Save and Save cannot reference Gossip (R3), and pushing on this event is what makes the
        /// two independent of the order the bus happens to deliver handlers in.
        /// </summary>
        private void HandleBootstrapComplete(OnBootstrapComplete e)
        {
            if (_relationships == null)
            {
                Log.Error("[SaveSystem] Bootstrap completed before Load. No opinions were restored.");
                return;
            }

            // Published even when empty: a fresh save is a valid answer to "what did I have", and
            // staying silent would leave Gossip unable to tell an empty file from a missing one.
            EventBus.Publish(_relationships.ToRestoredPayload());

            Log.Trace($"[SaveSystem] Restored {_relationships.Count} opinions to the village.");
        }

        /// <summary>
        /// Save owns the currency, so it is the one that decides whether an approved purchase can be
        /// paid for. The shop agreed to a price without knowing the balance and Save takes it without
        /// knowing why it is that price (R4), the same deal as a pickup turning into currency, run
        /// backwards. TrySpend publishes OnProgressChanged on success, which marks the save dirty.
        /// </summary>
        private void HandlePurchaseApproved(OnPurchaseApproved e)
        {
            if (_progress == null)
            {
                Log.Error("[SaveSystem] Purchase approved before Load. Nothing was spent.");
                return;
            }

            if (e.price <= 0)
            {
                // PricingPolicy never produces this, and TrySpend would throw on it. Reported and
                // dropped, because a free sale is a bug in whoever approved it, not a sale (R9).
                Log.Error($"[SaveSystem] '{e.shopkeeperId}' approved '{e.itemId}' at {e.price}. Not charged.");
                return;
            }

            bool paid = _progress.TrySpend(e.price);

            // Settled either way. A purchase the player cannot afford has to end in an event too,
            // or pressing the key at the counter would look like the counter is broken.
            EventBus.Publish(new OnPurchaseSettled
            {
                shopkeeperId = e.shopkeeperId,
                customerId   = e.customerId,
                itemId       = e.itemId,
                price        = e.price,
                paid         = paid
            });

            Log.Trace($"[SaveSystem] '{e.itemId}' from '{e.shopkeeperId}' for {e.price}: " +
                      $"{(paid ? "paid" : "could not afford it")}. Currency {_data.currency}.");
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
