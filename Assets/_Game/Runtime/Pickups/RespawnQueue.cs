using System;
using System.Collections.Generic;

namespace GosipSimulator.Pickups
{
    /// <summary>
    /// Which spawn points are waiting to refill, and for how long. Plain C#, so the timing rules run
    /// in EditMode (R5). Time only advances through Tick, and a Tick with no elapsed time releases
    /// nothing: that is what holds every respawn while the game is paused.
    /// </summary>
    public class RespawnQueue
    {
        private struct PendingRespawn
        {
            public int   pointIndex;
            public float remaining;
        }

        private readonly List<PendingRespawn> _pending = new List<PendingRespawn>();

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public int Count => _pending.Count;

        public void Schedule(int pointIndex, float delay)
        {
            if (pointIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "Must be zero or positive.");
            }

            // The negated comparison also rejects NaN, which would never count down (A5).
            if (!(delay >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(delay), delay, "Must be zero or positive.");
            }

            _pending.Add(new PendingRespawn { pointIndex = pointIndex, remaining = delay });
        }

        /// <summary>
        /// Advances every pending respawn by deltaTime and moves the due ones into ready, in the order
        /// they were scheduled. ready is cleared first and meant to be reused by the caller, so a frame
        /// with nothing due allocates nothing.
        /// </summary>
        public void Tick(float deltaTime, List<int> ready)
        {
            if (ready == null) throw new ArgumentNullException(nameof(ready));

            ready.Clear();

            if (!(deltaTime > 0f)) return;

            // One forward pass with a separate write index: keeps the scheduling order and removes the
            // due entries without shifting the list once per removal.
            int write = 0;

            for (int read = 0; read < _pending.Count; read++)
            {
                PendingRespawn entry = _pending[read];
                entry.remaining -= deltaTime;

                if (entry.remaining <= 0f)
                {
                    ready.Add(entry.pointIndex);
                }
                else
                {
                    _pending[write++] = entry;
                }
            }

            _pending.RemoveRange(write, _pending.Count - write);
        }

        #endregion
    }
}
