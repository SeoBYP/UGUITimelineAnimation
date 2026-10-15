using System;
using Unity.Burst;
using Unity.Mathematics;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// LitMotion-style batching: the values of every playing ease-keyed track are computed by one Burst call per frame,
    /// before the timer callbacks run; a playback's step then only writes them. Tracks with an AnimationCurve, Punch/Shake,
    /// per-character effects or scramble stay on the managed path (<see cref="UIAnimTrack.Sample"/>), as does any step whose
    /// time is not the one computed here (tests, the first pose, a playback started this frame).
    /// </summary>
    [BurstCompile]
    internal static unsafe class UIAnimBatch
    {
        struct Key
        {
            public float time;
            public float4 value;
            public UIAnimEase ease;
        }

        struct Lane
        {
            public int keyStart, keyCount, group;
            public byte step;   // Active: on / off, no in-between
            public byte goTo;   // a lone key reached from the live start value
            public float4 from;
        }

        struct Group
        {
            public double start;
            public float duration, t;
            public byte loop, reversed, alive;
            public int laneStart, laneCount, keyStart, keyCount;
        }

        static Key[] _keys = new Key[256];
        static Lane[] _lanes = new Lane[64];
        static float4[] _values = new float4[64];
        static Group[] _groups = new Group[32];
        static UIAnimation.Playback[] _owners = new UIAnimation.Playback[32];   // null = abandoned slot
        static int _keyCount, _laneCount, _groupCount, _compactAt = 256;

        static bool Batchable(UIAnimTrack track)
        {
            if (track.target == null || track.keys.Count == 0 || UIAnimTextChars.IsCharProperty(track.property)) return false;
            if (track.property == UIAnimProperty.TextReveal && track.scramble != UIAnimScramble.None) return false;
            foreach (var key in track.keys)
                if (key.curveType != UIAnimCurveType.Ease) return false;
            return true;
        }

        /// <summary>
        /// Packs a started playback's resolved keys. Its timer must be running (the start time comes from it).
        /// A playback keeps its slot between plays and rewrites it in place, so replaying does not pack anew.
        /// </summary>
        internal static void Register(UIAnimation.Playback p)
        {
            if (p.group < 0 || !Rewrite(p))
            {
                if (p.group >= 0) _owners[p.group] = null;   // the keys were edited since: leave the old slot behind
                Allocate(p);
                Rewrite(p);
            }
            ref var group = ref _groups[p.group];
            group.start = p.handle.StartTime;
            group.duration = p.duration;
            group.t = float.NaN;
            group.loop = (byte)(p.state.loop ? 1 : 0);
            group.reversed = (byte)(p.reversed ? 1 : 0);
            group.alive = 1;
        }

        // Copies the resolved keys into the slot; false if they no longer fit it (one pass, no Unity null checks).
        static bool Rewrite(UIAnimation.Playback p)
        {
            ref var group = ref _groups[p.group];
            int lane = group.laneStart, key = group.keyStart, end = group.laneStart + group.laneCount;
            var tracks = p.state.tracks;
            var allKeys = _keys;
            for (var i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                if (track.lane < 0) continue;
                var keys = track.keys;
                var count = keys.Count;
                if (track.lane != lane || lane >= end) return false;
                ref var l = ref _lanes[lane];
                if (l.keyCount != count) return false;
                l.from = track.startValue;
                l.goTo = (byte)(count == 1 && track.start != UIAnimStart.KeyValue && keys[0].time > 0f ? 1 : 0);
                for (var j = 0; j < count; j++)
                {
                    var k = keys[j];
                    if (k.curveType != UIAnimCurveType.Ease) return false;
                    ref var packed = ref allKeys[key++];
                    packed.time = k.time;
                    packed.value = k.resolved;
                    packed.ease = k.ease;
                }
                lane++;
            }
            return lane == end;
        }

        static void Allocate(UIAnimation.Playback p)
        {
            var g = _groupCount++;
            Grow(ref _groups, _groupCount);
            Grow(ref _owners, _groupCount);
            _owners[g] = p;
            p.group = g;
            int laneStart = _laneCount, keyStart = _keyCount;
            foreach (var track in p.state.tracks)
            {
                track.lane = -1;
                if (!Batchable(track)) continue;
                var lane = _laneCount++;
                Grow(ref _lanes, _laneCount);
                Grow(ref _values, _laneCount);
                _lanes[lane] = new Lane
                {
                    keyStart = _keyCount, keyCount = track.keys.Count, group = g,
                    step = (byte)(track.property == UIAnimProperty.Active ? 1 : 0),
                };
                _keyCount += track.keys.Count;
                track.lane = lane;
            }
            Grow(ref _keys, _keyCount);
            _groups[g] = new Group { laneStart = laneStart, laneCount = _laneCount - laneStart, keyStart = keyStart, keyCount = _keyCount - keyStart };
        }

        /// <summary>Stops computing it; the slot stays reserved for its next play.</summary>
        internal static void Unregister(UIAnimation.Playback p)
        {
            if (p.group >= 0) _groups[p.group].alive = 0;
        }

        /// <summary>Whether this frame's batch computed the playback's values at exactly t.</summary>
        internal static bool Ready(UIAnimation.Playback p, float t) => p.group >= 0 && _groups[p.group].alive != 0 && _groups[p.group].t == t;

        internal static float4 Value(int lane) => _values[lane];

        /// <summary>Computes every live lane at <paramref name="now"/>, the clock the timer uses for this frame.</summary>
        internal static void Run(double now)
        {
            if (_groupCount >= _compactAt) Compact();
            if (_groupCount == 0) return;
            fixed (Group* groups = _groups)
            fixed (Lane* lanes = _lanes)
            fixed (Key* keys = _keys)
            fixed (float4* values = _values)
                Sample(groups, _groupCount, lanes, _laneCount, keys, values, now);
        }

        internal static void Clear()
        {
            for (var g = 0; g < _groupCount; g++)
                if (_owners[g] != null) Forget(_owners[g]);
            Array.Clear(_owners, 0, _groupCount);
            _keyCount = _laneCount = _groupCount = 0;
            _compactAt = 256;
        }

        static void Forget(UIAnimation.Playback p)
        {
            p.group = -1;
            foreach (var track in p.state.tracks) track.lane = -1;
        }

        // Slots of destroyed animations and edited states pile up: drop them and move the rest down (rare, at doubling).
        static void Compact()
        {
            int groups = 0, lanes = 0, keys = 0;
            for (var g = 0; g < _groupCount; g++)
            {
                var p = _owners[g];
                if (p == null) continue;
                var group = _groups[g];
                if (p.owner == null && group.alive == 0)   // destroyed (Unity null) and not playing
                {
                    Forget(p);
                    continue;
                }
                int laneShift = group.laneStart - lanes, keyShift = group.keyStart - keys;
                Array.Copy(_lanes, group.laneStart, _lanes, lanes, group.laneCount);
                Array.Copy(_values, group.laneStart, _values, lanes, group.laneCount);
                Array.Copy(_keys, group.keyStart, _keys, keys, group.keyCount);
                for (var l = lanes; l < lanes + group.laneCount; l++)
                {
                    _lanes[l].group = groups;
                    _lanes[l].keyStart -= keyShift;
                }
                foreach (var track in p.state.tracks)
                    if (track.lane >= 0) track.lane -= laneShift;
                group.laneStart = lanes;
                group.keyStart = keys;
                _groups[groups] = group;
                _owners[groups] = p;
                p.group = groups++;
                lanes += group.laneCount;
                keys += group.keyCount;
            }
            Array.Clear(_owners, groups, _groupCount - groups);
            (_groupCount, _laneCount, _keyCount) = (groups, lanes, keys);
            _compactAt = Math.Max(256, groups * 2);
        }

        static void Grow<T>(ref T[] array, int size)
        {
            if (size > array.Length) Array.Resize(ref array, Math.Max(size, array.Length * 2));
        }

        // Same arithmetic as the timer callbacks (UIAnimation.StartMotion / Lap), so Ready compares equal floats.
        [BurstCompile]
        static void Sample(Group* groups, int groupCount, Lane* lanes, int laneCount, Key* keys, float4* values, double now)
        {
            for (var g = 0; g < groupCount; g++)
            {
                ref var group = ref groups[g];
                if (group.alive == 0) continue;
                var elapsed = (float)(now - group.start);
                float t;
                if (group.loop != 0)
                {
                    t = elapsed % group.duration;
                    if (group.reversed != 0) t = group.duration - t;
                }
                else
                {
                    if (!(elapsed < group.duration)) elapsed = group.duration;
                    t = group.reversed != 0 ? group.duration - elapsed : elapsed;
                }
                group.t = t;
            }
            for (var l = 0; l < laneCount; l++)
            {
                ref var lane = ref lanes[l];
                if (groups[lane.group].alive == 0) continue;
                values[l] = Evaluate(ref lane, keys + lane.keyStart, groups[lane.group].t);
            }
        }

        // UIAnimTrack.Sample for ease-only keys.
        static float4 Evaluate(ref Lane lane, Key* keys, float t)
        {
            var first = keys[0];
            if (lane.goTo != 0 && t < first.time) return Segment(lane.from, ref first, t, first.time, lane.step);
            if (t <= first.time) return first.value;
            for (var i = 1; i < lane.keyCount; i++)
            {
                ref var key = ref keys[i];
                if (t >= key.time) continue;
                var prevTime = keys[i - 1].time;
                return Segment(keys[i - 1].value, ref key, t - prevTime, key.time - prevTime, lane.step);
            }
            return keys[lane.keyCount - 1].value;
        }

        static float4 Segment(float4 from, ref Key key, float local, float span, byte step)
        {
            var p = span > 0f ? math.saturate(local / span) : 1f;
            if (step != 0) return p >= 1f ? key.value : from;
            return from + (key.value - from) * UIAnimEasing.Evaluate(p, key.ease);
        }
    }
}
