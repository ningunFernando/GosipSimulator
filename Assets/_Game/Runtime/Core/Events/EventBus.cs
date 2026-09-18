using System;
using System.Collections.Generic;

namespace GosipSimulator.Core
{
    /// <summary>
    /// Typed event bus and the only channel between gameplay modules (R3, R4).
    /// Payloads are structs, so publishing never boxes and never needs a base event class.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _subscribers
            = new Dictionary<Type, Delegate>();
        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public static void Subscribe<T>(Action<T> callback) where T : struct
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            Type eventType = typeof(T);

            if (_subscribers.TryGetValue(eventType, out Delegate existing))
            {
                _subscribers[eventType] = Delegate.Combine(existing, callback);
            }
            else
            {
                _subscribers[eventType] = callback;
            }
        }

        public static void Unsubscribe<T>(Action<T> callback) where T : struct
        {
            Type eventType = typeof(T);

            if (!_subscribers.TryGetValue(eventType, out Delegate existing)) return;

            Delegate remaining = Delegate.Remove(existing, callback);

            // Drop the key when the last subscriber leaves, so Publish can tell
            // "nobody listens" apart from "somebody listens".
            if (remaining == null)
            {
                _subscribers.Remove(eventType);
            }
            else
            {
                _subscribers[eventType] = remaining;
            }
        }

        public static void Publish<T>(T eventData) where T : struct
        {
            if (!_subscribers.TryGetValue(typeof(T), out Delegate subscriber))
            {
                // A silent bus is indistinguishable from a broken one (A1).
                Log.Warn($"[EventBus] Published {typeof(T).Name} with no subscribers.");
                return;
            }

            (subscriber as Action<T>)?.Invoke(eventData);
        }

        /// <summary>
        /// Only the Bootstrapper may call this, before instantiating anything. Called after
        /// DontDestroyOnLoad managers exist it leaves them permanently deaf (A1).
        /// </summary>
        public static void ClearAllSubscriptions()
        {
            _subscribers.Clear();
            Log.Trace("[EventBus] All subscriptions cleared.");
        }

        #endregion
    }
}
