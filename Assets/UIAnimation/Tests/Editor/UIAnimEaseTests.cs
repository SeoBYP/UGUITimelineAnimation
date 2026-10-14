using System;
using NUnit.Framework;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>The easing formulas taken over from LitMotion: ends, known values and the saved int of each member.</summary>
    public sealed class UIAnimEaseTests
    {
        [Test]
        public void 모든_이징은_0에서_시작해_1에서_끝난다()
        {
            foreach (UIAnimEase ease in Enum.GetValues(typeof(UIAnimEase)))
            {
                Assert.AreEqual(0f, UIAnimEasing.Evaluate(0f, ease), 1e-5f, $"{ease}(0)");
                Assert.AreEqual(1f, UIAnimEasing.Evaluate(1f, ease), 1e-5f, $"{ease}(1)");
            }
        }

        [Test]
        public void 알려진_값과_같다()
        {
            Assert.AreEqual(0.875f, UIAnimEasing.Evaluate(0.5f, UIAnimEase.OutCubic), 1e-6f);
            Assert.AreEqual(0.5f, UIAnimEasing.Evaluate(0.5f, UIAnimEase.InOutSine), 1e-6f);
            Assert.AreEqual(0.70710678f, UIAnimEasing.Evaluate(0.5f, UIAnimEase.OutSine), 1e-6f);
            Assert.AreEqual(0.765625f, UIAnimEasing.Evaluate(0.5f, UIAnimEase.OutBounce), 1e-6f);
            Assert.AreEqual(-0.0876975f, UIAnimEasing.Evaluate(0.5f, UIAnimEase.InBack), 1e-6f);
        }

        // Prefabs store the ease as an int: these must never move (they are LitMotion.Ease's values).
        [Test]
        public void 저장되는_번호는_LitMotion과_같다()
        {
            Assert.AreEqual(0, (int)UIAnimEase.Linear);
            Assert.AreEqual(2, (int)UIAnimEase.OutSine);
            Assert.AreEqual(8, (int)UIAnimEase.OutCubic);
            Assert.AreEqual(26, (int)UIAnimEase.OutBack);
            Assert.AreEqual(31, (int)UIAnimEase.CustomAnimationCurve);
            Assert.AreEqual(2, (int)UIAnimScramble.Lowercase);
            Assert.AreEqual(4, (int)UIAnimScramble.All);
        }
    }
}
