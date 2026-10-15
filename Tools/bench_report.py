"""Turns benchmark results (Benchmarks/Results/*.jsonl, one per player) into Docs/Benchmarks.md tables.
Run: python Tools/bench_report.py"""
import glob, json, os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
RESULTS = os.path.join(ROOT, 'Benchmarks', 'Results')
ORDER = ['Baseline', 'UIAnimation', 'Animator', 'DOTween', 'LitMotion', 'AnimationUI']
NAMES = {'Baseline': '직접 대입(기준)', 'UIAnimation': '**UI Animation**', 'Animator': 'Animator', 'DOTween': 'DOTween Pro',
         'LitMotion': 'LitMotion', 'AnimationUI': 'AnimationUI'}
SCENARIOS = {
    'Pulse': ('맥박 반복', 'N개 버튼이 계속 맥박: 크기 1 → 1.1 → 1, 흰색 → 금색 → 흰색, 1초 주기(InOutSine), 무한 반복'),
    'Entry': ('목록 등장', 'N개 줄이 등장: x +100 → 0(OutCubic), 투명도 0 → 1, 0.4초. 0.5초마다 전부 다시 재생'),
    'Text': ('글자 물결', '24글자 텍스트 N개, 글자마다 y 0 → 20 → 0(1초, InOutSine), 0.04초씩 늦게, 무한 반복'),
}


def load(name):
    path = os.path.join(RESULTS, name)
    if not os.path.exists(path): return None, {}
    meta, rows = None, {}
    for line in open(path, encoding='utf-8'):
        d = json.loads(line)
        if d.get('meta'): meta = d
        elif 'avgMs' in d: rows[(d['scenario'], d['approach'], d['n'])] = d
    return meta, rows


def table(rows, scenario, value, fmt, note_base=True):
    counts = sorted({n for (s, _, n) in rows if s == scenario})
    approaches = [a for a in ORDER if any((scenario, a, n) in rows for n in counts)]
    out = ['| 방식 | ' + ' | '.join(f'N = {n:,}' for n in counts) + ' |', '|---|' + '---:|' * len(counts)]
    for a in approaches:
        cells = []
        for n in counts:
            d = rows.get((scenario, a, n))
            cells.append(fmt(d, rows.get((scenario, 'Baseline', n))) if d else '—')
        out.append(f'| {NAMES[a]} | ' + ' | '.join(cells) + ' |')
    return out


def ms_with_overhead(d, base):
    s = f"{d['avgMs']:.2f}"
    if base and d['approach'] != 'Baseline': s += f" ({d['avgMs'] - base['avgMs']:+.2f})"
    return s


lines = ['# 성능 비교', '',
         '> `python Tools/bench_report.py`가 `Benchmarks/Results/*.jsonl`에서 만듭니다. 측정 프로젝트·방법은 [`Benchmarks/README.md`](../Benchmarks/README.md).', '']
players = [('mono.jsonl', 'Mono 릴리스'), ('il2cpp.jsonl', 'IL2CPP 릴리스')]
meta_any = None
for file, title in players:
    meta, rows = load(file)
    if not rows: continue
    meta_any = meta_any or meta
    lines += [f'## 프레임 시간 — {title}', '',
              '평균 프레임 시간(ms). 괄호는 **직접 대입(기준)과의 차이 = 그 방식 자체의 비용**입니다(같은 화면 변경을 uGUI가 그리는 비용은 양쪽에 같이 들어 있음). 낮을수록 좋습니다.', '']
    for s, (name, desc) in SCENARIOS.items():
        if not any(k[0] == s for k in rows): continue
        lines += [f'### {name}', '', desc, ''] + table(rows, s, 'avgMs', ms_with_overhead) + ['']
        if s == 'Entry':
            lines += ['재생 시작 비용: 0.5초마다 N개를 다시 재생하는 호출 한 번의 평균 시간(ms).', '']
            lines += table(rows, s, 'replayMs', lambda d, b: f"{d['replayMs']:.2f}") + ['']

meta, rows = load('mono-dev.jsonl')
if rows:
    lines += ['## GC 할당 — Mono 개발 빌드', '',
              '할당 카운터(`GC Allocated In Frame`)는 개발 빌드에서만 동작해서 따로 쟀습니다. 프레임당 평균 바이트(괄호: 프레임당 할당 횟수). '
              '직접 대입(기준)에도 있는 값은 엔진·측정 코드 몫이라, **기준보다 많은 만큼이 그 방식의 할당**입니다.', '']
    for s, (name, _) in SCENARIOS.items():
        if not any(k[0] == s for k in rows): continue
        lines += [f'### {name}', ''] + table(rows, s, 'gcBytesPerFrame', lambda d, b: f"{d['gcBytesPerFrame']:,.0f} ({d['gcAllocsPerFrame']:.1f})") + ['']
    lines += ['### 준비(Setup) 할당', '', '처음 만들 때 한 번 드는 할당(KB). UI Animation은 측정 코드가 상태를 코드로 만들어서 크게 나오고, 실제로는 Prefab에서 불러오므로 이보다 적습니다.', '']
    for s, (name, _) in SCENARIOS.items():
        if not any(k[0] == s for k in rows): continue
        lines += [f'**{name}**', ''] + table(rows, s, 'setupGcBytes', lambda d, b: f"{d['setupGcBytes'] / 1024:,.0f}") + ['']

if meta_any:
    lines += ['## 환경', '', f"- Unity {meta_any['unity']}, Windows 64비트 플레이어 1280×720 창 모드, VSync 끔", f"- CPU: {meta_any['cpu'].strip()}",
              '- 경우마다 1초 예열 후 4초 측정. 한 번 실행한 값이라 ±5 % 정도 흔들립니다.', '']
open(os.path.join(ROOT, 'Docs', 'Benchmarks.md'), 'w', encoding='utf-8').write('\n'.join(lines))
print('wrote Docs/Benchmarks.md')
