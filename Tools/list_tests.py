"""Writes Docs/Tests.md: every test, grouped by suite and file, from the test sources (test names are Korean sentences).
Run: python Tools/list_tests.py"""
import os, re

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
SUITES = [
    ('EditMode (패키지)', 'Assets/UIAnimation/Tests/Editor', 'asmdef `SeoBYP.TimelineAnimation.Tests.Editor` · CI에서 실행'),
    ('PlayMode (패키지)', 'Assets/UIAnimation/Tests/PlayMode', 'asmdef `SeoBYP.TimelineAnimation.Tests.PlayMode` · CI에서 실행'),
    ('PlayMode (참고 데모, 패키지 밖)', 'Assets/Reference/Tests', 'asmdef `Reference.Tests.PlayMode` · 원본 재현(AnimationUI, ScreenNavigator)'),
    ('PlayMode (개발용 캡처)', 'Assets/Dev/Tests', '`UIANIM_CAPTURE=<폴더>`가 있을 때만 실행(없으면 건너뜀) · 프레임을 PNG로 저장'),
]


def parse(path):
    src = open(path, encoding='utf-8').read()
    m = re.search(r'((?:\s*///.*\n)+)\s*(?:\[[^\]]*\]\s*)*public sealed class (\w+)', src)
    summary = ''
    if m:
        summary = ' '.join(l.strip()[3:].strip() for l in m.group(1).strip().splitlines())
        summary = re.sub(r'</?summary>|<see cref="([^"]+)"\s*/>', lambda x: x.group(1) or '', summary).strip()
    cls = m.group(2) if m else os.path.basename(path)[:-3]
    tests = re.findall(r'\[(?:Unity)?Test\][^\n]*\n(?:\s*\[[^\n]*\n)*\s*public (?:IEnumerator|void|async \w+) (\w+)\(', src)
    return cls, summary, tests


lines = ['# 테스트 목록', '',
         '> 이 문서는 `python Tools/list_tests.py`가 테스트 코드에서 만듭니다(직접 고치지 말 것). 테스트 이름은 확인하는 내용을 문장으로 쓴 것입니다.', '',
         '실행: Unity를 batchmode로 `-runTests -testPlatform EditMode` / `PlayMode` (에디터가 프로젝트를 잠그므로 복사본에서). CI는 둘 다 실행합니다.', '']
body, totals = [], []
for title, folder, note in SUITES:
    files = sorted(f for f in os.listdir(os.path.join(ROOT, folder)) if f.endswith('.cs'))
    parsed = [(f, *parse(os.path.join(ROOT, folder, f))) for f in files]
    count = sum(len(t) for *_, t in parsed)
    totals.append((title, count, folder))
    body += [f'## {title} — {count}개', '', note, '']
    for f, cls, summary, tests in parsed:
        if not tests: continue
        body += [f'### `{cls}` ({len(tests)}개) — `{folder}/{f}`', '']
        if summary: body += [summary, '']
        body += [f'{i}. {t.replace("_", " ")}' for i, t in enumerate(tests, 1)] + ['']
lines += ['| 묶음 | 개수 | 폴더 |', '|---|---|---|'] + [f'| {t} | {c} | `{f}` |' for t, c, f in totals] + ['']
open(os.path.join(ROOT, 'Docs', 'Tests.md'), 'w', encoding='utf-8').write('\n'.join(lines + body))
print({t: c for t, c, _ in totals})
