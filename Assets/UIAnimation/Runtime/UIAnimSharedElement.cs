using UnityEngine;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Shared element transitions: an image on one screen appears to travel to (or from) where it sits on another screen.
    /// The motion is a normal state (AnchoredPosition + SizeDelta tracks on the moving image, edited in the window);
    /// only the other screen's end is known at run time, so this writes it into the state's first or last keys
    /// just before the state plays.
    /// </summary>
    public static class UIAnimSharedElement
    {
        /// <summary>
        /// Writes <paramref name="partner"/>'s on-screen rect, in <paramref name="moving"/>'s own terms, into the first
        /// (<paramref name="atStart"/>) or last keys of <paramref name="state"/>'s AnchoredPosition and SizeDelta tracks on
        /// <paramref name="moving"/>. Assumes <paramref name="moving"/> has point anchors (its size is its sizeDelta).
        /// </summary>
        public static void SetPartnerKeys(UIAnimation animation, string state, RectTransform moving, RectTransform partner, bool atStart)
        {
            var (position, size) = (moving.position, moving.sizeDelta);
            moving.sizeDelta = partner.rect.size;
            moving.position = partner.TransformPoint(partner.rect.center) + moving.TransformVector(Pivoted(moving));
            var (partnerPosition, partnerSize) = (moving.anchoredPosition, moving.sizeDelta);
            (moving.position, moving.sizeDelta) = (position, size);

            foreach (var s in animation.states)
            {
                if (s.name != state) continue;
                foreach (var track in s.tracks)
                {
                    if (track.target != moving || track.keys.Count == 0) continue;
                    var key = track.keys[atStart ? 0 : track.keys.Count - 1];
                    if (track.property == UIAnimProperty.AnchoredPosition) key.value = partnerPosition;
                    else if (track.property == UIAnimProperty.SizeDelta) key.value = partnerSize;
                }
            }
        }

        // From the rect's center to its pivot, in its local space (0 for a centered pivot).
        static Vector3 Pivoted(RectTransform rect) => (Vector3)((rect.pivot - new Vector2(0.5f, 0.5f)) * rect.rect.size);
    }
}
