# Benchmarks

UI Animation과 다른 방식(Animator, DOTween Pro, LitMotion, Unity-AnimationUI)을 **같은 연출, 같은 오브젝트**로 만들어
Windows 플레이어에서 프레임 시간과 GC 할당을 재는 별도 Unity 프로젝트입니다. 결과 표: [`Docs/Benchmarks.md`](../Docs/Benchmarks.md).

## 무엇을 재나

| 장면 | 내용 | N |
|---|---|---|
| 맥박 반복 | 버튼 N개가 계속 맥박: 크기 1 → 1.1 → 1, 흰색 → 금색 → 흰색, 1초 주기, 무한 반복 | 100 · 500 · 1,000 · 2,000 |
| 목록 등장 | 줄 N개가 x +100 → 0, 투명도 0 → 1 (0.4초). 0.5초마다 전부 다시 재생 | 100 · 500 · 1,000 · 2,000 |
| 글자 물결 | 24글자 텍스트 N개, 글자마다 y 0 → 20 → 0, 0.04초씩 늦게 | 10 · 50 · 100 |

- **직접 대입(기준)**: 애니메이션 시스템 없이 스크립트 하나가 매 프레임 같은 값을 넣는 경우. 같은 화면 변경을 uGUI가 그리는 비용은 모든 방식에 똑같이 들어 있으므로, **기준과의 차이가 그 방식 자체의 비용**입니다.
- Animator·AnimationUI는 글자 단위 애니메이션이 없어 글자 물결에서 빠집니다. AnimationUI는 반복이 없어 끝날 때 다시 `Play()`하고, Sine 이징이 없어 InOut Quad를 씁니다.
- 프레임 시간: 릴리스 빌드(Mono, IL2CPP), 1280×720 창, VSync 끔, 경우마다 1초 예열 + 4초 측정.
- GC 할당: 할당 카운터(`GC Allocated In Frame`)가 개발 빌드에서만 동작해서 Mono 개발 빌드로 따로 잽니다. 측정 시작 때 1MB를 할당해 카운터가 맞는지 확인합니다(`Results/*.jsonl` 첫 줄 `gcMode`).

## 실행 (CLI)

1. DOTween Pro(유료, 재배포 불가)는 저장소에 없습니다. Asset Store에서 이 프로젝트에 Import하고 Unity를 닫습니다. 없으면 DOTween만 빠지고 나머지는 그대로 잽니다.
2. 설정과 빌드:
   ```
   Unity -batchmode -projectPath Benchmarks -executeMethod Bench.BenchBuild.Setup -quit
   Unity -batchmode -projectPath Benchmarks -executeMethod Bench.BenchBuild.BuildMono -quit      # BuildIl2cpp, BuildMonoDev
   ```
3. 측정: `Builds/Mono/Bench.exe -benchOut Results/mono.jsonl` (`il2cpp.jsonl`, `mono-dev.jsonl`). `-benchQuick`은 짧게 확인용.
4. 표 만들기: `python Tools/bench_report.py` → `Docs/Benchmarks.md`

## 구성

- `Assets/Bench/` — 측정기(`BenchRunner`), 방식별 구동기(`*Driver.cs`), 빌드·Animator 에셋 생성(`Editor/BenchBuild.cs`)
- `Assets/ThirdParty/AnimationUI/` — [Unity-AnimationUI](https://github.com/DhafinFawwaz/Unity-AnimationUI) 런타임 소스(MIT, `LICENSE`)
- 패키지: UI Animation(`file:../../Assets/UIAnimation`), LitMotion 2.0.2(OpenUPM)
