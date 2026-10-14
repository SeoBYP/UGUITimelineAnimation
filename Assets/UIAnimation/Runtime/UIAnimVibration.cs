using System.Text;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Punch / shake / scramble math, matching LitMotion's (VibrationHelper, RandomHelper, FixedStringHelper are
    /// internal there), so the same parameters give the same motion as LMotion.Punch / LMotion.Shake / WithScrambleChars.
    /// </summary>
    internal static class UIAnimVibration
    {
        /// <summary>LitMotion VibrationHelper.EvaluateStrength: decaying cosine, 0 at both ends.</summary>
        public static float Envelope(int frequency, float dampingRatio, float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            var angularFrequency = (frequency * t - 0.5f) * Mathf.PI;
            var dampingFactor = dampingRatio * frequency / (2f * Mathf.PI);
            return Mathf.Pow(2.718281828f, -dampingFactor * t) * Mathf.Cos(angularFrequency);
        }

        /// <summary>LitMotion RandomHelper.Create: a generator seeded from (seed, elapsed time), so it changes every frame.</summary>
        public static Random RandomAt(uint seed, double time)
        {
            var hash = new Hash128();
            hash.Append(ref seed);
            hash.Append(ref time);
            var state = (uint)hash.GetHashCode();
            return new Random(state == 0 ? 1u : state);
        }

        /// <summary>Per-component random in -1..1, drawn the way LitMotion's float/Vector2/Vector3 shake adapters draw it.</summary>
        public static Vector4 RandomSigned(uint seed, double time, int dimensions)
        {
            var r = RandomAt(seed, time);
            switch (dimensions)
            {
                case 1: return new Vector4(r.NextFloat(-1f, 1f), 0, 0, 0);
                case 2: { var v = r.NextFloat2(-1f, 1f); return new Vector4(v.x, v.y, 0, 0); }
                case 3: { var v = r.NextFloat3(-1f, 1f); return new Vector4(v.x, v.y, v.z, 0); }
                default: { var v = r.NextFloat4(-1f, 1f); return new Vector4(v.x, v.y, v.z, v.w); }
            }
        }

        // ---------- scramble (LitMotion FixedStringHelper.FillRichText) ----------

        const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string Numerals = "0123456789";
        const string All = Lowercase + Uppercase + Numerals;

        /// <summary>Characters of a rich text, not counting tags.</summary>
        public static int VisibleLength(string richText)
        {
            var count = 0;
            for (var i = 0; i < richText.Length; i++)
            {
                if (richText[i] == '<')
                {
                    var close = richText.IndexOf('>', i);
                    if (close > i) { i = close; continue; }
                }
                count++;
            }
            return count;
        }

        /// <summary>
        /// The first round(length × t) characters of the text (tags kept, open tags closed), then random characters
        /// for the rest of the length, re-rolled from (seed, time) every frame.
        /// </summary>
        public static string Scramble(string richText, float t, UIAnimScramble mode, uint seed, double time, StringBuilder sb)
        {
            var length = VisibleLength(richText);
            var shown = Mathf.RoundToInt(length * Mathf.Clamp01(t));
            sb.Clear();

            var count = 0;
            var depth = 0;
            for (var i = 0; i < richText.Length; i++)
            {
                if (richText[i] == '<')
                {
                    var close = richText.IndexOf('>', i);
                    if (close > i)
                    {
                        var isEnd = i + 1 < richText.Length && richText[i + 1] == '/';
                        // Past the cut, only tags that close what is already open are kept.
                        if (count >= shown && depth == 0) break;
                        if (count >= shown && !isEnd) { i = close; continue; }
                        sb.Append(richText, i, close - i + 1);
                        depth += isEnd ? -1 : 1;
                        i = close;
                        if (count >= shown && depth == 0) break;
                        continue;
                    }
                }
                if (count < shown) sb.Append(richText[i]);
                count++;
                if (count >= shown && depth == 0) break;
            }

            var chars = mode switch
            {
                UIAnimScramble.Uppercase => Uppercase,
                UIAnimScramble.Lowercase => Lowercase,
                UIAnimScramble.Numerals => Numerals,
                UIAnimScramble.All => All,
                _ => null,
            };
            if (chars != null)
            {
                // Whitespace of the source stays in place: one long unbroken random word would wrap differently
                // every frame and make the text jump between line counts.
                var random = RandomAt(seed, time);
                var visible = 0;
                for (var i = 0; i < richText.Length; i++)
                {
                    if (richText[i] == '<')
                    {
                        var close = richText.IndexOf('>', i);
                        if (close > i) { i = close; continue; }
                    }
                    if (visible++ < shown) continue;
                    sb.Append(char.IsWhiteSpace(richText[i]) ? richText[i] : chars[random.NextInt(0, chars.Length)]);
                }
            }
            return sb.ToString();
        }
    }
}
