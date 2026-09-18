namespace GosipSimulator.Core.State
{
    public class PlayState : IGameState
    {
        public GameState Id => GameState.Play;

        public void Enter() => Log.Trace("[PlayState] Enter");

        public void Tick() { }

        public void Exit() => Log.Trace("[PlayState] Exit");
    }
}
