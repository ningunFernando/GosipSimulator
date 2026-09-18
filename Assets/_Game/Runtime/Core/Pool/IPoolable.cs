namespace GosipSimulator.Core.Pool
{
    /// <summary>
    /// Implemented by pooled objects that carry per-use state. A reused object comes back with
    /// the previous use still on it (particles, timers, health), so the reset has to be explicit
    /// instead of assumed.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();

        void OnDespawn();
    }
}
