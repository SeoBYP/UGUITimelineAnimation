using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// The Scroll sample in real Play mode: a pager of 18 pages, one scroll form each (Start, OnEnable, Canvas, LitMotion on the player loop).
    /// Sample scripts live in Assembly-CSharp, so they are reached by name.
    /// </summary>
    public sealed class ScrollPlayModeTests
    {
        const string ScenePath = "Assets/Scenes/Scroll.unity";
        UIAnimScrollList _pager;

        IEnumerator Load()
        {
#if UNITY_EDITOR
            if (!System.IO.File.Exists(ScenePath)) Assert.Ignore($"{ScenePath} 샘플이 없음");
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("에디터 전용");
#endif
            yield return null;
            yield return null;
            _pager = Object.FindObjectsByType<UIAnimScrollList>(FindObjectsSortMode.None).First(l => l.name == "Pages");
        }

        IEnumerator GoTo(int page)
        {
            _pager.ScrollTo(page);
            yield return null;
            yield return null;
        }

        MonoBehaviour Page(int i) => _pager.GetItem(i).GetComponents<MonoBehaviour>().First(m => m.GetType().Name == "ScrollDemoPage");
        static T Field<T>(object o, string name) => (T)o.GetType().GetField(name).GetValue(o);
        UIAnimScrollList ListOf(int page) => Field<UIAnimScrollList>(Page(page), "list");
        static void Invoke(object o, string method) => o.GetType().GetMethod(method).Invoke(o, null);
        static float Alpha(RectTransform item) => item.GetComponentInChildren<Graphic>().color.a;
        static PointerEventData Drag(Vector2 press, Vector2 position) => new(EventSystem.current) { pressPosition = press, position = position };

        [UnityTest]
        public IEnumerator 페이지_18장을_다음_버튼과_가로_스와이프로_넘긴다()
        {
            yield return Load();
            Assert.AreEqual(18, _pager.Count);
            var counter = GameObject.Find("Counter").GetComponent<TMP_Text>();
            Assert.AreEqual("1 / 18", counter.text);
            Assert.AreEqual(18, _pager.transform.parent.Find("Dots").childCount, "페이지 점 18개");

            GameObject.Find("Button Next").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(1, _pager.CurrentIndex);
            Assert.AreEqual("2 / 18", counter.text);

            // A sideways drag on the vertical list of page 1 turns the page (the list passes it to the pager).
            yield return GoTo(0);
            var list = ListOf(0);
            var center = new Vector2(960, 540);
            list.OnBeginDrag(Drag(center, center + new Vector2(-30, 0)));
            list.OnDrag(Drag(center, center + new Vector2(-300, 0)));
            list.OnEndDrag(Drag(center, center + new Vector2(-300, 0)));
            Assert.AreEqual(0, list.Offset, 0.01f, "세로 리스트는 그대로");
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(1, _pager.CurrentIndex, "한 장 넘어감");
        }

        [UnityTest]
        public IEnumerator 리스트와_그리드와_메이슨리()
        {
            yield return Load();
            yield return new WaitForSeconds(1.5f);
            var vertical = ListOf(0);
            Assert.AreEqual(100, vertical.Count);
            var (first, last) = vertical.VisibleRange;
            Assert.Greater(last - first + 1, 3, "여러 줄이 보임");
            for (var i = first; i <= last; i++) Assert.AreEqual(1f, Alpha(vertical.GetItem(i)), 0.01f, $"{i}번 진입 끝");

            yield return GoTo(1);
            var horizontal = ListOf(1);
            Assert.Greater(horizontal.ItemPosition(1).x, horizontal.ItemPosition(0).x, "가로로 나열");

            yield return GoTo(2);
            var grid = ListOf(2);
            Assert.AreEqual(grid.ItemPosition(0).y, grid.ItemPosition(1).y, 0.01f, "같은 줄");
            Assert.Greater(grid.ItemPosition(1).x, grid.ItemPosition(0).x);

            yield return GoTo(3);
            var rows = ListOf(3);
            Assert.AreEqual(rows.ItemPosition(0).x, rows.ItemPosition(1).x, 0.01f, "Fixed Row Count 2: 0과 1이 같은 열");
            Assert.Greater(rows.ItemPosition(2).x, rows.ItemPosition(0).x);

            yield return GoTo(13);
            var masonry = ListOf(13);
            var columns = Enumerable.Range(0, 8).Select(i => masonry.ItemPosition(i).x).Distinct().Count();
            Assert.AreEqual(4, columns, "4열");
            Assert.AreNotEqual(masonry.SizeOf(0), masonry.SizeOf(1), "높이가 다름");
        }

        [UnityTest]
        public IEnumerator 캐러셀_루프_페이징_자동_배너()
        {
            yield return Load();
            yield return GoTo(4);
            var carousel = ListOf(4);
            carousel.ScrollTo(5, UIAnimScrollList.Alignment.Center);
            yield return null;
            Assert.AreEqual(1f, carousel.GetItem(5).Find("Card").localScale.x, 0.01f, "가운데 카드 1배");
            Assert.Less(carousel.GetItem(6).Find("Card").localScale.x, 0.95f, "옆 카드는 작음");

            yield return GoTo(6);
            var loop = ListOf(6);
            loop.SetOffset(-200);
            yield return null;
            Assert.IsNotNull(loop.GetItem(7), "처음보다 앞에 마지막 카드");

            yield return GoTo(5);
            var paging = ListOf(5);
            var dots = Page(5).transform.Find("Demo/Dots");
            Assert.AreEqual(5, dots.childCount);
            var center = new Vector2(960, 540);
            paging.OnBeginDrag(Drag(center, center + new Vector2(-40, 0)));
            paging.OnEndDrag(Drag(center, center + new Vector2(-600, 0)));
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(1, paging.CurrentIndex, "긴 스와이프도 한 장");
            Assert.Greater(dots.GetChild(1).localScale.x, 1.4f, "1번 점 On");
            Assert.AreEqual(1f, dots.GetChild(0).localScale.x, 0.01f, "0번 점 Off");

            yield return GoTo(7);
            var banner = ListOf(7);
            var before = banner.CurrentIndex;
            yield return new WaitForSeconds(3.2f);
            Assert.AreNotEqual(before, banner.CurrentIndex, "2.5초마다 자동으로 넘어감");
        }

        [UnityTest]
        public IEnumerator 휠_피커는_가운데_줄만_똑바로_서고_선택을_알려준다()
        {
            yield return Load();
            yield return GoTo(8);
            var wheel = ListOf(8);
            wheel.ScrollTo(7, UIAnimScrollList.Alignment.Center);
            yield return null;
            Assert.AreEqual(7, wheel.CurrentIndex);
            var centerRow = wheel.GetItem(7).Find("Title");
            var nextRow = wheel.GetItem(9).Find("Title");
            Assert.AreEqual(0f, Mathf.DeltaAngle(0, centerRow.localEulerAngles.x), 0.5f, "가운데는 0도");
            Assert.Less(Mathf.DeltaAngle(0, nextRow.localEulerAngles.x), -10f, "아래 줄은 뒤로 넘어감");
            Assert.AreEqual("Selected  07:00", Field<TMP_Text>(Page(8), "status").text);
        }

        [UnityTest]
        public IEnumerator 섹션_헤더_가변_크기_채팅()
        {
            yield return Load();
            yield return GoTo(9);
            var sticky = ListOf(9);
            sticky.SetOffset(sticky.ItemPosition(8).y);   // inside section B
            yield return null;
            var pinned = sticky.GetComponent<ScrollRect>().viewport.GetChild(1);
            Assert.IsTrue(pinned.gameObject.activeSelf, "고정 헤더");
            Assert.AreEqual("Section B", pinned.GetComponentInChildren<TMP_Text>().text);

            yield return GoTo(10);
            var feed = ListOf(10);
            TestContext.WriteLine("가변: " + string.Join(", ", Enumerable.Range(0, 8).Select(i => $"{feed.SizeOf(i):0}")) +
                                  $" measure={feed.measureItems} item0={feed.GetItem(0)?.rect.size} text0={feed.GetItem(0)?.GetComponentInChildren<TMP_Text>().text} pref={UnityEngine.UI.LayoutUtility.GetPreferredHeight(feed.GetItem(0))}");
            var sizes = Enumerable.Range(0, 8).Select(feed.SizeOf).Distinct().Count();
            Assert.Greater(sizes, 2, "글 길이마다 높이가 다름");

            yield return GoTo(11);
            var chat = ListOf(11);
            Assert.IsTrue(chat.AtEnd, "끝에서 시작");
            var count = chat.Count;
            Invoke(Page(11), "Send");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(count + 1, chat.Count);
            Assert.IsTrue(chat.AtEnd, "보낸 메시지까지 내려감");
            Assert.IsNotNull(chat.GetItem(count));
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(count + 2, chat.Count, "답장");
            Assert.IsTrue(chat.AtEnd);
        }

        [UnityTest]
        public IEnumerator 아코디언_중첩_탭()
        {
            yield return Load();
            yield return GoTo(12);
            var accordion = ListOf(12);
            var below = accordion.ItemPosition(1).y;
            accordion.GetItem(0).GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(300, accordion.GetItem(0).rect.height, 0.5f, "펼쳐짐");
            Assert.AreEqual(below + 190, accordion.ItemPosition(1).y, 0.5f, "아래 줄이 밀림");
            Assert.AreEqual(45, accordion.GetItem(0).Find("Header/Plus").localEulerAngles.z, 0.5f, "+ → ×");

            yield return GoTo(14);
            var nested = ListOf(14);
            var row = nested.GetItem(0).GetComponentInChildren<UIAnimScrollList>();
            Assert.AreNotSame(nested, row);
            Assert.AreEqual(15, row.Count, "줄마다 가로 리스트");

            yield return GoTo(15);
            var tabs = Page(15).transform.Find("Demo/Dots");
            Assert.AreEqual(new[] { "Home", "Search", "Inbox", "Profile" }, tabs.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray());
            tabs.GetChild(2).GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(2, ListOf(15).CurrentIndex, "탭을 누르면 그 페이지");
            Assert.AreEqual(1f, tabs.GetChild(2).Find("Underline").localScale.x, 0.01f, "밑줄 On");
            Assert.AreEqual(0f, tabs.GetChild(0).Find("Underline").localScale.x, 0.01f, "밑줄 Off");
        }

        [UnityTest]
        public IEnumerator 접히는_헤더와_당겨서_새로고침()
        {
            yield return Load();
            yield return GoTo(16);
            var list = ListOf(16);
            var header = (RectTransform)Page(16).transform.Find("Demo/Header");
            Assert.AreEqual(300, header.rect.height, 0.5f, "펼친 헤더");
            list.SetOffset(95);
            yield return null;
            Assert.AreEqual(205, header.rect.height, 1f, "반쯤");
            list.SetOffset(400);
            yield return null;
            Assert.AreEqual(110, header.rect.height, 0.5f, "다 접힘");

            yield return GoTo(17);
            var feed = ListOf(17);
            var pull = Field<UIAnimPullToRefresh>(Page(17), "pull");
            var count = feed.Count;
            var content = feed.GetComponent<ScrollRect>().content;
            content.anchoredPosition = new Vector2(0, -160);   // pulled past the 140 px line
            yield return null;
            static string Title(RectTransform item) => item.GetComponentsInChildren<TMP_Text>()[0].text;
            static string Badge(RectTransform item) => item.GetComponentsInChildren<TMP_Text>()[1].text;
            Assert.AreEqual(($"Item {count}", $"#{count}"), (Title(feed.GetItem(0)), Badge(feed.GetItem(0))), "최신이 맨 위, 번호 = 제목 번호");

            pull.OnEndDrag(Drag(Vector2.zero, Vector2.zero));
            Assert.IsTrue(pull.Refreshing);
            yield return new WaitForSeconds(0.6f);
            var gap = feed.ItemPosition(0).y - feed.Offset;   // first row's top below the list's top
            var spinner = (RectTransform)Page(17).transform.Find("Demo/Spinner");
            var listTop = ScreenRect((RectTransform)feed.transform).yMax;
            var spin = ScreenRect(spinner);
            var firstRow = ScreenRect(feed.GetItem(0));
            TestContext.WriteLine($"새로고침 중: 첫 줄 위 빈칸 {gap:0}px, 스피너 y {spin.yMin:0}~{spin.yMax:0}, 리스트 위 {listTop:0}, 첫 줄 위 {firstRow.yMax:0}");
            Assert.Greater(gap, 140f, "새로고침 동안 열린 채로 유지");
            Assert.LessOrEqual(spin.yMax, listTop + 0.5f, "스피너가 리스트 안쪽");
            Assert.GreaterOrEqual(spin.yMin, firstRow.yMax - 0.5f, "스피너가 첫 줄에 가리지 않음");

            yield return new WaitForSeconds(1.6f);
            Assert.IsFalse(pull.Refreshing, "끝나면 EndRefresh");
            Assert.AreEqual(count + 3, feed.Count, "새 항목 3개");
            Assert.AreEqual("Updated: 3 new", Field<TMP_Text>(Page(17), "status").text);
            Assert.AreEqual($"Items {count + 3}", GameObject.Find("Item Count Label").GetComponent<TMP_Text>().text, "도구 막대 개수도 따라감");
            Assert.AreEqual(count + 3, GameObject.Find("Item Count").GetComponent<Slider>().value);
            Assert.AreEqual(8, feed.ItemPosition(0).y - feed.Offset, 1f, "닫히면 원래 여백");
            Assert.AreEqual(($"Item {count + 3}  New", $"#{count + 3}"), (Title(feed.GetItem(0)), Badge(feed.GetItem(0))));
            Assert.AreEqual(($"Item {count}", $"#{count}"), (Title(feed.GetItem(3)), Badge(feed.GetItem(3))), "있던 항목 번호는 그대로");
        }

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);   // min / max of all four: a rotated rect swaps its corners
            return Rect.MinMaxRect(c.Min(p => p.x), c.Min(p => p.y), c.Max(p => p.x), c.Max(p => p.y));
        }

        // Text: the drawn glyphs, not the box.
        static Rect TextRect(TMP_Text text)
        {
            text.ForceMeshUpdate();
            var b = text.textBounds;
            var min = text.transform.TransformPoint(b.min);
            var max = text.transform.TransformPoint(b.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [UnityTest]
        public IEnumerator 도구_막대와_제목과_페이지가_겹치지_않는다()
        {
            yield return Load();
            var panel = _pager.transform.parent;
            var dots = (RectTransform)panel.Find("Dots");
            var dotRect = Rect.MinMaxRect(
                dots.Cast<RectTransform>().Min(d => ScreenRect(d).xMin), dots.Cast<RectTransform>().Min(d => ScreenRect(d).yMin),
                dots.Cast<RectTransform>().Max(d => ScreenRect(d).xMax), dots.Cast<RectTransform>().Max(d => ScreenRect(d).yMax));
            var heading = Page(0).transform.Find("Heading").GetComponent<TMP_Text>();
            var demoList = (RectTransform)ListOf(0).transform;
            var parts = new (string name, Rect rect)[]
            {
                ("Title", TextRect(panel.Find("Title").GetComponent<TMP_Text>())),
                ("Counter", TextRect(panel.Find("Counter").GetComponent<TMP_Text>())),
                ("Heading", TextRect(heading)),
                ("List", ScreenRect(demoList)),
                ("Slider", ScreenRect((RectTransform)panel.Find("Item Count"))),
                ("Items label", TextRect(panel.Find("Item Count Label").GetComponent<TMP_Text>())),
                ("Remove", ScreenRect((RectTransform)panel.Find("Button Remove"))),
                ("Add", ScreenRect((RectTransform)panel.Find("Button Add"))),
                ("Edit Animation", ScreenRect((RectTransform)panel.Find("Button Edit Animation"))),
                ("Prev", ScreenRect((RectTransform)panel.Find("Button Prev"))),
                ("Next", ScreenRect((RectTransform)panel.Find("Button Next"))),
                ("Dots", dotRect),
            };
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            foreach (var (name, rect) in parts)
            {
                TestContext.WriteLine($"{name}: x {rect.xMin:0}~{rect.xMax:0}, y {rect.yMin:0}~{rect.yMax:0} (화면 {Screen.width}×{Screen.height})");
                Assert.IsTrue(screen.Contains(rect.min) && screen.Contains(rect.max), $"{name}이 화면 안");
            }
            for (var i = 0; i < parts.Length; i++)
            for (var j = i + 1; j < parts.Length; j++)
                Assert.IsFalse(parts[i].rect.Overlaps(parts[j].rect), $"{parts[i].name}와 {parts[j].name}가 겹침");

            // Edit Animation's label fits its button.
            var edit = panel.Find("Button Edit Animation");
            var label = TextRect(edit.GetComponentInChildren<TMP_Text>());
            var box = ScreenRect((RectTransform)edit);
            Assert.IsTrue(box.Contains(label.min) && box.Contains(label.max), "Edit Animation 글자가 버튼 안");
        }

        [UnityTest]
        public IEnumerator 캐러셀을_끝_너머까지_당겼다_놓으면_튀지_않고_돌아온다()
        {
            yield return Load();
            yield return GoTo(4);
            var carousel = ListOf(4);
            carousel.ScrollTo(19, UIAnimScrollList.Alignment.Center);
            yield return null;
            var end = carousel.Offset;   // card 20 centered = the end
            var content = carousel.GetComponent<ScrollRect>().content;
            var center = new Vector2(960, 540);
            carousel.OnBeginDrag(Drag(center, center + new Vector2(-40, 0)));
            content.anchoredPosition = new Vector2(-(end + 180), content.anchoredPosition.y);   // dragged 180 px past the end (elastic)
            yield return null;
            carousel.OnEndDrag(Drag(center, center + new Vector2(-220, 0)));

            var offsets = new System.Collections.Generic.List<float> { carousel.Offset };
            for (var t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                offsets.Add(carousel.Offset);
            }
            TestContext.WriteLine($"끝={end:0}, 놓은 뒤 위치: " + string.Join(", ", offsets.Take(25).Select(o => o.ToString("0"))));
            var steps = offsets.Zip(offsets.Skip(1), (a, b) => b - a).ToList();
            Assert.That(steps.All(d => d <= 0.5f), "끝 쪽으로 되돌아가기만 함(다시 튀어나가지 않음)");
            Assert.Less(-steps.Min(), 60f, "한 프레임에 크게 뛰지 않음");
            Assert.AreEqual(end, carousel.Offset, 1f, "마지막 카드가 가운데에서 멈춤");
            Assert.AreEqual(ScrollRect.MovementType.Elastic, carousel.GetComponent<ScrollRect>().movementType, "원래 움직임 방식으로 복원");
        }

        [UnityTest]
        public IEnumerator 채팅에서_Send를_연달아_눌러도_있던_말풍선은_깜빡이지_않는다()
        {
            yield return Load();
            yield return GoTo(11);
            var chat = ListOf(11);
            yield return new WaitForSeconds(0.5f);
            var send = Page(11).transform.Find("Demo/Button Send").GetComponent<Button>();
            var count = chat.Count;
            var minAlpha = 1f;
            var flickered = new System.Collections.Generic.HashSet<int>();
            for (var frame = 0; frame < 40; frame++)
            {
                if (frame % 4 == 0 && frame < 24) send.onClick.Invoke();   // six quick clicks
                // Bubbles that were already there before this run of clicks must stay fully visible.
                var (first, last) = chat.VisibleRange;
                for (var i = first; i <= last && i < count; i++)
                {
                    var a = chat.GetItem(i).GetComponentInChildren<Graphic>().color.a;
                    if (a < 0.99f) flickered.Add(i);
                    minAlpha = Mathf.Min(minAlpha, a);
                }
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);   // answers arrive
            TestContext.WriteLine($"Send 6번: 개수 {count} → {chat.Count}, 있던 말풍선 최저 alpha {minAlpha:0.00}, 깜빡인 인덱스 [{string.Join(",", flickered)}], 끝={chat.AtEnd}");
            Assert.IsEmpty(flickered, "있던 말풍선은 다시 나타나는 연출을 하지 않음");
            Assert.AreEqual(count + 12, chat.Count, "6번 보내고 6번 답장");
            Assert.AreEqual($"Items {count + 12}", GameObject.Find("Item Count Label").GetComponent<TMP_Text>().text, "도구 막대 개수도 따라감");
            Assert.IsTrue(chat.AtEnd, "연달아 보내도 끝에 붙어 있음");
        }

        [UnityTest]
        public IEnumerator 아이템_수_슬라이더와_Add_Remove()
        {
            yield return Load();
            var list = ListOf(0);
            var slider = GameObject.Find("Item Count").GetComponent<Slider>();
            var label = GameObject.Find("Item Count Label").GetComponent<TMP_Text>();
            Assert.AreEqual(100, slider.value, "현재 페이지 개수");

            slider.value = 7;
            yield return null;
            Assert.AreEqual(7, list.Count);
            Assert.AreEqual("Items 7", label.text);

            GameObject.Find("Button Add").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(8, list.Count);
            Assert.IsNotNull(list.GetItem(7), "새 아이템이 보임");
            Assert.AreEqual(8, slider.value);

            GameObject.Find("Button Remove").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Button Remove").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(6, list.Count);
            Assert.AreEqual("Items 6", label.text);

            // Other pages keep their own counts; coming back keeps 6.
            yield return GoTo(4);
            Assert.AreEqual(20, slider.value, "캐러셀 20장");
            yield return GoTo(0);
            Assert.AreEqual(6, ListOf(0).Count);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator Edit_Animation_버튼은_Play_모드에서도_에디터를_연다()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("그래픽 장치가 필요");
            yield return Load();
            var button = GameObject.Find("Button Edit Animation").GetComponent<Button>();
            try
            {
                button.onClick.Invoke();   // page 1: the row prefab's Show state
                yield return null;
                var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                Assert.IsNotNull(stage, "Prefab 모드로 열림");
                StringAssert.EndsWith("ScrollItem.prefab", stage.assetPath);
                var window = Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().FirstOrDefault(w => w.GetType().Name == "UIAnimationEditorWindow");
                Assert.IsNotNull(window, "UI Animation Editor 창");
                TestContext.WriteLine($"Play 모드={Application.isPlaying}, 단계={stage.assetPath}, 창={window.titleContent.text}");

                // Collapsing header: the header lives in the page, so the page prefab opens.
                UnityEditor.SceneManagement.StageUtility.GoToMainStage();
                yield return GoTo(16);
                button.onClick.Invoke();
                yield return null;
                stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                StringAssert.EndsWith("17 CollapsingHeader.prefab", stage.assetPath);
                Assert.AreEqual("Header", UnityEditor.Selection.activeGameObject.name, "헤더가 선택됨");
            }
            finally
            {
                UnityEditor.SceneManagement.StageUtility.GoToMainStage();
                foreach (var w in Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().Where(w => w.GetType().Name == "UIAnimationEditorWindow").ToList()) w.Close();
            }
        }
#endif
    }
}
