using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// The RPG sample's layout: on every screen (with its real data), no label's letters run into an icon or another
    /// label next to it, and no label spills out of its own box.
    /// </summary>
    public sealed class RpgLayoutPlayModeTests
    {
        const string ScenePath = "Assets/UIAnimation/Samples/RPG/Scenes/RPG.unity";
        const float Slack = 2f;   // canvas units
        static readonly string[] Decor = { "glow", "rays", "summon_circle", "card_back", "dot" };   // backgrounds a label sits on

        UIAnimPageContainer _pages;
        UIAnimModalContainer _modals;
        readonly List<string> _problems = new();

        static IEnumerator Idle(float extra = 0.3f)
        {
            yield return null;
            var end = Time.realtimeSinceStartup + 8f;
            while (UIAnimScreenContainer.AnyInTransition && Time.realtimeSinceStartup < end) yield return null;
            yield return new WaitForSeconds(extra);
        }

        static Button Named(Component root, string name) => root.GetComponentsInChildren<Button>(true).First(b => b.name == name);

        // A rect in the root canvas' units.
        static Rect InCanvas(RectTransform canvas, Transform t, Bounds local)
        {
            var a = canvas.InverseTransformPoint(t.TransformPoint(local.min));
            var b = canvas.InverseTransformPoint(t.TransformPoint(local.max));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static Rect InCanvas(RectTransform canvas, RectTransform rt)
        {
            var r = rt.rect;
            return InCanvas(canvas, rt, new Bounds(r.center, r.size));
        }

        static bool Overlap(Rect a, Rect b) =>
            Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > Slack && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > Slack;

        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        void Check(string where, Component screen)
        {
            if (screen == null) return;
            var canvas = (RectTransform)screen.GetComponentInParent<Canvas>().rootCanvas.transform;
            foreach (var text in screen.GetComponentsInChildren<TMP_Text>())
            {
                if (string.IsNullOrWhiteSpace(text.text) || text.color.a < 0.05f) continue;
                text.ForceMeshUpdate();
                if (text.textInfo.characterCount == 0) continue;
                var glyphs = InCanvas(canvas, text.transform, text.textBounds);
                var box = InCanvas(canvas, text.rectTransform);
                if (glyphs.width > box.width + Slack * 2 || glyphs.height > box.height + Slack * 4)
                    _problems.Add($"{where}: '{text.text}' spills out of its box ({glyphs.width:0}x{glyphs.height:0} in {box.width:0}x{box.height:0}) — {PathOf(text.transform)}");
                var parent = text.transform.parent;
                if (parent == null) continue;
                foreach (Transform sibling in parent)
                {
                    if (sibling == text.transform || !sibling.gameObject.activeInHierarchy) continue;
                    if (sibling.TryGetComponent<Image>(out var image) && image.sprite != null && image.color.a > 0.05f && image.sprite.border == Vector4.zero
                        && !Decor.Contains(image.sprite.name) && Overlap(glyphs, InCanvas(canvas, image.rectTransform)))
                        _problems.Add($"{where}: '{text.text}' runs into icon '{image.sprite.name}' — {PathOf(text.transform)}");
                    if (sibling.TryGetComponent<TMP_Text>(out var other) && other.color.a > 0.05f && !string.IsNullOrWhiteSpace(other.text)
                        && sibling.GetSiblingIndex() < text.transform.GetSiblingIndex())
                    {
                        other.ForceMeshUpdate();
                        if (Overlap(glyphs, InCanvas(canvas, other.transform, other.textBounds)))
                            _problems.Add($"{where}: '{text.text}' and '{other.text}' overlap — {PathOf(text.transform)}");
                    }
                }
            }
        }

        IEnumerator Page(string button, string where)
        {
            Named(_pages.Current, button).onClick.Invoke();
            yield return Idle(1.2f);
            Check(where, _pages.Current);
        }

        IEnumerator Back()
        {
            Named(_pages.Current, "BackButton").onClick.Invoke();
            yield return Idle();
        }

        IEnumerator Modal(Component root, string button, string where)
        {
            Named(root, button).onClick.Invoke();
            yield return Idle(0.8f);
            Check(where, _modals.Current);
        }

        IEnumerator CloseModal()
        {
            while (_modals.Modals.Count > 0) yield return _modals.Pop(animated: false);
            yield return Idle();
        }

        [UnityTest]
        public IEnumerator 모든_화면에서_글자가_아이콘이나_다른_글자와_겹치지_않는다()
        {
#if UNITY_EDITOR
            if (!System.IO.File.Exists(ScenePath)) Assert.Ignore("RPG 샘플이 없음");
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("에디터 전용");
#endif
            yield return null;
            _pages = Object.FindAnyObjectByType<UIAnimPageContainer>();
            _modals = Object.FindAnyObjectByType<UIAnimModalContainer>();
            yield return Idle(1.5f);
            Check("Title", _pages.Current);
            yield return RpgPlayModeTests.ToLobby(_pages);
            yield return new WaitForSeconds(1.5f);
            Check("Lobby", _pages.Current);

            foreach (var (menu, name) in new[] { ("HEROES", "Heroes"), ("BAG", "Bag"), ("SUMMON", "Summon"), ("QUESTS", "Quests"), ("SHOP", "Shop"), ("MAIL", "Mail"), ("GUILD", "Guild"), ("RANK", "Ranking") })
            {
                yield return Page(menu + "Button", name);
                yield return Back();
            }

            yield return Page("HEROESButton", "Heroes");
            _pages.Current.GetComponentsInChildren<Button>().First(b => b.name == "Body").onClick.Invoke();
            yield return Idle(1.2f);
            Check("HeroDetail", _pages.Current);
            yield return Page("UpgradeButton", "Upgrade");
            yield return Back();
            yield return Back();
            yield return Back();

            yield return Page("ADVENTUREButton", "Adventure");
            yield return Page("StartButton", "Formation");
            yield return Back();
            yield return Back();

            yield return Page("BAGButton", "Bag");
            _pages.Current.GetComponentsInChildren<Button>().First(b => b.transform.Find("Icon")?.GetComponent<Image>().sprite?.name == "helm").onClick.Invoke();
            yield return Idle(0.8f);
            Check("ItemInfo", _modals.Current);
            yield return Modal(_modals.Current, "UseButton", "Quantity");
            yield return CloseModal();
            yield return Back();

            yield return Page("SHOPButton", "Shop");
            _pages.Current.GetComponentsInChildren<Button>().First(b => b.name == "Body").onClick.Invoke();
            yield return Idle(0.8f);
            Check("Purchase", _modals.Current);
            yield return CloseModal();
            yield return Back();

            yield return Modal(_pages.Current, "DAILYButton", "Attendance");
            yield return CloseModal();
            yield return Modal(_pages.Current, "SETTINGSButton", "Settings");
            yield return Modal(_modals.Current, "ResetButton", "Confirm");
            yield return CloseModal();

            Assert.IsEmpty(_problems, string.Join("\n", _problems));
        }
    }
}
