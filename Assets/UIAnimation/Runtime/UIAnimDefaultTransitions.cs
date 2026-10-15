using UnityEngine;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Default transitions, the same as UnityScreenNavigator's defaults: 0.3 s OutQuart. Pages slide by one screen width
    /// (Size Relative, so any resolution), modals grow from 0.3 and fade in, backdrops and sheets fade (Linear).
    /// </summary>
    public static class UIAnimDefaultTransitions
    {
        public const float Duration = 0.3f;

        static UIAnimTrack Track(Component target, UIAnimProperty property, Vector4 from, Vector4 to, UIAnimEase ease, bool sizeRelative = false) => new()
        {
            target = target, property = property, start = UIAnimStart.KeyValue, sizeRelative = sizeRelative,
            keys = { new UIAnimKey { time = 0f, value = from }, new UIAnimKey { time = Duration, value = to, ease = ease } },
        };

        /// <summary>Slides along x from <paramref name="fromX"/> to <paramref name="toX"/> screen widths.</summary>
        public static UIAnimState Slide(UIAnimScreen screen, float fromX, float toX) => new()
        {
            tracks = { Track(screen.Rect, UIAnimProperty.AnchoredPosition, new Vector4(fromX, 0), new Vector4(toX, 0), UIAnimEase.OutQuart, sizeRelative: true) },
        };

        public static UIAnimState PagePushEnter(UIAnimScreen s) => Slide(s, 1f, 0f);
        public static UIAnimState PagePushExit(UIAnimScreen s) => Slide(s, 0f, -1f);
        public static UIAnimState PagePopEnter(UIAnimScreen s) => Slide(s, -1f, 0f);
        public static UIAnimState PagePopExit(UIAnimScreen s) => Slide(s, 0f, 1f);

        public static UIAnimState ModalEnter(UIAnimScreen s) => new()
        {
            tracks =
            {
                Track(s.Rect, UIAnimProperty.Scale, Vector3.one * 0.3f, Vector3.one, UIAnimEase.OutQuart),
                Track(s.Group, UIAnimProperty.CanvasGroupAlpha, Vector4.zero, new Vector4(1, 0), UIAnimEase.OutQuart),
            },
        };

        public static UIAnimState ModalExit(UIAnimScreen s) => new()
        {
            tracks =
            {
                Track(s.Rect, UIAnimProperty.Scale, Vector3.one, Vector3.one * 0.3f, UIAnimEase.OutQuart),
                Track(s.Group, UIAnimProperty.CanvasGroupAlpha, new Vector4(1, 0), Vector4.zero, UIAnimEase.OutQuart),
            },
        };

        public static UIAnimState FadeIn(UIAnimScreen s) => new()
        {
            tracks = { Track(s.Group, UIAnimProperty.CanvasGroupAlpha, Vector4.zero, new Vector4(1, 0), UIAnimEase.Linear) },
        };

        public static UIAnimState FadeOut(UIAnimScreen s) => new()
        {
            tracks = { Track(s.Group, UIAnimProperty.CanvasGroupAlpha, new Vector4(1, 0), Vector4.zero, UIAnimEase.Linear) },
        };
    }
}
