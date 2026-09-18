namespace GosipSimulator.Core.State
{
    public class MenuState : IGameState
    {
        public GameState Id => GameState.Menu;

        public void Enter() => Log.Trace("[MenuState] Enter");

        public void Tick() { }

        public void Exit() => Log.Trace("[MenuState] Exit");
    }
}
