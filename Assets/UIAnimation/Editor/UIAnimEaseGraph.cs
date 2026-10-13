using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeoBYP.TimelineAnimation.Editor
{
    /// <summary>Read-only picture of how a key is reached: the ease/curve shape from the previous value (0) to this key (1).</summary>
    internal sealed class UIAnimEaseGraph : VisualElement
    {
        const int Samples = 64;
        const float Pad = 12f;

        readonly Func<float, float> _weight;
        readonly Color _color;

        public UIAnimEaseGraph(Func<float, float> weight, Color color)
        {
            _weight = weight;
            _color = color;
            AddToClassList("uia-easegraph");
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 20 || r.height < 20) return;

            float min = 0f, max = 1f;
            var values = new float[Samples + 1];
            for (var i = 0; i <= Samples; i++)
            {
                values[i] = _weight(i / (float)Samples);
                min = Mathf.Min(min, values[i]);
                max = Mathf.Max(max, values[i]);
            }
            float X(float t) => Mathf.Lerp(r.xMin + Pad, r.xMax - Pad, t);
            float Y(float v) => Mathf.Lerp(r.yMax - Pad, r.yMin + Pad, Mathf.InverseLerp(min, max, v));

            var p = ctx.painter2D;
            p.lineWidth = 1f;
            p.strokeColor = new Color(1f, 1f, 1f, 0.12f);
            foreach (var v in new[] { 0f, 1f })
            {
                p.BeginPath();
                p.MoveTo(new Vector2(X(0), Y(v)));
                p.LineTo(new Vector2(X(1), Y(v)));
                p.Stroke();
            }

            p.lineWidth = 2.5f;
            p.strokeColor = _color;
            p.BeginPath();
            for (var i = 0; i <= Samples; i++)
            {
                var pt = new Vector2(X(i / (float)Samples), Y(values[i]));
                if (i == 0) p.MoveTo(pt);
                else p.LineTo(pt);
            }
            p.Stroke();
        }
    }
}
