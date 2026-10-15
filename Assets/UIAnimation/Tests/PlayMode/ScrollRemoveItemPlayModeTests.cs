using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// Removing one item from the middle of a list (a claimed quest): fold it with ResizeItem (awaitable), then RemoveItem.
    /// The rows below keep their objects and only move up; rows that come into view meanwhile get no Appear.
    /// </summary>
    public sealed class ScrollRemoveItemPlayModeTests
    {
        GameObject _root;

        [TearDown]
        public void TearDown() => Object.Destroy(_root);

        UIAnimScrollList List(int count)
        {
            _root = new GameObject("Canvas", typeof(Canvas));
            _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            // Item: 400 × 100, "Appear" moves it in from the side.
            var item = new GameObject("Row", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            item.SetParent(_root.transform, false);
            item.sizeDelta = new Vector2(400, 100);
            item.gameObject.SetActive(false);
            var appear = new UIAnimState { name = "Appear" };
            var track = new UIAnimTrack { target = item, property = UIAnimProperty.Scale, start = UIAnimStart.KeyValue };
            track.keys.Add(new UIAnimKey { time = 0f, value = new Vector4(0.5f, 0.5f, 1) });
            track.keys.Add(new UIAnimKey { time = 0.5f, value = Vector4.one });
            appear.tracks.Add(track);
            item.gameObject.AddComponent<UIAnimation>().states.Add(appear);

            var rt = new GameObject("List", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(_root.transform, false);
            rt.sizeDelta = new Vector2(400, 450);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(rt, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.sizeDelta = Vector2.zero;
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            var scroll = rt.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            var list = rt.gameObject.AddComponent<UIAnimScrollList>();
            list.itemPrefab = item;
            list.appearState = "Appear";
            list.playEntryOnFill = false;
            list.SetCount(count, (RectTransform r, int i) => r.name = "Row " + i);
            return list;
        }

        [UnityTest]
        public IEnumerator 가운데_줄을_접고_지우면_아래_줄은_그대로_올라오고_등장_연출이_없다()
        {
            var list = List(20);
            yield return null;
            var row4 = list.GetItem(4);
            var row2Y = list.GetItem(2).anchoredPosition.y;
            Assert.AreEqual("Row 4", row4.name);
            Assert.IsNull(list.GetItem(5), "450 px: 0~4번만 보임");

            yield return list.ResizeItem(2, 0f, 0.2f);   // completes when folded
            Assert.AreEqual(0f, list.SizeOf(2), 0.001f, "기다리면 끝까지 접혀 있음");
            var row5 = list.GetItem(5);
            Assert.IsNotNull(row5, "접히는 동안 5번이 보이게 됨");
            Assert.IsFalse(row5.GetComponent<UIAnimation>().IsPlaying("Appear"), "스크롤이 아니라 접혀서 보인 줄은 등장 연출 없음");

            list.RemoveItem(2);
            Assert.AreEqual(19, list.Count);
            Assert.AreSame(row4, list.GetItem(3), "4번 줄 오브젝트가 그대로 3번이 됨 (다시 만들지 않음)");
            Assert.AreEqual("Row 4", list.GetItem(3).name, "다시 바인딩하지 않음");
            Assert.AreEqual(row2Y - 100f, list.GetItem(3).anchoredPosition.y, 0.01f, "한 칸 위로");
            Assert.AreEqual(100f, list.SizeOf(2), 0.001f, "지운 줄의 접힘 크기는 남지 않음");
            Assert.IsFalse(list.GetComponentsInChildren<UIAnimation>().Any(a => a.IsPlaying("Appear")));
        }
    }
}
