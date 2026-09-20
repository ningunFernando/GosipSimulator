using System;
using System.Collections.Generic;
using UnityEngine;
using GosipSimulator.Core;
using GosipSimulator.Data;

namespace GosipSimulator.Gossip
{
    /// <summary>
    /// The adapter of the module, and the only part of Gossip that Unity knows about. It builds the
    /// domain from the configuration assets in Awake, feeds it the sightings that arrive on the bus,
    /// and drives its clock from Update. Every rule lives in GossipService, RumorPropagator and the
    /// two graphs, which stay plain C# and testable without a scene (R5).
    ///
    /// Nothing here reaches for another module: a sighting arrives as OnActionWitnessed and a loaded
    /// game as OnRelationshipsRestored, so this component never learns that Npcs or Save exist (R4).
    /// </summary>
    public class GossipManager : MonoBehaviour
    {
        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("Configuration")]
        [Tooltip("Opinion range, decay per hop, maximum hops and the delay between them.")]
        [SerializeField] private GossipConfigSO _config;

        [Header("Village")]
        [Tooltip("Every NPC that can hold an opinion or pass a story on. Their ties build the social graph.")]
        [SerializeField] private NpcDefinitionSO[] _npcs;

        [Header("Actions")]
        [Tooltip("Every action a sighting can report. The id is what OnActionWitnessed carries; the base delta is how hard it lands.")]
        [SerializeField] private ActionDefinitionSO[] _actions;

        #endregion

        // There is no OnValidate here on purpose: this component serializes no numbers of its own to
        // clamp, and the values that do need clamping belong to GossipConfigSO. What is left is
        // reference and id checking, which needs the whole set at once and happens in Awake (R8).

        private GossipService _service;

        // Built once from the action assets, because OnActionWitnessed carries only the id: Gossip
        // cannot reference the Actions assembly to ask what a robbery is worth (R3).
        private Dictionary<string, int> _deltaByActionId;

        private bool _isSubscribed;

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>
        /// The service this adapter owns, so the HUD and the tests can read the opinions. Reading
        /// only: the graph is still the single owner of every value in it (R7, R11).
        /// </summary>
        public GossipService Service => _service;

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
                Log.Error($"[GossipManager] {problem} Gossip is disabled.");
                enabled = false;
                return;
            }

            try
            {
                _service = BuildService();
            }
            catch (Exception e)
            {
                // The domain constructors are what know an inverted opinion range or a trust outside
                // 0 to 100, and they throw rather than repair it. Letting that escape Awake would
                // leave the component alive and half built, still subscribed to the bus with no
                // service behind it, so it becomes the same visible disable as any other bad config
                // (R9, A3).
                Log.Error($"[GossipManager] The configuration was rejected: {e.Message} Gossip is disabled.");
                enabled = false;
                return;
            }

            Log.Trace($"[GossipManager] Built from {_npcs.Length} NPCs and {_actions.Length} actions.");
        }

        private void OnEnable()
        {
            EventBus.Subscribe<OnActionWitnessed>(HandleActionWitnessed);
            EventBus.Subscribe<OnRelationshipsRestored>(HandleRelationshipsRestored);

            _isSubscribed = true;
        }

        private void Update()
        {
            if (_service == null) return;

            // Scaled time on purpose, the same as PickupSpawner: PausedState sets timeScale to zero,
            // and a story already on its way waits with it instead of arriving during a pause.
            _service.Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            if (!_isSubscribed) return;

            EventBus.Unsubscribe<OnActionWitnessed>(HandleActionWitnessed);
            EventBus.Unsubscribe<OnRelationshipsRestored>(HandleRelationshipsRestored);

            _isSubscribed = false;
        }

        #endregion

        // ────────────────────────────────
        // EVENT HANDLERS
        // ────────────────────────────────
        #region Event Handlers

        private void HandleActionWitnessed(OnActionWitnessed e)
        {
            // Checked here rather than left to the domain, which throws on an empty id: an exception
            // crossing the bus would take down whoever published the sighting, and one malformed
            // event should cost one event (R9).
            if (string.IsNullOrWhiteSpace(e.actionId)
                || string.IsNullOrWhiteSpace(e.actorId)
                || string.IsNullOrWhiteSpace(e.witnessId))
            {
                Log.Error($"[GossipManager] A sighting arrived with an empty id (action '{e.actionId}', " +
                          $"actor '{e.actorId}', witness '{e.witnessId}'). Ignored.");
                return;
            }

            if (!_deltaByActionId.TryGetValue(e.actionId, out int baseDelta))
            {
                // Without a definition there is no way to know how hard this should land. Dropping it
                // in silence would make a missing asset look like an NPC who simply did not mind.
                Log.Error($"[GossipManager] No action definition for '{e.actionId}'. The sighting is ignored.");
                return;
            }

            if (!_service.WitnessAction(e.actionId, e.actorId, e.witnessId, baseDelta))
            {
                // Today this can only mean the actor was reported as their own witness. That is a
                // perception bug upstream, and Gossip is not the layer that can repair it.
                Log.Warn($"[GossipManager] '{e.witnessId}' was reported as a witness to their own " +
                         $"'{e.actionId}'. Ignored.");
            }
        }

        private void HandleRelationshipsRestored(OnRelationshipsRestored e)
        {
            try
            {
                _service.Restore(e.npcIds, e.aboutIds, e.values);
            }
            catch (ArgumentException ex)
            {
                // A save whose three columns disagree is corrupt. The village keeps the graph it built
                // from configuration, which is a valid starting state, and the log says what happened
                // instead of leaving a silently empty village (R9).
                Log.Error($"[GossipManager] The saved opinions were rejected: {ex.Message} " +
                          "The village keeps what configuration gave it.");
                return;
            }

            Log.Trace($"[GossipManager] Restored {e.npcIds?.Length ?? 0} opinions from the save.");
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private GossipService BuildService()
        {
            var ties = new List<(string fromId, string toId, int trust)>();

            for (int i = 0; i < _npcs.Length; i++)
            {
                NpcDefinitionSO npc = _npcs[i];
                IReadOnlyList<SocialTie> npcTies = npc.Ties;

                if (npcTies == null) continue;

                for (int t = 0; t < npcTies.Count; t++)
                {
                    // Directed outwards from the owner: the asset lists who this NPC passes a story
                    // to, and that is the direction SocialGraph.TiesOf reads. The son holding a tie
                    // to the blacksmith at 90 is the son telling his father, not the reverse.
                    ties.Add((npc.Id, npcTies[t].OtherNpcId, npcTies[t].Trust));
                }
            }

            var social     = new SocialGraph(ties);
            var propagator = new RumorPropagator(social, _config.DecayPercentPerHop, _config.MaxHops);
            var opinions   = new RelationshipGraph(_config.OpinionMin, _config.OpinionMax);

            _deltaByActionId = new Dictionary<string, int>(_actions.Length, StringComparer.Ordinal);

            for (int i = 0; i < _actions.Length; i++)
            {
                _deltaByActionId.Add(_actions[i].Id, _actions[i].BaseDelta);
            }

            return new GossipService(opinions, propagator, _config.HopDelay);
        }

        private bool IsConfigured(out string problem)
        {
            problem = null;

            if (_config == null)
            {
                problem = "No gossip config assigned.";
                return false;
            }

            if (_npcs == null || _npcs.Length == 0)
            {
                problem = "No NPC definitions assigned.";
                return false;
            }

            if (_actions == null || _actions.Length == 0)
            {
                problem = "No action definitions assigned.";
                return false;
            }

            var npcIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < _npcs.Length; i++)
            {
                if (_npcs[i] == null)
                {
                    problem = $"NPC definition {i} is not assigned.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(_npcs[i].Id))
                {
                    problem = $"NPC definition '{_npcs[i].name}' has an empty id.";
                    return false;
                }

                // Two assets sharing an id would quietly merge their ties into one NPC, and the
                // village would behave like an asset nobody wrote.
                if (!npcIds.Add(_npcs[i].Id))
                {
                    problem = $"NPC id '{_npcs[i].Id}' is declared by more than one definition.";
                    return false;
                }
            }

            // A tie pointing at an id nobody declares still builds a node, so a typo would send
            // rumors to an NPC who does not exist and the village would look merely quiet (R9).
            for (int i = 0; i < _npcs.Length; i++)
            {
                IReadOnlyList<SocialTie> ties = _npcs[i].Ties;

                if (ties == null) continue;

                for (int t = 0; t < ties.Count; t++)
                {
                    if (npcIds.Contains(ties[t].OtherNpcId)) continue;

                    problem = $"NPC '{_npcs[i].Id}' has a tie to '{ties[t].OtherNpcId}', which no definition declares.";
                    return false;
                }
            }

            var actionIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < _actions.Length; i++)
            {
                if (_actions[i] == null)
                {
                    problem = $"Action definition {i} is not assigned.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(_actions[i].Id))
                {
                    problem = $"Action definition '{_actions[i].name}' has an empty id.";
                    return false;
                }

                // The id is also what a save writes as the reason of every opinion it caused, so a
                // duplicate would make two different actions indistinguishable on disk.
                if (!actionIds.Add(_actions[i].Id))
                {
                    problem = $"Action id '{_actions[i].Id}' is declared by more than one definition.";
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
