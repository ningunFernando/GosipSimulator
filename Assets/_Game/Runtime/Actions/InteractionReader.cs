using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using GosipSimulator.Core;
using GosipSimulator.Core.State;

namespace GosipSimulator.Actions
{
    /// <summary>
    /// The player's verbs. Reads the Interact action, decides what was in reach, and publishes what
    /// happened. It never interprets: it does not know that anyone might be watching, what an action
    /// is worth, or that opinions exist. All of that hangs off OnActionCommitted (R4).
    ///
    /// Input comes from a serialized InputActionReference rather than PlayerInput in Send Messages
    /// mode, where renaming a handler breaks input with no compile error and no warning (M9). Play
    /// state comes from the bus rather than from GameManager, the same way PlayerInputReader learns
    /// it, so this module needs no reference to Player or Core's manager (R4).
    /// </summary>
    public class InteractionReader : MonoBehaviour
    {
        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("Input")]
        [Tooltip("Button action that acts on whatever is in reach, e.g. Player/Interact in InputSystem_Actions.")]
        [SerializeField] private InputActionReference _interactAction;

        [Header("Reach")]
        [Tooltip("How far the player can act, in world units, measured from this object.")]
        [SerializeField] private float _reach = 2.5f;

        [Header("World")]
        [Tooltip("Everything that can be acted on. Assigned here rather than found at runtime (R6).")]
        [SerializeField] private Interactable[] _interactables;

        [Header("Identity")]
        [Tooltip("Who the player is to the rest of the game. Must match the id opinions are about.")]
        [SerializeField] private string _actorId = "player";

        #endregion

        private readonly InteractionResolver _resolver = new InteractionResolver();

        // Two lists kept in step: the positions the resolver sees, and which interactable each one
        // came from. Rebuilt per press rather than cached, because things move and a stale position
        // would let the player act on where something used to be.
        private readonly List<Vector3>      _positions = new List<Vector3>();
        private readonly List<Interactable> _inReach   = new List<Interactable>();

        private bool _isPlaying;
        private bool _isSubscribed;

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void OnValidate()
        {
            // The Inspector stores a negative reach without complaining, and a negative reach reads
            // as "cannot act" rather than as the mistake it is (R8).
            _reach = Mathf.Max(0f, _reach);
        }

        private void Awake()
        {
            // Setting enabled makes Unity call OnDisable on the spot, before any OnEnable ran, which
            // is why OnDisable only undoes what _isSubscribed says actually happened (R9).
            if (!IsConfigured(out string problem))
            {
                Log.Error($"[InteractionReader] {problem} The player cannot act.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _interactAction.action.performed += OnInteract;
            _interactAction.action.Enable();

            EventBus.Subscribe<OnGameStateChanged>(HandleGameStateChanged);

            _isSubscribed = true;
        }

        private void OnDisable()
        {
            if (!_isSubscribed) return;

            EventBus.Unsubscribe<OnGameStateChanged>(HandleGameStateChanged);

            _interactAction.action.performed -= OnInteract;
            _interactAction.action.Disable();

            _isPlaying    = false;
            _isSubscribed = false;
        }

        #endregion

        // ────────────────────────────────
        // EVENT HANDLERS
        // ────────────────────────────────
        #region Event Handlers

        private void OnInteract(InputAction.CallbackContext context)
        {
            // Input keeps arriving during a pause because it runs on unscaled time, so the state
            // check is what stops a paused player from robbing the blacksmith.
            if (!_isPlaying) return;

            _positions.Clear();
            _inReach.Clear();

            for (int i = 0; i < _interactables.Length; i++)
            {
                Interactable interactable = _interactables[i];

                // A disabled one is either destroyed or an Awake that rejected its own asset. It is
                // not a configuration error here, it simply cannot be acted on.
                if (interactable == null || !interactable.isActiveAndEnabled) continue;

                _inReach.Add(interactable);
                _positions.Add(interactable.transform.position);
            }

            int index = _resolver.Resolve(transform.position, _reach, _positions);

            // Pressing the key with nothing in reach is the normal case, not a problem to report.
            if (index == InteractionResolver.NONE) return;

            Interactable target = _inReach[index];

            EventBus.Publish(new OnActionCommitted
            {
                actionId = target.ActionId,
                actorId  = _actorId,
                targetId = target.TargetId,

                // Where the thing is, not where the player stands. Perception asks who could see the
                // action, and the action happens at the strongbox rather than at the thief's feet.
                position = target.transform.position
            });

            Log.Trace($"[InteractionReader] '{_actorId}' committed '{target.ActionId}' on '{target.name}'.");
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            _isPlaying = e.newState == GameState.Play;
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private bool IsConfigured(out string problem)
        {
            problem = null;

            if (_interactAction == null || _interactAction.action == null)
            {
                problem = "Interact action not assigned.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_actorId))
            {
                // Gossip stores opinions about this id and Save writes it to disk, so an empty one
                // would leave rows nobody can match back to the player.
                problem = "Actor id is empty.";
                return false;
            }

            if (_interactables == null || _interactables.Length == 0)
            {
                problem = "Nothing to interact with is assigned.";
                return false;
            }

            for (int i = 0; i < _interactables.Length; i++)
            {
                if (_interactables[i] != null) continue;

                problem = $"Interactable slot {i} is not assigned.";
                return false;
            }

            return true;
        }

        #endregion
    }
}
