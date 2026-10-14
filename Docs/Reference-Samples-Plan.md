# 참고 샘플 재현 계획 — UnityScreenNavigator · Unity-AnimationUI

> 작성: 2026-10-15 · 상태: **A·B 완료(`feature/aui-demo`), C·D1·D2 완료(`feature/usn-demo`), D3부터 남음** · 기준 `main` 487631c (PR #12 머지 시점)
> 설계 문서: [`UIAnimation-Design.md`](UIAnimation-Design.md)

## 1. 목적

배포 전에 아래 네 가지를 샘플과 수치로 증명합니다.

| # | 증명할 것 | 근거로 남길 것 |
|---|---|---|
| 1 | 실무에서도 쓸 수 있는 멋진 연출을 만들 수 있다 | 두 레포 데모를 **그대로** 재현한 샘플 |
| 2 | Animator·LitMotion·DOTween 없이도 성능 이득을 내면서 편하게 쓸 수 있다 | 같은 연출을 각 도구로 만든 성능 비교 (시간·GC 할당) |
| 3 | UI Animation Window로 기존 Animator·Tween보다 더 편하게 만든다 | 같은 연출의 제작 절차 비교 문서 |
| 4 | UIEffect 같은 UI 라이브러리, UniTask·R3와 쉽게 연동된다 | 연동 샘플 |

대상 레포:
- [Haruma-K/UnityScreenNavigator](https://github.com/Haruma-K/UnityScreenNavigator) (이하 USN) v1.8.0, Unity 6000.0
- [DhafinFawwaz/Unity-AnimationUI](https://github.com/DhafinFawwaz/Unity-AnimationUI) (이하 AUI) v1.2.0, Unity 2021.3

## 2. 확정된 결정 (2026-10-15)

| 항목 | 결정 |
|---|---|
| 라이선스가 불확실한 에셋 | **대체 에셋** 사용. 레이아웃·크기·연출은 그대로, 그림은 단색 도형·실루엣, 효과음은 CC0 또는 직접 생성 |
| AUI의 Back(들어온 연출 역재생) | **`PlayReversed` 기능 추가**. 상태 하나로 열기·닫기 |
| 화면 전환 컨테이너(Page·Modal·Sheet) | **패키지 기능**으로 제작 (USN 대체) |
| 성능 비교 코드 | 저장소 안 **별도 `Benchmarks/` Unity 프로젝트**. 패키지와 데모 프로젝트는 서드파티 의존성 0 유지 |

## 3. 라이선스

| 대상 | 라이선스 | 처리 |
|---|---|---|
| USN 코드 | MIT (Copyright 2021 Haruki Yano) | 코드를 옮기면 고지 유지. 우리는 코드를 옮기지 않고 동작만 재현 |
| USN 캐릭터·유닛 일러스트 (20장, 7.8MB) | README가 외부 무료 소재 사이트(JewelSavior)를 출처로 언급. 별도 라이선스 없음 | **사용 안 함**, 대체 |
| USN GUI 이미지 (배경 3장 등 23장) | 명시 없음 | 대체 (배경은 단색·그라데이션) |
| USN 폰트 | TMP 기본 LiberationSans | 그대로 사용 가능 |
| AUI 코드 | MIT (Copyright 2022 Dhafin Fawwaz Ikramullah) | 위와 같음 |
| AUI 폰트 Passion One | SIL OFL 1.1 (Google Fonts) | 사용 가능. OFL 전문 동봉 |
| AUI 효과음 4개 | Button3.wav에 상용 효과음 라이브러리(PMSFX) 메타데이터, 나머지 출처 불명 | **사용 안 함**, 대체 |
| AUI 도형 스프라이트 7장 | 작성자 제작, MIT 범위로 추정 | 단순 도형이라 직접 다시 그려도 됨 |

## 4. 원본 분석 요약

### 4.1 Unity-AnimationUI 데모 (씬 2개, 연출 8개)

AUI는 "단계 목록" 방식입니다: Animation / Wait / SetActive / 입력 잠금 / 효과음 / 씬 전환 / UnityEvent 단계를 차례로 실행하고, 시간은 Wait로만 흐릅니다. 단계마다 시작·끝 값 한 쌍, 길이, 이징 하나(반복·곡선 없음). 런타임은 코루틴, 풀링 없음.

**Home 씬** (카메라 배경 #F26F67, 사각형 파티클 배경, 1920×1080)

| 연출 | 계기 | 길이 | 내용 |
|---|---|---|---|
| Home → Settings | Settings 클릭 (Back = 역재생) | 1.21초 | 메뉴 버튼 4개 0.05초 간격으로 왼쪽 퇴장(0.4초 InOutQuart), 카메라 배경색 → 초록, Settings 캔버스 켜기, 제목 그림자 내려오기, 행 4개가 **앵커**를 바꾸며 들어옴, Back 버튼, Home 끄기 |
| Home → Upgrade | Upgrade 클릭 (Back = 역재생) | 1.81초 | 메뉴 퇴장, 노란 배경이 앵커·크기·위치를 바꾸며 펼쳐짐, 옵션 3개 내려오기, 카드 4장 크기 0→1, Back |
| Home → Start | Start 클릭 | 1.61초 | 카메라 Orthographic 크기 5→0.5(InCubic, 파티클 배경 줌), 메뉴 퇴장, 좌우 줄무늬 4쌍이 채우기로 화면 덮기(0.5~0.8초, 시차), 씬 전환 |
| → Home | 시작 시 | 0.51초 | 흰 화면 페이드 아웃 |

**Level1 씬**

| 연출 | 계기 | 길이 | 내용 |
|---|---|---|---|
| 시작 | 시작 시 | 2.26초 | 흰 화면 페이드, 박스 5개가 크기 0→1 + Z회전 180→0으로 0.05초 간격 등장(0.4초 OutQuart), Prev/Next 등장 |
| Lose / Win | 박스 클릭 | 0.61 / 0.51초 | 팝업 캔버스 켜기, CanvasGroup 알파 0→1 + 배경 크기 1.5→1 |
| → Home | Win/Lose 버튼 | 0.51초 | 흰 화면 페이드 인 후 씬 전환 |

**버튼(ButtonUI)**: 상태가 바뀔 때마다 0.15초 동안 크기(평소 1, hover 1.1, press 1.2)와 색을 **현재 값에서** 바꿉니다. 이징은 AUI 전용 OutBack 다항식입니다. hover·press·click에 효과음이 납니다.

### 4.2 UnityScreenNavigator 데모 (화면 7개)

USN은 화면 전환 프레임워크입니다.
- **Page**: 기록 스택, push/pop. 나가는 화면과 들어오는 화면이 동시에 움직입니다.
- **Modal**: 위에 쌓임. 모달마다 배경을 어둡게 합니다.
- **Sheet**: 기록 없는 탭 전환.
- 전환 전·후에 코드를 실행하는 생명주기 콜백(Will/Did, await 가능)이 있고, 전환 중에는 모든 컨테이너의 입력을 잠급니다.
- 전환 연출은 `Duration` + `SetTime(t)` 계약입니다. 기본 연출과 화면별 Timeline 연출이 있습니다.

**기본 연출** (0.3초, QuarticOut)
- 페이지 push: 오른쪽 → 가운데 / 나가는 쪽 가운데 → 왼쪽. pop은 반대 방향입니다.
- 모달: 크기 0.3→1 + 알파 0→1. 배경은 알파 페이드(Linear)입니다.
- 시트: 알파 페이드(Linear)입니다.

**화면 흐름**: Top → (탭) → Loading(기록에 안 남음) → Home → Shop.
- Home에서 Settings 모달을 엽니다. 잠긴 항목을 누르면 확인 모달이 뜹니다.
- Shop은 탭 3개입니다. 썸네일을 누르면 유닛 정보 모달이 열리고, 거기서 Expand를 누르면 초상화 보기 모달이 열립니다.

**화면별 연출**

| 화면 | 연출 |
|---|---|
| Top 나가기 | 위·아래 바가 화면 밖으로, 제목·안내 글 알파 0.749→0, 배경 알파 1→0 |
| Loading | 알파 페이드 0.3초 (들어올 때 QuadIn, 나갈 때 QuadOut) |
| Home 나가기 | 화면 알파 1→0 (0.25초 늦게 시작), 왼쪽 위 아이콘 위로, 오른쪽 메뉴 **원근 Y회전** 11°→58.9° |
| Shop 탭 시트 | 0.2초 QuarticOut 좌우 슬라이드. 방향은 **두 탭의 순서 비교**로 정해짐 |
| 초상화 보기 모달 | **공유 요소 전환**: 유닛 정보 모달의 초상화 위치·크기에서 자기 위치·크기로 0.3초 이동 |
| 버튼 | ColorTint만 (별도 연출 없음) |

**코드 구조**: 8개 어셈블리(Domain / UseCase / Presentation / View / Composition 등), 수동 의존성 연결, R3(ReactiveProperty·버튼 바인딩), UniTask, Addressables.

## 5. 재현에 필요한데 지금 엔진에 없는 것

| 필요한 것 | 쓰는 곳 | 비고 |
|---|---|---|
| CanvasGroup Alpha 트랙 | 두 데모 (화면·팝업 페이드) | |
| Anchor Min / Max (+ Pivot) 트랙 | AUI Settings 행, Upgrade 배경 | 위치로 흉내 내면 16:9에서만 맞음 |
| 카메라 배경색 · Orthographic 크기 트랙 | AUI 화면 전환 줌 | UI 밖 대상 |
| Active(켜기·끄기) 트랙 | AUI 전체 (캔버스 켜기, 입력 막는 오브젝트) | 값 보간 없이 키에서 바뀜 |
| 역재생 `PlayReversed` | AUI Back | 이벤트·자식 호출도 거꾸로. AUI는 이징을 그대로 쓰지만 시간 역재생은 이징이 뒤집혀 보임 → 차이로 기록 |
| 화면 크기 기준 값 ("자기 폭만큼 오른쪽에서") | USN 페이지·시트 슬라이드 | 키 값이 해상도에 따라 바뀌어야 함 |
| 화면 전환 컨테이너 Page / Modal / Sheet | USN 전체 | 상태 이름 규약(PushEnter / PushExit / PopEnter / PopExit), 모달 배경, 전환 중 입력 잠금, await 가능한 생명주기 |
| 상대 화면 기준 연출 | USN 시트 방향, 초상화 공유 요소 전환 | 실행 중 계산되는 시작·끝 값 (상대 화면의 위치·크기) |
| AUI 전용 OutBack 이징 | AUI 버튼 | `CustomAnimationCurve`로 근사하거나 이징 추가 |
| 씬 전환 | AUI | 애니메이션 이벤트 + 리스너 (엔진 변경 없음) |

> 진행 상황: 앞 5줄(트랙 4종 + 역재생)과 OutBack 이징은 **완료**(A1~A3). 나머지(화면 크기 기준 값, 화면 전환 컨테이너, 상대 화면 기준 연출)는 C·D에서 합니다.

## 6. 작업 단위 (브랜치·PR 하나씩)

작업을 PR 하나 크기로 나눴습니다. 위에서부터 진행하고, "먼저 필요"에 적힌 작업이 끝나야 시작할 수 있습니다. 크기는 대략적인 상대 크기입니다(S < M < L).

### A. 엔진 보강
| ID | 작업 | 먼저 필요 | 크기 | 끝났다고 볼 기준 |
|---|---|---|---|---|
| A1 | 새 트랙 7종: CanvasGroup Alpha, Anchor Min, Anchor Max, Pivot, 카메라 배경색, 카메라 Orthographic 크기, Active(켜기·끄기, 보간 없음) | — | M | 코드 초안은 `stash@{0}`에 있음. 트랙별 단위 테스트, 트랙 추가 창에 나옴, 에디터 미리보기·복원, 기존 테스트 통과 |
| A2 | 역재생 `PlayReversed` / `PlayReversedAsync`, UnityEvent용 메서드, Play On 연결 | — | M | 값·이벤트·사운드·자식 호출이 거꾸로, 시작 값은 마지막 정방향 재생 값 재사용, 완료·취소 규칙 같음 |
| A3 | AUI 전용 OutBack 이징(다항식 4종) | — | S | 원본 공식과 같은 값 테스트 |

### B. Unity-AnimationUI 데모 재현
| ID | 작업 | 먼저 필요 | 크기 | 끝났다고 볼 기준 |
|---|---|---|---|---|
| B1 | 공용 준비: Passion One 폰트(OFL 동봉), 도형 스프라이트, 대체 효과음 4개, 입력 막는 오브젝트, 버튼 연출(hover 1.1 · press 1.2, 현재 값에서, 효과음) | A1, A3 | M | 버튼 상태별 크기·색·소리 테스트 |
| B2 | Home 씬: 메뉴, Home → Settings, Home → Upgrade, 각 Back(역재생) | B1, A2 | L | 분석표의 시간·값과 일치하는지 연출별 PlayMode 테스트 |
| B3 | Home → Start(카메라 줌, 파티클 배경, 줄무늬 채우기, 씬 전환) + Level1 씬(박스 등장, Win/Lose, 홈 복귀) | B2 | L | 연출별 PlayMode 테스트, 두 씬 왕복 |

### C. 화면 전환 컨테이너 (패키지 기능)
| ID | 작업 | 먼저 필요 | 크기 | 끝났다고 볼 기준 |
|---|---|---|---|---|
| C1 | Page 컨테이너: 기록 스택 push/pop, 상태 이름 규약(PushEnter·PushExit·PopEnter·PopExit), 나가기·들어오기 동시 재생, 기록에 안 남는 페이지, 전환 중 입력 잠금, await 가능한 생명주기 | A1 | L | 컨테이너 단위 테스트, USN 기본 연출과 같은 값 |
| C2 | Modal 컨테이너: 쌓기, 모달마다 배경 페이드, 닫을 때 위 모달만 연출 | C1 | M | 단위 테스트 |
| C3 | Sheet 컨테이너 + **화면 크기 기준 값**(자기 폭·높이 배수로 키 지정) | C1 | M | 해상도를 바꿔도 같은 비율로 움직이는지 테스트 |
| C4 | 상대 화면 기준 연출: 상대 화면의 위치·크기를 시작·끝 값으로(공유 요소 전환), 두 탭 순서로 방향 고르기 | C3 | M | 단위 테스트 |

### D. UnityScreenNavigator 데모 재현
| ID | 작업 | 먼저 필요 | 크기 | 끝났다고 볼 기준 |
|---|---|---|---|---|
| D1 | Top → Loading → Home 흐름과 각 연출(바·제목 퇴장, 원근 Y회전 메뉴), 대체 배경·캐릭터 실루엣 | C1, C2 | L | 흐름 PlayMode 테스트 |
| D2 | Shop: 탭 3개 시트 슬라이드, 아이템 8칸, 구매 → Connecting 오버레이 | D1, C3, C4 | M | 탭 전환 방향·구매 흐름 테스트 |
| D3 | 모달 4종: Settings(토글·슬라이더 저장), Confirmation, UnitTypeInformation(시트 3개), PortraitViewer(공유 요소 전환) | D2, C4 | L | 모달 흐름 테스트, 공유 요소 시작·끝 위치 테스트 |

### E. 성능 비교
| ID | 작업 | 먼저 필요 | 크기 | 끝났다고 볼 기준 |
|---|---|---|---|---|
| E1 | `Benchmarks/` Unity 프로젝트, Animator·DOTween·LitMotion·AnimationUI 설치, 공통 측정 도구(프레임 시간, GC 할당 횟수) | — | M | 빈 측정이 돌아감, 측정 도구 자체 검증 |
| E2 | 같은 연출(버튼 hover 다수, 화면 전환, 목록 등장)을 5가지 방식으로 만들고 동시 재생 개수별 측정 | E1, B2 | L | 측정표가 문서에 들어감 |

### F. 연동 샘플
| ID | 작업 | 먼저 필요 | 크기 | 끝났다고 볼 기준 |
|---|---|---|---|---|
| F1 | UniTask 연동 샘플(이미 있는 `PlayUniTaskAsync` 활용: 동시 재생, 시간 제한) | — | S | 샘플 + 테스트 |
| F2 | R3 연동: 상태 완료·애니메이션 이벤트를 Observable로 (설치돼 있을 때만 컴파일) | — | M | 설치 유무 둘 다 컴파일, 테스트 |
| F3 | UIEffect 연동: 셰이더 효과 값을 트랙으로 (설치돼 있을 때만) | A1 | M | 설치 유무 둘 다 컴파일, 샘플 |

### G. 제작 편의 비교
| ID | 작업 | 먼저 필요 | 크기 | 끝났다고 볼 기준 |
|---|---|---|---|---|
| G1 | 같은 연출을 각 도구로 만드는 절차 비교(단계 수, 코드 줄 수, 미리보기 여부, 수정할 때 드는 일) | B2, E2 | M | 비교 문서 |

### 진행 결과

| 묶음 | 상태 | 결과 |
|---|---|---|
| A + B | ✅ `feature/aui-demo` | 트랙 7종(`CanvasGroupAlpha`, `AnchorMin`, `AnchorMax`, `Pivot`, `CameraBackground`, `CameraOrthoSize`, `Active`), `PlayReversed`, OutBack 이징 4종. AnimationUI 데모는 **원본 씬을 그대로 가져와** 원본 스크립트만 떼고 UIAnimation으로 교체(빌더 `AnimationUISampleBuilder`). 연출 수치는 원본 씬 YAML에서 자동 추출(단계 108개, 버튼 23개, 클릭 연결 21개). 효과음 4개는 직접 합성. EditMode 128/128, PlayMode 24/24 |
| C + D (D1·D2) | ✅ `feature/usn-demo` | Page·Modal·Sheet 컨테이너(패키지, `Awaitable`), Size Relative 트랙 옵션, 시트 방향별 상태. ScreenNavigator 데모는 **원본 Prefab 8개를 GUID 그대로** 가져와 원본 스크립트·Timeline 오브젝트를 떼고 UIAnimScreen·상태로 교체(빌더 `ScreenNavigatorSampleBuilder`). 전환 수치는 원본 Timeline(UnityUIPlayables 클립)·SimpleTransition 파일에서 추출(이징 번호 2 = OutQuad, UnityUIPlayables 소스로 확인). 그림 43장은 같은 크기·같은 GUID의 대체 그림. EditMode 129/129, PlayMode 33/33 |
| D3 (모달 4종 + 공유 요소 전환) | ⬜ | Settings·Confirmation·UnitTypeInformation·PortraitViewer, C4의 공유 요소 전환 |

**권장 순서**: A1 → A2 → A3 → B1 → B2 → B3 → C1 → C2 → C3 → C4 → D1 → D2 → D3 → E1 → E2 → F1 → F2 → F3 → G1.
E1·F1·F2는 다른 작업과 겹치지 않아서 언제든 끼워 넣을 수 있습니다.

## 7. 원본과 달라질 수 있는 점 (미리 기록)

- **역재생 이징**: AUI는 거꾸로 돌릴 때도 같은 이징을 쓰고, 시간 역재생은 이징 곡선이 뒤집힙니다. 시작·끝 값과 길이는 같습니다.
- **에셋**: 그림과 소리는 대체합니다. 레이아웃·크기·색·시간은 원본 수치를 따릅니다.
- **USN 미사용 데이터**: 원본의 연결 안 된 Home 트랙과 쓰이지 않는 Shop 등장 Timeline은 재현하지 않습니다. 실제 동작(기본 슬라이드)을 따릅니다.
- **확인함**: USN Timeline 연출의 이징 번호 2는 UnityUIPlayables `EaseType`의 QuadraticEaseOut(= `OutQuad`)입니다.
- **USN 데모의 Settings 버튼**: D3 전까지는 로그만 남깁니다.
