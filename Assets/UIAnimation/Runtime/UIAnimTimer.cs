using System;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>A running timer; default = none. Cancel and check it like a motion handle.</summary>
    internal readonly struct UIAnimTimerHandle
    {
        readonly int _slot, _version;

        internal UIAnimTimerHandle(int slot, int version)
        {
            _slot = slot;
            _version = version + 1;   // 0 stays "no timer"
        }

        public bool IsActive() => _version != 0 && UIAnimTimer.IsActive(_slot, _version - 1);

        /// <summary>The clock time it started at (elapsed = clock - this).</summary>
        public double StartTime => UIAnimTimer.StartOf(_slot);

        /// <summary>Stops it without completing (its cancel callback runs). Nothing if it already ended.</summary>
        public void TryCancel()
        {
            if (_version != 0) UIAnimTimer.Cancel(_slot, _version - 1);
        }
    }

    /// <summary>
    /// The one clock behind every playing state and scroll motion: real time (time scale ignored), ticked once per frame
    /// in the player loop's Update, and by the editor update outside Play mode. Callbacks are static (state passed in),
    /// so starting a timer does not allocate once the slot array has grown.
    /// </summary>
    internal static class UIAnimTimer
    {
        struct Slot
        {
            public int version;
            public bool active;
            public double start;
            public float duration;
            public object state;
            public Action<object, float> tick;   // (state, elapsed seconds), last call at elapsed = duration
            public Action<object> complete, cancel;
        }

        static Slot[] _slots = new Slot[64];
        static int _count;                       // slots in use or freed, up to here
        static int[] _free = new int[64];
        static int _freeCount;
        static double _now;

        /// <summary>
        /// Starts a timer of <paramref name="duration"/> seconds: <paramref name="tick"/> every frame with the elapsed time,
        /// then <paramref name="complete"/>. The first tick comes on the next frame.
        /// </summary>
        public static UIAnimTimerHandle Start(float duration, object state, Action<object, float> tick,
            Action<object> complete = null, Action<object> cancel = null)
        {
            EnsureHooked();
            int slot;
            if (_freeCount > 0) slot = _free[--_freeCount];
            else
            {
                if (_count == _slots.Length) Array.Resize(ref _slots, _count * 2);
                slot = _count++;
            }
            ref var s = ref _slots[slot];
            s.active = true;
            s.start = Now;
            s.duration = duration;
            s.state = state;
            s.tick = tick;
            s.complete = complete;
            s.cancel = cancel;
            return new UIAnimTimerHandle(slot, s.version);
        }

        internal static double StartOf(int slot) => _slots[slot].start;

        internal static bool IsActive(int slot, int version) => slot < _count && _slots[slot].version == version && _slots[slot].active;

        internal static void Cancel(int slot, int version)
        {
            if (!IsActive(slot, version)) return;
            var cancel = _slots[slot].cancel;
            var state = _slots[slot].state;
            Free(slot);
            cancel?.Invoke(state);
        }

        static void Free(int slot)
        {
            ref var s = ref _slots[slot];
            s.active = false;
            s.version++;
            s.state = null;
            s.tick = null;
            s.complete = s.cancel = null;
            if (_freeCount == _free.Length) Array.Resize(ref _free, _freeCount * 2);
            _free[_freeCount++] = slot;
        }

        static double Now => Time.realtimeSinceStartupAsDouble;

        /// <summary>Advances every timer to the current time. Callbacks may start or cancel timers.</summary>
        internal static void Update()
        {
            _now = Now;
            UIAnimBatch.Run(_now);   // values of every batched playback at _now, before the callbacks write them
            var count = _count;   // timers started during this update tick from the next frame
            for (var i = 0; i < count; i++)
            {
                ref var s = ref _slots[i];
                if (!s.active) continue;
                var version = s.version;
                var elapsed = (float)(_now - s.start);
                if (elapsed < s.duration)
                {
                    s.tick?.Invoke(s.state, elapsed);
                    continue;
                }
                var (state, tick, complete) = (s.state, s.tick, s.complete);
                tick?.Invoke(state, s.duration);
                if (!IsActive(i, version)) continue;   // the tick canceled or replaced it
                Free(i);
                complete?.Invoke(state);
            }
        }

        // ---------- hooking into the frame ----------

        static bool _hooked;

        struct UIAnimTimerUpdate { }

        static void EnsureHooked()
        {
            if (_hooked) return;
            _hooked = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorUpdate;
            UnityEditor.EditorApplication.update += EditorUpdate;
#endif
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (Insert(ref loop)) PlayerLoop.SetPlayerLoop(loop);
        }

        // Adds the tick to the Update phase once (survives entering Play mode without a domain reload).
        static bool Insert(ref PlayerLoopSystem loop)
        {
            if (loop.subSystemList == null) return false;
            for (var i = 0; i < loop.subSystemList.Length; i++)
            {
                ref var phase = ref loop.subSystemList[i];
                if (phase.type != typeof(Update)) continue;
                var systems = phase.subSystemList ?? Array.Empty<PlayerLoopSystem>();
                foreach (var system in systems)
                    if (system.type == typeof(UIAnimTimerUpdate)) return false;
                var list = new PlayerLoopSystem[systems.Length + 1];
                systems.CopyTo(list, 0);
                list[^1] = new PlayerLoopSystem
                {
                    type = typeof(UIAnimTimerUpdate),
                    updateDelegate = static () => { if (Application.isPlaying) Update(); },
                };
                phase.subSystemList = list;
                return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            // Without a domain reload, statics survive into Play mode: start clean, hook again.
            _count = _freeCount = 0;
            _slots = new Slot[64];
            _hooked = false;
            UIAnimBatch.Clear();
            EnsureHooked();
        }

#if UNITY_EDITOR
        static void EditorUpdate()
        {
            if (!Application.isPlaying) Update();
        }
#endif
    }
}
