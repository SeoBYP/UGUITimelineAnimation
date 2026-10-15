# 제작 편의 비교

같은 연출을 도구별로 만드는 절차를 비교합니다. 근거는 이 저장소에서 실제로 만든 것들입니다.
- [`Benchmarks/Assets/Bench/*Driver.cs`](../Benchmarks/Assets/Bench): 같은 연출 3가지(맥박, 목록 등장, 글자 물결)를 방식별로 구현한 코드
- Unity-AnimationUI·UnityScreenNavigator 데모 재현(`Assets/Reference`)
- RPG 샘플(`Assets/UIAnimation/Samples/RPG`)

다른 도구에 대해서는 이번 작업에서 직접 써 본 범위만 적었습니다. 써 보지 않은 기능은 "확인 안 함"으로 표시했습니다.

## 1. 한 연출을 만드는 절차 — 버튼 맥박 (크기 + 색, 반복)

| 도구 | 만드는 곳 | 절차 | 실행 코드 |
|---|---|---|---|
| **UI Animation** | UI Animation 창 | 1. 버튼에 `UIAnimation` 추가 → 2. 상태 "Pulse" 만들기, **Loop** 켜기 → 3. 트랙 고르기 창에서 Scale·Color 선택 → 4. 0 / 0.5 / 1초에 키, 이징 InOutSine → 5. Play On: On Enable | **0줄**(켜질 때 재생). 코드로 부를 때 `anim.Play("Pulse")` 1줄 |
| Animator | Animation 창 + Animator 창 | 1. Animation 창에서 Create(클립·컨트롤러 에셋 2개 생성) → 2. Record로 키 찍기 → 3. 클립의 Loop Time 확인 → 4. 컨트롤러에서 기본 상태 확인 | 0줄(기본 상태). 다른 연출로 바꾸려면 상태·전환 또는 `Play` |
| DOTween Pro | 인스펙터(`DOTweenAnimation`) 또는 코드 | 인스펙터: 효과 하나당 컴포넌트 하나(크기 1개 + 색 1개), Loops -1·Yoyo 설정. 코드: 아래 2줄 | `DOScale(...).SetLoops(-1, Yoyo)`, `DOColor(...)...` **2줄** |
| LitMotion | 코드 | 값마다 Motion 하나 만들고 대상에 바인딩 | `LMotion.Create(...).WithLoops(-1, Yoyo).BindToLocalScale(rt)` 등 **2개 문장(4줄)** |
| AnimationUI | 인스펙터(`AnimationUI` 시퀀스 목록) | 크기·색 단계 + Wait + 되돌리는 단계 + Wait. 반복 기능이 없음 | 반복하려면 `OnAnimationEnded += Play` **1줄** |

## 2. 연출별로 드는 코드 (측정 프로젝트 구동기 기준)

모든 방식을 같은 조건으로 재려고 측정 프로젝트에서는 전부 코드로 만들었습니다. 실제로 쓸 때 연출을 **에디터에서 만들 수 있는지**가 차이입니다.

| 연출 | UI Animation | Animator | DOTween Pro | LitMotion | AnimationUI |
|---|---|---|---|---|---|
| 맥박 반복 | 에디터(창) | 에디터(창) | 인스펙터 또는 코드 2줄 | 코드 2문장 | 인스펙터(시퀀스 4단계) + 반복 코드 1줄 |
| 목록 등장 (현재 위치 → 0) | 에디터, Start = Current | 에디터, 시작 값은 클립에 고정 | 코드 2줄(`From`) | 코드 2문장 | 인스펙터 |
| 글자 물결 (글자 단위) | **에디터**(Char 트랙 + Stagger) | 불가 | 코드: `DOTweenTMPAnimator` + 글자마다 반복문 | 코드: 글자마다 `BindToTMPCharPositionY` 반복문 | 불가 |
| 공유 요소 전환 | 에디터(상태) + 상대 쪽 키 1줄(`UIAnimSharedElement`) | 확인 안 함 | 코드 | 코드 | 불가 |
| UIEffect 값 (디졸브 등) | **에디터**(UIEffect 트랙 14종) | 에디터(직렬화 필드 기록) | 코드(`DOTween.To`) | 코드(`Bind`) | 불가 |

## 3. 기능 비교 (이 저장소에서 확인한 것)

| | UI Animation | Animator | DOTween Pro | LitMotion | AnimationUI |
|---|---|---|---|---|---|
| 에디터에서 키프레임 편집 | ✅ 타임라인 창 | ✅ Animation 창 | 인스펙터 값(키프레임 아님) | ❌ (별도 패키지는 확인 안 함) | 시퀀스 목록(시작·길이) |
| 편집 모드 미리보기 | ✅ 재생·스크럽, 반복 | ✅ | 확인 안 함 | ❌ | 확인 안 함 |
| 현재 값에서 시작 | ✅ Start = Current | ❌ (클립 값) | ✅ `From` 생략 시 | ✅ | ❌ |
| 역재생 | ✅ `PlayReversed` (현재 값 시작도 원래 자리로) | 속도 −1(파라미터) | ✅ `PlayBackwards` | 확인 안 함 | ✅ `PlayReversed` |
| 반복 | ✅ 상태 Loop | ✅ Loop Time | ✅ | ✅ | ❌ (다시 Play) |
| 글자 단위 | ✅ 에디터 | ❌ | 코드 | 코드 | ❌ |
| 이벤트·사운드 트랙 | ✅ | 이벤트 ✅ | 콜백(코드) | 콜백(코드) | ✅ 이벤트·사운드 단계 |
| 화면 전환 컨테이너 | ✅ Page·Modal·Sheet | ❌ | ❌ | ❌ | ❌ |
| await | ✅ `Awaitable`, UniTask 선택 | ❌ | ✅ (`AsyncWaitForCompletion`) | ✅ | ❌ |
| 서드파티 의존성 | 없음 | 내장 | DOTween | Burst·Collections | 없음 |

## 4. 고칠 때 드는 일

| 바꾸고 싶은 것 | UI Animation | 코드 트윈(DOTween·LitMotion) | Animator |
|---|---|---|---|
| 길이·이징 | 창에서 키를 끌거나 이징 선택, 바로 미리보기 | 코드 수정 → 컴파일 → Play로 확인 | Animation 창에서 키 이동(이징은 커브 접선) |
| 연출 추가 | 상태 하나 추가 | 메서드·필드 추가 | 클립·상태·전환 추가 |
| 프리팹마다 조금 다르게 | 상태별 오버라이드(현재 Prefab에서) | 코드 분기 또는 값 노출 | Override Controller |
| 디자이너가 직접 | ✅ 코드 없음 | ❌ (DOTween Pro 인스펙터는 일부 가능) | ✅ |

## 5. 정리

- **UI Animation**: 연출을 코드 없이 창에서 만들고 바로 미리봅니다. 코드 트윈에서만 되던 것(현재 값 시작, 글자 단위, await)도 에디터에서 됩니다.
- **성능**: [`Performance.md`](Performance.md) 참고. IL2CPP 2,000개 맥박에서 자체 비용 +0.36ms로 DOTween Pro와 같고 LitMotion(+0.17ms)에 가깝습니다. Animator(+2.50ms)·AnimationUI(+2.71ms)보다 훨씬 가볍고, 글자 효과는 가장 빠르며(텍스트 100개 0.63ms, 다른 방식 약 2ms), 재생 중 GC 할당이 없습니다.
- **코드 트윈(LitMotion, DOTween)**: 프레임 비용이 가장 낮습니다. 대신 연출이 코드에 있어서 미리보기와 수정에 컴파일이 들어갑니다.
- **Animator**: 에디터 제작은 되지만 UI 연출에서 가장 무겁습니다. 글자 단위와 현재 값 시작이 없습니다.
