using System.Collections.Generic;
using UnityEngine;
using GosipSimulator.Core;

namespace GosipSimulator.Npcs
{
    /// <summary>
    /// The adapter of the module: it knows which NPCs are in the scene, and it is the one thing that
    /// turns "something happened over there" into "these people saw it". The rule itself lives in
    /// PerceptionResolver and stays plain C# (R5).
    ///
    /// It never learns what a witness does with what they saw. One OnActionWitnessed goes out per
    /// person, and whether that becomes an opinion, a rumor or nothing at all is Gossip's business
    /// (R4). It equally never learns who committed the action beyond the id in the event.
    /// </summary>
    public class NpcRegistry : MonoBehaviour
    {
        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("Village")]
        [Tooltip("Every NPC that can witness something. Assigned here rather than found at runtime (R6).")]
        [SerializeField] private Npc[] _npcs;

        #endregion

        private readonly PerceptionResolver _resolver = new PerceptionResolver();

        // Rebuilt per action rather than cached, because NPCs move and a cached position would make
        // perception answer for where somebody used to be.
        private readonly List<PerceptionResolver.Candidate> _candidates
            = new List<PerceptionResolver.Candidate>();

        private bool _isSubscribed;

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>How many NPCs this registry is watching. For the tests and the HUD.</summary>
        public int Count => _npcs != null ? _npcs.Length : 0;

        #endregion

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            // Setting enabled makes Unity call OnDisable on the spot, before any OnEnable ran, which
            // is why OnDisable only undoes what _isSubscribed says actually happened (R9).
            if (!IsConfigured(out string problem))
            {
                Log.Error($"[NpcRegistry] {problem} Nobody will witness anything.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<OnActionCommitted>(HandleActionCommitted);

            _isSubscribed = true;
        }

        private void OnDisable()
        {
            if (!_isSubscribed) return;

            EventBus.Unsubscribe<OnActionCommitted>(HandleActionCommitted);

            _isSubscribed = false;
        }

        #endregion

        // ────────────────────────────────
        // EVENT HANDLERS
        // ────────────────────────────────
        #region Event Handlers

        private void HandleActionCommitted(OnActionCommitted e)
        {
            if (string.IsNullOrWhiteSpace(e.actionId) || string.IsNullOrWhiteSpace(e.actorId))
            {
                // One malformed event costs one event. Throwing here would take down whoever
                // published it, which is the module that owns the player's verbs (R9).
                Log.Error($"[NpcRegistry] An action arrived with an empty id (action '{e.actionId}', " +
                          $"actor '{e.actorId}'). Nobody was asked whether they saw it.");
                return;
            }

            _candidates.Clear();

            for (int i = 0; i < _npcs.Length; i++)
            {
                Npc npc = _npcs[i];

                // A destroyed or disabled NPC is not a configuration error, it is somebody who left
                // or who Awake turned off for having no identity. They simply do not see anything.
                if (npc == null || !npc.isActiveAndEnabled) continue;

                _candidates.Add(new PerceptionResolver.Candidate
                {
                    npcId      = npc.Id,
                    position   = npc.transform.position,
                    sightRange = npc.SightRange
                });
            }

            IReadOnlyList<string> witnesses = _resolver.Resolve(e.position, e.actorId, _candidates);

            // Nothing published when nobody saw it. An unwitnessed theft is the point of the whole
            // system, not a case to report: it is what makes being unobserved worth something.
            if (witnesses.Count == 0)
            {
                Log.Trace($"[NpcRegistry] '{e.actionId}' by '{e.actorId}' was seen by nobody.");
                return;
            }

            // One event per witness rather than a list in one event, so a listener cannot miss part
            // of a batch. Nearest first, which the resolver guarantees.
            for (int i = 0; i < witnesses.Count; i++)
            {
                EventBus.Publish(new OnActionWitnessed
                {
                    actionId  = e.actionId,
                    actorId   = e.actorId,
                    witnessId = witnesses[i],
                    targetId  = e.targetId
                });
            }

            Log.Trace($"[NpcRegistry] '{e.actionId}' by '{e.actorId}' was seen by {witnesses.Count}.");
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private bool IsConfigured(out string problem)
        {
            problem = null;

            if (_npcs == null || _npcs.Length == 0)
            {
                problem = "No NPCs assigned.";
                return false;
            }

            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < _npcs.Length; i++)
            {
                if (_npcs[i] == null)
                {
                    problem = $"NPC slot {i} is not assigned.";
                    return false;
                }

                string id = _npcs[i].Id;

                // Npc.Awake already disables an NPC with no identity and says so. Reported here as
                // well because a slot pointing at a broken NPC is a scene problem, not an NPC one,
                // and the two are fixed in different places.
                if (string.IsNullOrWhiteSpace(id))
                {
                    problem = $"NPC '{_npcs[i].name}' has no id.";
                    return false;
                }

                // Two objects claiming the same id would both witness every action, so one theft
                // would move that NPC's opinion twice.
                if (!seen.Add(id))
                {
                    problem = $"NPC id '{id}' is claimed by more than one object in the scene.";
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
