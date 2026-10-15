using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Bench
{
    /// <summary>
    /// Runs every case (scenario × approach × count) in the player: builds the objects, starts the motion, warms up,
    /// then records each frame's time and GC allocation, and writes one JSON line per case.
    /// Args: -benchOut &lt;file&gt; (default: persistentDataPath/bench.jsonl), -benchQuick (short runs, small counts).
    /// </summary>
    public sealed class BenchRunner : MonoBehaviour
    {
        public RectTransform root;
        public TMP_FontAsset font;

        static readonly Dictionary<Scenario, int[]> Counts = new()
        {
            [Scenario.Pulse] = new[] { 100, 500, 1000, 2000 },
            [Scenario.Entry] = new[] { 100, 500, 1000, 2000 },
            [Scenario.Text] = new[] { 10, 50, 100 },
        };

        float _warmup = 1f, _measure = 4f;
        string _gcMode;

        // Bytes allocated in the frame just finished. The profiler counter works in Development builds only, so GC is
        // measured with a Development build and time with a release build (-1 = not measured in this player).
        long FrameAlloc(ProfilerRecorder recorder) => _gcMode == "recorder" ? recorder.LastValue : -1;

        static IBenchDriver[] Drivers() => new IBenchDriver[]
        {
            new BaselineDriver(), new UIAnimationDriver(), new AnimatorDriver(),
#if BENCH_DOTWEEN
            new DOTweenDriver(),
#endif
            new LitMotionDriver(), new AnimationUIDriver(),
        };

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var args = Environment.GetCommandLineArgs();
            string Arg(string name) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            var quick = args.Contains("-benchQuick");
            if (quick) (_warmup, _measure) = (0.3f, 0.7f);
            var output = Arg("-benchOut") ?? Path.Combine(Application.persistentDataPath, "bench.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, "");

            using var gcBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            using var gcCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");

            // Which allocation counters work in this player: a known 1 MB allocation must show up.
            yield return null;
            var probe = new byte[1 << 20];
            yield return null;
            GC.KeepAlive(probe);
            _gcMode = gcBytes.LastValue >= 1 << 20 ? "recorder" : "none";
            Append(output, $"{{\"meta\":true,\"unity\":\"{Application.unityVersion}\",\"backend\":\"{Backend()}\",\"development\":{(Debug.isDebugBuild ? "true" : "false")},\"gcMode\":\"{_gcMode}\",\"probeRecorder\":{gcBytes.LastValue},\"quick\":{(quick ? "true" : "false")},\"cpu\":\"{SystemInfo.processorType}\",\"screen\":\"{Screen.width}x{Screen.height}\"}}");

            foreach (var scenario in (Scenario[])Enum.GetValues(typeof(Scenario)))
            foreach (var n in quick ? Counts[scenario].Take(2) : Counts[scenario])
            foreach (var driver in Drivers())
            {
                if (!driver.Supports(scenario)) continue;
                var items = Build(scenario, n);
                yield return null;

                var setupWatch = Stopwatch.StartNew();
                try { driver.Setup(scenario, items); }
                catch (Exception e)
                {
                    Append(output, $"{{\"scenario\":\"{scenario}\",\"approach\":\"{driver.Approach}\",\"n\":{n},\"error\":\"{e.GetType().Name}: {e.Message.Replace("\"", "'")}\"}}");
                    Destroy(items);
                    continue;
                }
                setupWatch.Stop();
                yield return null;
                var setupGc = FrameAlloc(gcBytes);

                var start = Time.time;
                var lastReplay = start;
                while (Time.time - start < _warmup)
                {
                    Tick(driver, scenario, ref lastReplay, null);
                    yield return null;
                }

                var frames = new List<float>(4096);
                var gc = new List<long>(4096);
                var gcN = new List<long>(4096);
                var replayMs = new List<double>();
                var measureStart = Time.time;
                while (Time.time - measureStart < _measure)
                {
                    Tick(driver, scenario, ref lastReplay, replayMs);
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime * 1000f);
                    gc.Add(FrameAlloc(gcBytes));
                    gcN.Add(_gcMode == "recorder" ? gcCount.LastValue : -1);
                }

                driver.Teardown();
                Destroy(items);
                yield return null;
                GC.Collect();
                yield return null;

                Append(output, Json(scenario, driver.Approach, n, frames, gc, gcN, setupWatch.Elapsed.TotalMilliseconds, setupGc, replayMs));
                Debug.Log($"[Bench] {scenario} {driver.Approach} {n}: {frames.Average():0.00} ms");
            }
            Append(output, "{\"done\":true}");
            Application.Quit();
        }

        static string Backend()
        {
#if ENABLE_IL2CPP
            return "IL2CPP";
#else
            return "Mono";
#endif
        }

        static void Tick(IBenchDriver driver, Scenario scenario, ref float lastReplay, List<double> replayMs)
        {
            if (scenario == Scenario.Entry && Time.time - lastReplay >= BenchValues.EntryInterval)
            {
                lastReplay = Time.time;
                var watch = Stopwatch.StartNew();
                driver.Replay();
                replayMs?.Add(watch.Elapsed.TotalMilliseconds);
            }
            driver.Tick(Time.time);
        }

        BenchItems Build(Scenario scenario, int n)
        {
            var items = new BenchItems();
            if (scenario == Scenario.Text)
            {
                items.texts = new TMP_Text[n];
                for (var i = 0; i < n; i++)
                {
                    var rt = Node("Text" + i, root, new Vector2(20 + i % 4 * 310, -24 - i / 4 * 28), new Vector2(300, 26));
                    var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
                    text.font = font;
                    text.fontSize = 20;
                    text.text = BenchValues.TextContent;
                    text.raycastTarget = false;
                    items.texts[i] = text;
                }
                return items;
            }
            items.holders = new RectTransform[n];
            items.bodies = new RectTransform[n];
            items.images = new Image[n];
            for (var i = 0; i < n; i++)
            {
                var holder = Node("Row" + i, root, new Vector2(16 + i % 50 * 25, -16 - i / 50 * 17), new Vector2(20, 14));
                var body = Node("Body", holder, Vector2.zero, new Vector2(20, 14));
                body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
                var image = body.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                (items.holders[i], items.bodies[i], items.images[i]) = (holder, body, image);
            }
            return items;
        }

        static RectTransform Node(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        static void Destroy(BenchItems items)
        {
            if (items.texts != null) foreach (var t in items.texts) Destroy(t.gameObject);
            else foreach (var h in items.holders) Destroy(h.gameObject);
        }

        static void Append(string path, string line) => File.AppendAllText(path, line + "\n");

        static string Json(Scenario s, Approach a, int n, List<float> frames, List<long> gc, List<long> gcN, double setupMs, long setupGc, List<double> replayMs)
        {
            var sorted = frames.OrderBy(f => f).ToList();
            float P(float q) => sorted[Mathf.Clamp(Mathf.RoundToInt(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
            var sb = new StringBuilder();
            sb.Append($"{{\"scenario\":\"{s}\",\"approach\":\"{a}\",\"n\":{n},\"frames\":{frames.Count}");
            sb.Append($",\"avgMs\":{frames.Average():0.000},\"p50Ms\":{P(0.5f):0.000},\"p95Ms\":{P(0.95f):0.000},\"maxMs\":{sorted[^1]:0.000}");
            sb.Append($",\"gcBytesPerFrame\":{gc.Average():0.0},\"gcAllocsPerFrame\":{gcN.Average():0.00},\"gcFramesWithAlloc\":{gc.Count(b => b > 0)}");
            sb.Append($",\"setupMs\":{setupMs:0.000},\"setupGcBytes\":{setupGc}");
            sb.Append($",\"replayMs\":{(replayMs.Count > 0 ? replayMs.Average() : 0):0.000},\"replays\":{replayMs.Count}}}");
            return sb.ToString();
        }
    }
}
