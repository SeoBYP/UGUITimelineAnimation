using UnityEngine;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// How a key arrives. Same members, order and formulas as LitMotion's Ease, so data saved with LitMotion.Ease
    /// (an int) reads back unchanged.
    /// </summary>
    public enum UIAnimEase
    {
        Linear,
        InSine,
        OutSine,
        InOutSine,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InQuart,
        OutQuart,
        InOutQuart,
        InQuint,
        OutQuint,
        InOutQuint,
        InExpo,
        OutExpo,
        InOutExpo,
        InCirc,
        OutCirc,
        InOutCirc,
        InElastic,
        OutElastic,
        InOutElastic,
        InBack,
        OutBack,
        InOutBack,
        InBounce,
        OutBounce,
        InOutBounce,
        CustomAnimationCurve,
        // appended (saved as ints, so new members go last): Unity-AnimationUI's "OutBack" family, a polynomial whose
        // overshoot grows with the power, f(x) = -(x-1)^4 + p(x-1)^3 + p(x-1)^2 + 1 with p = 2, 3, 4, 5
        OutBackQuad,
        OutBackCubic,
        OutBackQuart,
        OutBackQuint,
    }

    /// <summary>Characters a Text Reveal track shows where text is not revealed yet. Same values as LitMotion's ScrambleMode.</summary>
    public enum UIAnimScramble : byte
    {
        None = 0,
        Uppercase = 1,
        Lowercase = 2,
        Numerals = 3,
        All = 4,
    }

    /// <summary>The easing formulas (easings.net, as in LitMotion's EaseUtility).</summary>
    public static class UIAnimEasing
    {
        public static float Evaluate(float t, UIAnimEase ease)
        {
            return ease switch
            {
                UIAnimEase.InSine => InSine(t),
                UIAnimEase.OutSine => OutSine(t),
                UIAnimEase.InOutSine => InOutSine(t),
                UIAnimEase.InQuad => InQuad(t),
                UIAnimEase.OutQuad => OutQuad(t),
                UIAnimEase.InOutQuad => InOutQuad(t),
                UIAnimEase.InCubic => InCubic(t),
                UIAnimEase.OutCubic => OutCubic(t),
                UIAnimEase.InOutCubic => InOutCubic(t),
                UIAnimEase.InQuart => InQuart(t),
                UIAnimEase.OutQuart => OutQuart(t),
                UIAnimEase.InOutQuart => InOutQuart(t),
                UIAnimEase.InQuint => InQuint(t),
                UIAnimEase.OutQuint => OutQuint(t),
                UIAnimEase.InOutQuint => InOutQuint(t),
                UIAnimEase.InExpo => InExpo(t),
                UIAnimEase.OutExpo => OutExpo(t),
                UIAnimEase.InOutExpo => InOutExpo(t),
                UIAnimEase.InCirc => InCirc(t),
                UIAnimEase.OutCirc => OutCirc(t),
                UIAnimEase.InOutCirc => InOutCirc(t),
                UIAnimEase.InElastic => InElastic(t),
                UIAnimEase.OutElastic => OutElastic(t),
                UIAnimEase.InOutElastic => InOutElastic(t),
                UIAnimEase.InBack => InBack(t),
                UIAnimEase.OutBack => OutBack(t),
                UIAnimEase.InOutBack => InOutBack(t),
                UIAnimEase.InBounce => InBounce(t),
                UIAnimEase.OutBounce => OutBounce(t),
                UIAnimEase.InOutBounce => InOutBounce(t),
                UIAnimEase.OutBackQuad => OutPowBack(t, 2f),
                UIAnimEase.OutBackCubic => OutPowBack(t, 3f),
                UIAnimEase.OutBackQuart => OutPowBack(t, 4f),
                UIAnimEase.OutBackQuint => OutPowBack(t, 5f),
                _ => t,
            };
        }

        public static float Linear(float x) => x;

        /// <summary>Unity-AnimationUI's OutBack family: overshoots then settles, more with a larger <paramref name="p"/>.</summary>
        public static float OutPowBack(float x, float p)
        {
            var y = x - 1f;
            return -y * y * y * y + p * y * y * y + p * y * y + 1f;
        }

        public static float InSine(float x) => 1 - Mathf.Cos(x * Mathf.PI / 2);

        public static float OutSine(float x) => Mathf.Sin(x * Mathf.PI / 2);

        public static float InOutSine(float x) => -(Mathf.Cos(Mathf.PI * x) - 1) / 2;

        public static float InQuad(float x) => x * x;

        public static float OutQuad(float x) => 1 - (1 - x) * (1 - x);

        public static float InOutQuad(float x) => x < 0.5f ? 2 * x * x : 1 - Mathf.Pow(-2 * x + 2, 2) / 2;

        public static float InCubic(float x) => x * x * x;

        public static float OutCubic(float x) => 1 - Mathf.Pow(1 - x, 3);

        public static float InOutCubic(float x) => x < 0.5f ? 4 * x * x * x : 1 - Mathf.Pow(-2 * x + 2, 3) / 2;

        public static float InQuart(float x) => x * x * x * x;

        public static float OutQuart(float x) => 1 - Mathf.Pow(1 - x, 4);

        public static float InOutQuart(float x) => x < 0.5 ? 8 * x * x * x * x : 1 - Mathf.Pow(-2 * x + 2, 4) / 2;

        public static float InQuint(float x) => x * x * x * x * x;

        public static float OutQuint(float x) => 1 - Mathf.Pow(1 - x, 5);

        public static float InOutQuint(float x) => x < 0.5f ? 16 * x * x * x * x * x : 1 - Mathf.Pow(-2 * x + 2, 5) / 2;

        public static float InExpo(float x) => x == 0 ? 0 : Mathf.Pow(2, 10 * x - 10);

        public static float OutExpo(float x) => x == 1 ? 1 : 1 - Mathf.Pow(2, -10 * x);

        public static float InOutExpo(float x)
        {
            return x == 0 ? 0 :
                x == 1 ? 1 :
                x < 0.5f ? Mathf.Pow(2, 20 * x - 10) / 2 :
                (2 - Mathf.Pow(2, -20 * x + 10)) / 2;
        }

        public static float InCirc(float x) => 1 - Mathf.Sqrt(1 - Mathf.Pow(x, 2));

        public static float OutCirc(float x) => Mathf.Sqrt(1 - Mathf.Pow(x - 1, 2));

        public static float InOutCirc(float x)
        {
            return x < 0.5 ?
                (1 - Mathf.Sqrt(1 - Mathf.Pow(2 * x, 2))) / 2 :
                (Mathf.Sqrt(1 - Mathf.Pow(-2 * x + 2, 2)) + 1) / 2;
        }

        public static float InBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1;
            return c3 * x * x * x - c1 * x * x;
        }

        public static float OutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
        }

        public static float InOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c2 = c1 * 1.525f;

            return x < 0.5f
                ? Mathf.Pow(2 * x, 2) * ((c2 + 1) * 2 * x - c2) / 2
                : (Mathf.Pow(2 * x - 2, 2) * ((c2 + 1) * (x * 2 - 2) + c2) + 2) / 2;
        }

        public static float InElastic(float x)
        {
            const float c4 = 2 * Mathf.PI / 3;

            return x == 0 ? 0 :
                x == 1 ? 1 :
                -Mathf.Pow(2, 10 * x - 10) * Mathf.Sin((x * 10 - 10.75f) * c4);
        }

        public static float OutElastic(float x)
        {
            const float c4 = 2 * Mathf.PI / 3;

            return x == 0 ? 0 :
                x == 1 ? 1 :
                Mathf.Pow(2, -10 * x) * Mathf.Sin((x * 10 - 0.75f) * c4) + 1;
        }

        public static float InOutElastic(float x)
        {
            const float c5 = 2 * Mathf.PI / 4.5f;

            return x == 0 ? 0 :
                x == 1 ? 1 :
                x < 0.5f ?
                -(Mathf.Pow(2, 20 * x - 10) * Mathf.Sin((20 * x - 11.125f) * c5)) / 2 :
                Mathf.Pow(2, -20 * x + 10) * Mathf.Sin((20 * x - 11.125f) * c5) / 2 + 1;
        }

        public static float InBounce(float x) => 1 - OutBounce(1 - x);

        public static float OutBounce(float x)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            float t = x;

            if (t < 1 / d1)
            {
                return n1 * t * t;
            }
            else if (t < 2 / d1)
            {
                return n1 * (t -= 1.5f / d1) * t + 0.75f;
            }
            else if (t < 2.5 / d1)
            {
                return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            }
            else
            {
                return n1 * (t -= 2.625f / d1) * t + 0.984375f;
            }
        }
        public static float InOutBounce(float x)
        {
            return x < 0.5f ?
                (1 - OutBounce(1 - 2 * x)) / 2 :
                (1 + OutBounce(2 * x - 1)) / 2;
        }
    }
}
