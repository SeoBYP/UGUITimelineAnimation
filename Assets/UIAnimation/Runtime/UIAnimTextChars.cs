using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using TMPro;
using UnityEngine;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Per-character animation of a TMP_Text. Character tracks only accumulate per-character values during an
    /// evaluation; <see cref="FlushAll"/> then regenerates the mesh once and applies everything, so several
    /// character tracks on the same text (e.g. scale + offset + color) compose instead of overwriting each other.
    /// </summary>
    internal sealed class UIAnimTextChars
    {
        static readonly ConditionalWeakTable<TMP_Text, UIAnimTextChars> Cache = new();
        static readonly List<UIAnimTextChars> Touched = new();

        readonly TMP_Text _text;
        Vector3[] _scale = new Vector3[0], _rotation = new Vector3[0], _offset = new Vector3[0];
        Color32[] _color = new Color32[0];
        float[] _alpha = new float[0];
        bool[] _hasColor = new bool[0], _hasAlpha = new bool[0];
        bool _touched;

        // Scramble reveal rewrites the text itself; this remembers the real text behind it.
        string _source, _lastWritten;
        readonly StringBuilder _sb = new();

        UIAnimTextChars(TMP_Text text) => _text = text;

        public static UIAnimTextChars For(TMP_Text text) => Cache.GetValue(text, t => new UIAnimTextChars(t));

        public static bool IsCharProperty(UIAnimProperty p) =>
            p is UIAnimProperty.CharScale or UIAnimProperty.CharOffset or UIAnimProperty.CharRotation
                or UIAnimProperty.CharColor or UIAnimProperty.CharAlpha;

        /// <summary>Samples the track once per character, each one shifted by the track's stagger.</summary>
        public void Accumulate(UIAnimTrack track, float t)
        {
            var count = _text.textInfo.characterCount;
            if (!_touched) Begin(count);

            var lastKey = track.keys[track.keys.Count - 1].time;
            for (var i = 0; i < count && i < _scale.Length; i++)
            {
                var local = t - i * track.stagger;
                Vector4 v;
                if (track.durationPerChar > 0f && lastKey > 0f)
                {
                    // Character i plays the keys over i x durationPerChar seconds; character 0 just holds the end.
                    var scale = i * track.durationPerChar / lastKey;
                    v = scale > 0f ? track.Sample(local, scale) : track.Sample(float.MaxValue);
                }
                else v = track.Sample(local);
                switch (track.property)
                {
                    case UIAnimProperty.CharScale: _scale[i] = Vector3.Scale(_scale[i], v); break;
                    case UIAnimProperty.CharOffset: _offset[i] += (Vector3)v; break;
                    case UIAnimProperty.CharRotation: _rotation[i] += (Vector3)v; break;
                    case UIAnimProperty.CharColor: _color[i] = (Color)v; _hasColor[i] = true; break;
                    case UIAnimProperty.CharAlpha: _alpha[i] = v.x; _hasAlpha[i] = true; break;
                }
            }
        }

        void Begin(int count)
        {
            _touched = true;
            Touched.Add(this);
            if (_scale.Length < count)
            {
                _scale = new Vector3[count];
                _rotation = new Vector3[count];
                _offset = new Vector3[count];
                _color = new Color32[count];
                _alpha = new float[count];
                _hasColor = new bool[count];
                _hasAlpha = new bool[count];
            }
            for (var i = 0; i < count; i++)
            {
                _scale[i] = Vector3.one;
                _rotation[i] = _offset[i] = Vector3.zero;
                _hasColor[i] = _hasAlpha[i] = false;
            }
        }

        /// <summary>Remembers the text to reveal, unless the current text is our own scrambled output.</summary>
        public void CaptureSource()
        {
            if (_source == null || _text.text != _lastWritten) _source = _text.text;
        }

        /// <summary>Shows the first `reveal` share of the text and random characters for the rest (LitMotion scramble).</summary>
        public void Scramble(float reveal, UIAnimScramble mode, float time)
        {
            CaptureSource();   // text replaced mid-reveal (e.g. next dialogue line) becomes the new source
            // ponytail: builds a new string per frame while scrambling (TMP needs a string); fine for short reveals
            var text = reveal >= 1f ? _source : UIAnimVibration.Scramble(_source, reveal, mode, 0u, time, _sb);
            if (text == _text.text) return;
            _text.text = text;
            _lastWritten = text;
        }

        /// <summary>Applies every text touched during this evaluation.</summary>
        public static void FlushAll()
        {
            foreach (var chars in Touched) chars.Flush();
            Touched.Clear();
        }

        void Flush()
        {
            _touched = false;
            if (_text == null) return;

            // Fresh geometry every frame: respects maxVisibleCharacters (reveal) and text changes,
            // and means the per-character transforms never stack up across frames.
            // ponytail: one mesh rebuild per animated text per frame; cache base vertices if many texts animate at once
            _text.ForceMeshUpdate();
            var info = _text.textInfo;

            for (var i = 0; i < info.characterCount && i < _scale.Length; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;

                var mesh = info.meshInfo[ch.materialReferenceIndex];
                var vi = ch.vertexIndex;
                var verts = mesh.vertices;
                var center = (verts[vi] + verts[vi + 2]) * 0.5f;
                var m = Matrix4x4.TRS(center + _offset[i], Quaternion.Euler(_rotation[i]), _scale[i]) * Matrix4x4.Translate(-center);
                for (var k = 0; k < 4; k++) verts[vi + k] = m.MultiplyPoint3x4(verts[vi + k]);

                if (_hasColor[i] || _hasAlpha[i])
                {
                    var colors = mesh.colors32;
                    for (var k = 0; k < 4; k++)
                    {
                        var c = _hasColor[i] ? _color[i] : colors[vi + k];
                        if (_hasAlpha[i]) c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(_alpha[i] * 255f), 0, 255);
                        colors[vi + k] = c;
                    }
                }
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
