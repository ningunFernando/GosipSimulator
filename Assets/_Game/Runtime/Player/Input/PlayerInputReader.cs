using UnityEngine;
using UnityEngine.InputSystem;
using GosipSimulator.Core;
using GosipSimulator.Core.State;

namespace GosipSimulator.Player
{
    /// <summary>
    /// The one owner of player input (R11). Reads serialized InputActionReferences instead of
    /// PlayerInput in Send Messages mode, where renaming a handler breaks input with no compile
    /// error and no warning (M9). Move only counts while the game is in Play, a state the reader
    /// learns from the bus instead of asking GameManager (R4). Pause is only a request: GameManager
    /// decides whether the current state allows it.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("Input")]
        [Tooltip("Vector2 action that moves the player, e.g. Player/Move in InputSystem_Actions.")]
        [SerializeField] private InputActionReference _moveAction;
        [Tooltip("Button action that pauses and resumes the game, e.g. Player/Pause in InputSystem_Actions.")]
        [SerializeField] private InputActionReference _pauseAction;

        #endregion

        private Vector2 _rawMove;
        private bool    _isPlaying;
        private bool    _isSubscribed;

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>Move input with x to the right and y forward. Zero while the game is not in Play.</summary>
        public Vector2 Move => _isPlaying ? _rawMove : Vector2.zero;

        #endregion

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            bool isValid = true;

            if (!IsAssigned(_moveAction))
            {
                Log.Error("[PlayerInputReader] Move action not assigned. Input is disabled.");
                isValid = false;
            }

            if (!IsAssigned(_pauseAction))
            {
                Log.Error("[PlayerInputReader] Pause action not assigned. Input is disabled.");
                isValid = false;
            }

            // Disabled rather than left running without input (R9). Setting enabled here keeps
            // OnEnable from running, but Unity calls OnDisable on the spot, which is why OnDisable
            // only undoes what OnEnable actually did.
            if (!isValid) enabled = false;
        }

        private void OnEnable()
        {
            _moveAction.action.performed += OnMove;
            // Without canceled the last value sticks after the key is released.
            _moveAction.action.canceled  += OnMove;
            _moveAction.action.Enable();

            _pauseAction.action.performed += OnPause;
            _pauseAction.action.Enable();

            EventBus.Subscribe<OnGameStateChanged>(HandleGameStateChanged);

            _isSubscribed = true;
        }

        private void OnDisable()
        {
            if (!_isSubscribed) return;

            EventBus.Unsubscribe<OnGameStateChanged>(HandleGameStateChanged);

            _moveAction.action.performed -= OnMove;
            _moveAction.action.canceled  -= OnMove;
            _moveAction.action.Disable();

            _pauseAction.action.performed -= OnPause;
            _pauseAction.action.Disable();

            _rawMove      = Vector2.zero;
            _isSubscribed = false;
        }

        #endregion

        // ────────────────────────────────
        // EVENT HANDLERS
        // ────────────────────────────────
        #region Event Handlers

        private void OnMove(InputAction.CallbackContext context)
        {
            _rawMove = context.ReadValue<Vector2>();
        }

        private void OnPause(InputAction.CallbackContext context)
        {
            EventBus.Publish(new OnPauseRequested());
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

        private static bool IsAssigned(InputActionReference reference)
        {
            return reference != null && reference.action != null;
        }

        #endregion
    }
}
