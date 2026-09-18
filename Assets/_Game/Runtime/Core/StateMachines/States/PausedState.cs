using UnityEngine;

namespace GosipSimulator.Core.State
{
    /// <summary>
    /// Freezes scaled time while current: FixedUpdate, physics and anything timed with deltaTime
    /// stop, while input, which runs on unscaled time, still delivers the resume request. Exit
    /// restores the scale that was there before instead of assuming 1.
    /// </summary>
    public class PausedState : IGameState
    {
        private float _previousTimeScale = 1f;

        public GameState Id => GameState.Paused;

        public void Enter()
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale     = 0f;

            Log.Trace("[PausedState] Enter");
        }

        public void Tick() { }

        public void Exit()
        {
            Time.timeScale = _previousTimeScale;

            Log.Trace("[PausedState] Exit");
        }
    }
}
