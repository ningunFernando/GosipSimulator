namespace GosipSimulator.Core.State
{
    /// <summary>
    /// Coarse game flow. Bootstrap and GameOver deliberately have no state class: Bootstrap is
    /// what GameManager reports while no state machine exists, and asking for either one goes
    /// through the "no implementation" path instead of silently mutating anything (C2).
    /// </summary>
    public enum GameState
    {
        Bootstrap,
        Menu,
        Play,
        Paused,
        GameOver
    }

    /// <summary>
    /// One game state. Id lets the state declare its own identity, so adding a state means
    /// adding a class instead of editing two places that can drift apart (R7, A2).
    /// </summary>
    public interface IGameState
    {
        GameState Id { get; }

        /// <summary>Called once when the state becomes current. Subscribe to events here (R10).</summary>
        void Enter();

        /// <summary>Called every frame while the state is current. Per-frame logic goes here.</summary>
        void Tick();

        /// <summary>Called once when the state stops being current. Unsubscribe here (R10).</summary>
        void Exit();
    }
}
