using System;
using System.Collections.Generic;
using GosipSimulator.Core;

namespace GosipSimulator.Gossip
{
    /// <summary>
    /// The domain layer of the module: owns the opinions, turns a sighting into a spreading story and
    /// publishes the result. Plain C# apart from the bus, following ProgressService, which is also a
    /// pure class that publishes (R5). GossipManager is the adapter and stays thin.
    ///
    /// Rumors move on game time through Tick rather than instantly, so pausing holds a story mid-spread
    /// the same way it holds a pickup waiting to respawn.
    /// </summary>
    public class GossipService
    {
        private struct PendingRumor
        {
            public string actionId;

            /// <summary>Who the opinion is about, which is the actor and not whoever is telling it.</summary>
            public string aboutId;

            public RumorPropagator.Step step;
            public float remaining;
        }

        private readonly RelationshipGraph  _opinions;
        private readonly RumorPropagator    _propagator;
        private readonly float              _hopDelay;
        private readonly List<PendingRumor> _pending = new List<PendingRumor>();

        // Reused across ticks so a frame with rumors in flight allocates nothing, which is the same
        // reason RespawnQueue.Tick writes into a caller-owned list.
        private readonly List<PendingRumor> _due = new List<PendingRumor>();

        // ────────────────────────────────
        // CONSTRUCTOR
        // ────────────────────────────────
        #region Constructor

        /// <param name="hopDelay">Seconds of game time per hop. Zero delivers the whole story on the next Tick.</param>
        public GossipService(RelationshipGraph opinions, RumorPropagator propagator, float hopDelay)
        {
            if (opinions == null) throw new ArgumentNullException(nameof(opinions));
            if (propagator == null) throw new ArgumentNullException(nameof(propagator));

            // Negated comparison, which also rejects NaN: a NaN delay would never count down, so the
            // rumor would sit in the queue forever with nothing in the log to explain it (A5).
            if (!(hopDelay >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(hopDelay), hopDelay, "Must be zero or positive.");
            }

            _opinions   = opinions;
            _propagator = propagator;
            _hopDelay   = hopDelay;
        }

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>
        /// The opinion store this service owns. Exposed so the adapter and the HUD can read it, and
        /// read-only in practice because RelationshipGraph is the only thing that can mutate it (R7).
        /// </summary>
        public RelationshipGraph Opinions => _opinions;

        /// <summary>How many rumor steps are waiting to be told. Zero when the village is quiet.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>
        /// An NPC saw something. The witness forms an opinion of the actor at once and the story starts
        /// travelling. Returns false when there was nothing to do, which today only happens when the
        /// actor is their own witness.
        /// </summary>
        /// <param name="baseDelta">
        /// Signed: negative for a theft, positive for a good turn. Its magnitude is also how loud the
        /// story starts out, so a petty theft spreads faintly and a grand one reaches the whole village.
        /// </param>
        public bool WitnessAction(string actionId, string actorId, string witnessId, int baseDelta)
        {
            ValidateId(actionId, nameof(actionId));
            ValidateId(actorId, nameof(actorId));
            ValidateId(witnessId, nameof(witnessId));

            // RelationshipGraph rejects a self-opinion outright. Perception should not have reported
            // this, and throwing here would take the game down over a filter that belongs upstream, so
            // it is ignored and the caller is told through the return value rather than in silence (R9).
            if (string.Equals(actorId, witnessId, StringComparison.Ordinal)) return false;

            int previous = _opinions.Get(witnessId, actorId);
            int current  = _opinions.Apply(witnessId, actorId, baseDelta);

            // Only on a real change, the way GameManager publishes OnGameStateChanged: a subscriber can
            // treat every event as news instead of comparing against a value it already had. An opinion
            // already sitting at the end of its range therefore publishes nothing, which is correct.
            if (current != previous)
            {
                EventBus.Publish(new OnRelationshipChanged
                {
                    npcId    = witnessId,
                    aboutId  = actorId,
                    previous = previous,
                    current  = current,
                    reason   = actionId
                });
            }

            IReadOnlyList<RumorPropagator.Step> plan = _propagator.Plan(witnessId, baseDelta);

            for (int i = 0; i < plan.Count; i++)
            {
                _pending.Add(new PendingRumor
                {
                    actionId = actionId,
                    aboutId  = actorId,
                    step     = plan[i],

                    // Cumulative rather than one delay for every hop, so hop 2 lands an interval after
                    // hop 1 and the story visibly moves outward instead of arriving everywhere at once.
                    remaining = _hopDelay * plan[i].hop
                });
            }

            return true;
        }

        /// <summary>
        /// Advances every rumor in flight and delivers the ones that came due, in hop order. Call it
        /// with scaled deltaTime from the adapter so a paused game holds the story where it is.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f)) return;
            if (_pending.Count == 0) return;

            _due.Clear();

            // One forward pass with a separate write index: keeps the hop order and removes the
            // delivered entries without shifting the list once per removal.
            int write = 0;

            for (int read = 0; read < _pending.Count; read++)
            {
                PendingRumor entry = _pending[read];
                entry.remaining -= deltaTime;

                if (entry.remaining <= 0f) _due.Add(entry);
                else _pending[write++] = entry;
            }

            _pending.RemoveRange(write, _pending.Count - write);

            // Delivered only after the queue is already consistent. A subscriber to OnRelationshipChanged
            // is free to start another rumor, and mutating _pending while walking it would drop or
            // double-apply steps.
            for (int i = 0; i < _due.Count; i++)
            {
                Deliver(_due[i]);
            }

            _due.Clear();
        }

        /// <summary>
        /// Applies a loaded save. Silent by design: routing this through Apply would publish one
        /// OnRelationshipChanged per row, and SaveSystem listens to that event to mark the save dirty,
        /// so a game would rewrite its own file the moment it finished loading.
        /// </summary>
        public void Restore(string[] npcIds, string[] aboutIds, int[] values)
        {
            // A default OnRelationshipsRestored has null arrays, and that is what a fresh save
            // produces, so null means empty here rather than broken.
            int count = npcIds?.Length ?? 0;

            if ((aboutIds?.Length ?? 0) != count || (values?.Length ?? 0) != count)
            {
                throw new ArgumentException(
                    $"The three parallel arrays must be the same length; got {npcIds?.Length ?? 0}, " +
                    $"{aboutIds?.Length ?? 0} and {values?.Length ?? 0}.");
            }

            // Rumors in flight are not persisted, so a load discards them. That is a scope decision and
            // not an oversight: the demo does not need a half-told story to survive a quit. Leaving them
            // queued would move an opinion twice, once for the saved value and once for the rumor.
            _pending.Clear();

            for (int i = 0; i < count; i++)
            {
                _opinions.Set(npcIds[i], aboutIds[i], values[i]);
            }
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private void Deliver(PendingRumor rumor)
        {
            // The story first and the consequence second, so a log reads "the son told the blacksmith"
            // before "the blacksmith now thinks less of you".
            EventBus.Publish(new OnRumorSpread
            {
                actionId = rumor.actionId,
                fromId   = rumor.step.fromId,
                toId     = rumor.step.toId,
                hop      = rumor.step.hop,
                weight   = rumor.step.weight
            });

            int previous = _opinions.Get(rumor.step.toId, rumor.aboutId);
            int current  = _opinions.Apply(rumor.step.toId, rumor.aboutId, rumor.step.weight);

            if (current == previous) return;

            EventBus.Publish(new OnRelationshipChanged
            {
                npcId    = rumor.step.toId,
                aboutId  = rumor.aboutId,
                previous = previous,
                current  = current,
                reason   = rumor.actionId
            });
        }

        private static void ValidateId(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An id cannot be null, empty or whitespace.", paramName);
            }
        }

        #endregion
    }
}
