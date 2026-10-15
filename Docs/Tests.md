# 테스트 목록

> 이 문서는 `python Tools/list_tests.py`가 테스트 코드에서 만듭니다(직접 고치지 말 것). 테스트 이름은 확인하는 내용을 문장으로 쓴 것입니다.

실행: Unity를 batchmode로 `-runTests -testPlatform EditMode` / `PlayMode` (에디터가 프로젝트를 잠그므로 복사본에서). CI는 둘 다 실행합니다.

| 묶음 | 개수 | 폴더 |
|---|---|---|
| EditMode (패키지) | 139 | `Assets/UIAnimation/Tests/Editor` |
| PlayMode (패키지) | 51 | `Assets/UIAnimation/Tests/PlayMode` |
| PlayMode (참고 데모, 패키지 밖) | 9 | `Assets/Reference/Tests` |
| PlayMode (개발용 캡처) | 4 | `Assets/Dev/Tests` |

## EditMode (패키지) — 139개

asmdef `SeoBYP.TimelineAnimation.Tests.Editor` · CI에서 실행

### `ComponentScriptTests` (1개) — `Assets/UIAnimation/Tests/Editor/ComponentScriptTests.cs`

Unity saves a component in a scene or prefab only if its class has a script file of the same name. A component class in another file still works when added at runtime, but is saved as "missing script".

1. 패키지의 모든 컴포넌트는 같은 이름의 스크립트 파일에 있다

### `EditorLayoutMeasureTests` (3개) — `Assets/UIAnimation/Tests/Editor/EditorLayoutMeasureTests.cs`

Measures the laid-out UI Animation Editor (real window, a few editor frames) instead of looking at a screenshot: row heights, key positions, opacity, label colors, button states. Values are written to the test output. Needs batchmode with a graphics device (no -nographics).

1. Popup1 Open의 자식 트랙을 화면 값으로 잰다
2. 바깥 Prefab이 같은 트랙을 오버라이드하면 안쪽 팝업 에디터에서 어떻게 보이는지 잰다
3. 전체 되돌리기는 가장 바깥 단계만 되돌린다

### `EditorWorkflowTests` (4개) — `Assets/UIAnimation/Tests/Editor/EditorWorkflowTests.cs`

Builds the Button2 ripple from scratch the way a user does in the UI Animation Editor: add the component, a state, tracks from the picker, then edit names, Start, key values and Play On through the inspector fields. Then checks that it plays right and that the editor draws every track/key/inspector in the same form.

1. 에디터로 처음부터 만든 Press는 누른 자리에서 퍼진다
2. Pointer 트랙에 키를 더해도 누른 자리를 기준으로 움직인다
3. 타임라인의 모든 트랙과 키는 같은 모양이다
4. 키 인스펙터는 어느 키든 같은 구성이다

### `PerformanceTests` (4개) — `Assets/UIAnimation/Tests/Editor/PerformanceTests.cs`

R3 measurements on the sample prefabs (EditMode, this PC): time and managed allocations of starting states and of one frame of evaluation, for many objects at once. Results go to the test log; the GC-free paths are asserted.

1. 측정 도구 검증 알려진 할당을 잡는다
2. 버튼 100개 동시 Hover
3. 텍스트 10개 동시 재생 상태별
4. 무거운 스크롤 형태

### `PrefabOverrideTests` (9개) — `Assets/UIAnimation/Tests/Editor/PrefabOverrideTests.cs`

Button1.prefab reused inside a new popup prefab. Overriding Hover tracks in the popup (click a dimmed track in the UI Animation Editor) changes only that popup, per track: the other tracks keep following Button1.prefab, which itself never changes. × reverts one track, ↺ all of them. Uses real prefab assets in a temporary folder (a copy of the sample button).

1. 팝업에서 오버라이드하면 팝업의 Button1만 바뀌고 원본 Prefab은 그대로다
2. 팝업의 Open이 재생하는 자식 Hover도 오버라이드다
3. 원본에서 오버라이드하지 않은 트랙을 바꾸면 팝업도 따라간다
4. 원본에서 트랙을 지우면 오버라이드 안 한 트랙만 사라진다
5. 원본에 추가한 트랙은 팝업에도 생긴다
6. 트랙 하나만 되돌리면 그 트랙만 원본으로 돌아간다
7. 전체 되돌리기하면 팝업도 원본으로 돌아오고 원본 Prefab은 계속 그대로다
8. 씬의 팝업 인스턴스에서 오버라이드해도 Unity Revert로 되돌아온다
9. 타임라인에서 오버라이드한 트랙만 파란 이름으로 편집되고 나머지는 흐리다

### `SampleButton2Tests` (8개) — `Assets/UIAnimation/Tests/Editor/SampleButton2Tests.cs`

The Button2 sample prefab as built by Tools/UIAnimation Dev/Rebuild Sample Buttons: a ripple from the press point. Skipped where the sample isn't in the project (the package on its own).

1. UI 이벤트 바인딩은 상태마다 하나다
2. 모든 트랙의 첫 키는 0초다
3. Hover는 배경을 켜고 Unhover는 끈다
4. Press는 누른 자리에서 원이 퍼진다
5. 다시 누르면 새로 누른 자리에서 퍼진다
6. 코드로 Press하면 포인터가 없어 자리를 옮기지 않는다
7. Release는 원을 흐리게 지우고 자리는 그대로 둔다
8. Release 중에 누르면 Release를 끝내고 Press를 시작한다

### `ScrollEditorWorkflowTests` (2개) — `Assets/UIAnimation/Tests/Editor/ScrollEditorWorkflowTests.cs`

Scroll animations made from nothing the way a user does: the list's (or indicator's) state row → Create opens the UI Animation Editor on a new state, tracks come from the picker and keys are typed into the inspector. Then the list plays them.

1. 휠 피커 줄을 에디터로 처음부터 만들면 가운데만 똑바로 선다
2. 페이지 점의 On Off를 에디터로 만들면 현재 페이지 점이 커진다

### `UIAnimBatchTests` (3개) — `Assets/UIAnimation/Tests/Editor/UIAnimBatchTests.cs`

Burst batch (UIAnimBatch): the values it computes for a frame are the ones UIAnimTrack.Sample gives, and Step writes them.

1. 배치 값은 관리 코드 샘플과 같다
2. 역재생과 반복도 같은 시간을 계산한다
3. Step은 배치 값을 쓰고 다시 재생하면 자리를 재사용한다

### `UIAnimEaseTests` (3개) — `Assets/UIAnimation/Tests/Editor/UIAnimEaseTests.cs`

The easing formulas taken over from LitMotion: ends, known values and the saved int of each member.

1. 모든 이징은 0에서 시작해 1에서 끝난다
2. 알려진 값과 같다
3. 저장되는 번호는 LitMotion과 같다

### `UIAnimEngineAdditionsTests` (8개) — `Assets/UIAnimation/Tests/Editor/UIAnimEngineAdditionsTests.cs`

Engine additions for the reference samples: the new tracks (CanvasGroup alpha, anchors, pivot, camera, active), reversed playback and Unity-AnimationUI's OutBack eases.

1. 새 트랙은 값을 읽고 쓴다
2. Active 트랙은 키 시간에 바로 바뀐다
3. Anchor 트랙은 해상도와 상관없이 부모 비율로 움직인다
4. 역재생은 끝에서 시작으로 가고 이벤트도 거꾸로 한 번씩
5. 역재생은 현재값에서 시작한 트랙을 정방향이 시작한 자리로 되돌린다
6. 역재생 중 Complete는 시작 모습으로 끝낸다
7. 역재생하면 자식 호출도 역재생된다
8. AnimationUI OutBack 이징은 원본 공식과 같다

### `UIAnimLoopTests` (4개) — `Assets/UIAnimation/Tests/Editor/UIAnimLoopTests.cs`

State Loop: a looping state runs again from its start at its end, without a gap, until stopped.

1. 반복 상태는 끝에서 처음으로 이어진다
2. 바퀴마다 이벤트가 다시 울린다
3. 역재생 반복은 처음에서 끝으로 이어진다
4. 반복 상태는 Stop할 때까지 재생 중이다

### `UIAnimScrollFormsTests` (15개) — `Assets/UIAnimation/Tests/Editor/UIAnimScrollFormsTests.cs`

The scroll forms beyond a plain list: sizes, prefab types, masonry, loop, pages, accordion, chat, sticky headers, nesting, helpers.

1. 아이템마다 크기가 달라도 이어서 배치된다
2. 인덱스마다 다른 Prefab을 쓰고 종류별로 재사용한다
3. 측정 모드는 레이아웃의 선호 크기를 쓴다
4. 메이슨리는 가장 짧은 열에 쌓는다
5. 루프는 끝에서 처음 아이템으로 이어진다
6. 한 장씩 넘기기는 드래그 한 번에 한 장만 이동한다
7. 아이템 크기를 바꾸면 뒤 아이템이 밀린다
8. 끝에 붙기는 추가돼도 끝을 유지하고 적으면 아래부터 쌓는다
9. 섹션 헤더는 위에 고정되고 다음 헤더가 밀어올린다
10. 가로 드래그는 세로 리스트를 감싼 스크롤로 넘어간다
11. 미리보기는 저장되지 않는 아이템을 만들고 지우면 남지 않는다
12. Rotation은 음수 각도로 읽고 쓴다
13. 페이지 표시는 아이템마다 점을 만들고 현재 점에 On을 재생한다
14. 스크롤 스크럽은 스크롤한 거리로 상태를 보여준다
15. 당겨서 새로고침은 거리를 넘겨 놓으면 이벤트를 낸다

### `UIAnimScrollListTests` (17개) — `Assets/UIAnimation/Tests/Editor/UIAnimScrollListTests.cs`

1. 세로 리스트는 패딩과 간격대로 배치된다
2. 가운데 정렬과 자식 크기 제어
3. 반대 배치는 마지막 아이템이 맨 위다
4. 그리드 Flexible은 뷰포트에 들어가는 만큼 열을 만든다
5. 그리드 행 고정은 가로로 스크롤한다
6. 보이는 아이템만 만들고 스크롤하면 오브젝트를 재사용한다
7. 바인딩과 해제는 정확한 인덱스로 불린다
8. 데이터 목록으로 바인딩한다
9. 개수를 줄이면 스크롤이 범위 안으로 들어온다
10. ScrollTo는 시작 가운데 끝에 맞추고 끝에서는 멈춘다
11. 화면 좌표로 아이템을 찾는다
12. 진입은 보이는 아이템이 간격을 두고 차례로 재생된다
13. 진입이 끝난 뒤 스크롤로 들어온 아이템은 끝 모습이고 등장 상태를 재생한다
14. 스크럽 상태는 뷰포트 안 위치로 정해진다
15. Sample은 재생하지 않고 그 시간의 모습만 보여 준다
16. 성능 바인딩만 스크롤 1000번
17. 성능 스크럽 애니메이션 스크롤 1000번

### `UIAnimationTests` (43개) — `Assets/UIAnimation/Tests/Editor/UIAnimationTests.cs`

1. 키 3개면 가운데 키 값을 지난다
2. 키 하나면 현재 값에서 출발한다
3. 포인터에서 시작하면 누른 위치로 옮겨진다
4. 현재값에서 시작할 때 첫 키가 0초 뒤면 지연이 된다
5. 상대값은 움직인 뒤에도 원래 값 기준이다
6. 키의 Ease가 그 키로 도착하는 모양을 정한다
7. Cancel 새 상태는 자기 속성만 가져간다
8. Cancel 속성을 모두 뺏긴 상태는 멈춘다
9. Complete 중단된 상태를 끝 값으로 맞춘다
10. 끝까지 재생되면 대기가 완료된다
11. 다른 상태가 속성을 모두 가져가면 대기가 취소된다
12. 같은 상태를 다시 재생하면 이전 대기는 취소된다
13. Complete로 건너뛰면 끝 값이 되고 대기가 완료된다
14. Complete 인터럽트로 끝난 상태의 대기는 완료된다
15. 토큰이 취소되면 재생이 멈추고 대기가 취소된다
16. 이미 취소된 토큰이면 재생하지 않는다
17. FillAmount 트랙은 Image 채우기 비율을 움직인다
18. 애니메이션 이벤트는 그 시간을 지날 때 한 번씩 발생한다
19. 영초 이벤트는 재생을 시작하자마자 발생한다
20. Complete로 건너뛰면 남은 이벤트가 순서대로 발생한다
21. 취소되면 남은 이벤트는 발생하지 않는다
22. WaitEventAsync는 그 이벤트가 오면 완료된다
23. 부모가 지정한 시간에 자식 상태를 재생한다
24. 부모 길이는 자식 상태가 끝나는 시간까지 포함한다
25. 부모를 Complete하면 자식도 끝 값이 된다
26. 미리보기는 자식 상태를 호출 후 지난 시간으로 그린다
27. 부모와 자식이 서로 호출해도 무한히 반복하지 않는다
28. 커스텀 struct 데이터를 AddListener로 받는다
29. 다른 struct를 기다리는 리스너는 호출되지 않는다
30. 리스너를 Dispose하면 더 이상 받지 않는다
31. 이벤트 정보로 struct 데이터를 꺼낼 수 있다
32. 인스펙터 리스너는 이름이 같은 이벤트에 UnityEvent를 호출한다
33. 상태 길이는 사운드가 끝나는 시간까지 포함한다
34. 사운드는 AudioSource 없이 공용 2D 소스로 재생한다
35. Complete로 건너뛴 사운드는 재생하지 않는다
36. 바깥 Prefab의 오버라이드가 자식 상태를 대신한다
37. 오버라이드한 Prefab 밖의 같은 자식은 원래 상태를 재생한다
38. 가장 바깥 Prefab의 오버라이드가 우선한다
39. 자식 호출 길이는 오버라이드한 상태의 길이를 쓴다
40. 오버라이드하지 않은 트랙은 자식 원본을 따라간다
41. 예전 형식의 전체 상태 오버라이드는 트랙 오버라이드로 옮겨진다
42. 상태 복사본은 참조는 같고 값은 독립적이다
43. 예전 형식의 이벤트와 사운드는 불러올 때 트랙으로 옮겨진다

### `UIAnimationTextTests` (13개) — `Assets/UIAnimation/Tests/Editor/UIAnimationTextTests.cs`

1. 공개 비율만큼 글자가 보인다
2. 글자 효과 중에 상자가 넓어지면 줄바꿈을 다시 한다
3. 글자 간격만큼 마지막 글자가 늦어져 길이가 늘어난다
4. 글자 크기는 각 글자 중심 기준으로 줄었다가 돌아온다
5. Punch 키는 흔들린 뒤 이전 값으로 돌아온다
6. 스크램블은 글자 수를 유지하고 끝나면 원문이 된다
7. 스크램블은 공백을 유지해서 줄바꿈이 바뀌지 않는다
8. 텍스트를 바꾸고 다시 재생하면 새 글자수로 공개된다
9. 텍스트를 바꾸고 재생하면 첫 프레임부터 새 텍스트가 숨겨져 있다
10. 텍스트를 바꾸면 글자 간격으로 늘어나는 길이도 새 글자수로 계산된다
11. 스크램블 텍스트를 바꾸고 다시 재생하면 새 텍스트로 공개된다
12. 스크램블 재생 중에 텍스트를 바꾸면 새 텍스트를 덮어쓰지 않는다
13. CharacterSpacing 트랙은 자간을 움직이고 원래 값으로 돌아온다

### `UIAnimUIEffectTests` (2개) — `Assets/UIAnimation/Tests/Editor/UIEffect/UIAnimUIEffectTests.cs`

UIEffect values as keyframe tracks (compiled only when UIEffect is installed).

1. UIEffect 값을 키프레임 트랙으로 움직인다
2. UIEffect가 있는 오브젝트는 트랙 고르기에 14가지가 나온다

## PlayMode (패키지) — 51개

asmdef `SeoBYP.TimelineAnimation.Tests.PlayMode` · CI에서 실행

### `PlayAsyncPlayModeTests` (7개) — `Assets/UIAnimation/Tests/PlayMode/PlayAsyncPlayModeTests.cs`

PlayAsync on the real player loop: the animation timer drives the state and the await ends when it finishes or is canceled (design doc §4.3 — every stop goes through "finished" or "canceled", so no await is left hanging).

1. 끝까지 가면 길이만큼 기다린 뒤 끝 값으로 끝난다
2. Complete를 부르면 바로 끝 값으로 완료된다
3. 같은 상태를 다시 재생하면 앞의 대기는 취소되고 뒤의 것이 끝난다
4. 다른 상태가 속성을 가져가면 취소 Complete 인터럽트면 완료
5. 비활성화와 토큰 취소는 대기를 취소한다
6. 이벤트를 기다릴 수 있다
7. UniTask 연동 PlayUniTaskAsync는 WhenAll로 묶을 수 있다

### `R3PlayModeTests` (4개) — `Assets/UIAnimation/Tests/PlayMode/R3/R3PlayModeTests.cs`

R3 integration (compiled only when R3 is installed): states and animation events as Observables.

1. PlayAsObservable은 끝나면 한 번 내보내고 완료된다
2. 구독을 끊으면 그 자리에서 멈춘다
3. 다른 상태가 가져가면 오류로 완료된다
4. 이벤트를 Observable로 받는다

### `R3SamplePlayModeTests` (1개) — `Assets/UIAnimation/Tests/PlayMode/R3/R3SamplePlayModeTests.cs`

The R3 sample scene (Samples/R3): button streams → states, animation events → log.

1. Open 연타는 한 번만 재생되고 이벤트와 끝이 로그에 남는다

### `RpgPhase2PlayModeTests` (6개) — `Assets/UIAnimation/Tests/PlayMode/RpgPhase2PlayModeTests.cs`

The RPG sample's second set of screens (title, loading, upgrade, shop, formation, mail, attendance) in Play mode.

1. 타이틀은 탭하면 로딩을 거쳐 로비로 가고 기록에 남지 않는다
2. 강화는 골드를 쓰고 레벨이 오르며 장비가 차례로 들어간다
3. 상점은 구매 팝업을 거쳐 재화가 바뀌고 다 사면 품절이 찍힌다
4. 편성은 영웅을 넣고 빼면 전투력이 바뀐다
5. 우편은 모두 받기로 한 통씩 접히고 빈 안내가 뜬다
6. 출석은 오늘 칸에 도장을 찍고 보상을 준다

### `RpgPhase3PlayModeTests` (6개) — `Assets/UIAnimation/Tests/PlayMode/RpgPhase3PlayModeTests.cs`

The RPG sample's third set (settings, confirm, toast, quantity wheel, battle HUD, guild chat, ranking, story) in Play mode.

1. 설정 스위치는 손잡이가 옮겨가고 초기화는 확인 후 토스트로 알린다
2. 수량 휠을 돌리면 개수가 바뀌고 사용하면 토스트와 보유량이 줄어든다
3. 전투는 자동 공격과 스킬로 적을 쓰러뜨리고 승리 팝업이 뜬다
4. 길드 채팅은 빠른 답장을 보내면 내 말풍선과 답장이 붙는다
5. 랭킹은 단상이 솟고 내 순위로 스크롤하면 그 줄이 빛난다
6. 스토리는 글자가 타자처럼 나오고 탭하면 한번에 끝나며 마지막에 선택지가 뜬다

### `RpgPlayModeTests` (6개) — `Assets/UIAnimation/Tests/PlayMode/RpgPlayModeTests.cs`

The RPG sample (Assets/UIAnimation/Samples/RPG) in Play mode: each screen opens, moves as built, and closes.

1. 로비의 메뉴마다 화면이 열리고 뒤로 돌아온다
2. 영웅 초상이 카드에서 상세 자리로 이어지고 뒤로 가면 카드로 돌아간다
3. 가방은 탭으로 페이지가 넘어가고 아이템 아이콘이 정보 팝업으로 이어진다
4. 소환 10회는 카드가 떨어지고 모두 앞면으로 뒤집힌다
5. 모험은 챕터를 고르고 편성과 전투를 거쳐 승리와 패배 결과가 뜬다
6. 퀘스트 보상을 받으면 도장이 찍히고 그 줄만 접혀 사라진다

### `ScreenContainerPlayModeTests` (7개) — `Assets/UIAnimation/Tests/PlayMode/ScreenContainerPlayModeTests.cs`

Page, modal and sheet containers on the real player loop: order of screens, transitions, lifecycle, input lock.

1. 페이지는 밀어 넣고 되돌아가며 화면 폭만큼 미끄러진다
2. stack false 페이지는 다음 페이지가 들어오면 기록에서 빠진다
3. renderingOrder가 높은 페이지가 위에 그려진다
4. 생명주기 Will은 기다린 뒤 애니메이션이 시작된다
5. 화면에 같은 이름의 상태가 있으면 기본 대신 그것을 쓴다
6. 모달은 배경과 함께 열리고 닫힌다
7. 시트는 높은 번호로 가면 Enter 낮은 번호로 가면 EnterBackward

### `ScrollPlayModeTests` (12개) — `Assets/UIAnimation/Tests/PlayMode/ScrollPlayModeTests.cs`

The Scroll sample in real Play mode: a pager of 18 pages, one scroll form each (Start, OnEnable, Canvas, LitMotion on the player loop). Sample scripts live in Assembly-CSharp, so they are reached by name.

1. 페이지 18장을 다음 버튼과 가로 스와이프로 넘긴다
2. 리스트와 그리드와 메이슨리
3. 캐러셀 루프 페이징 자동 배너
4. 휠 피커는 가운데 줄만 똑바로 서고 선택을 알려준다
5. 섹션 헤더 가변 크기 채팅
6. 아코디언 중첩 탭
7. 접히는 헤더와 당겨서 새로고침
8. 도구 막대와 제목과 페이지가 겹치지 않는다
9. 캐러셀을 끝 너머까지 당겼다 놓으면 튀지 않고 돌아온다
10. 채팅에서 Send를 연달아 눌러도 있던 말풍선은 깜빡이지 않는다
11. 아이템 수 슬라이더와 Add Remove
12. Edit Animation 버튼은 Play 모드에서도 에디터를 연다

### `ScrollRemoveItemPlayModeTests` (1개) — `Assets/UIAnimation/Tests/PlayMode/ScrollRemoveItemPlayModeTests.cs`

Removing one item from the middle of a list (a claimed quest): fold it with ResizeItem (awaitable), then RemoveItem. The rows below keep their objects and only move up; rows that come into view meanwhile get no Appear.

1. 가운데 줄을 접고 지우면 아래 줄은 그대로 올라오고 등장 연출이 없다

### `UniTaskSamplePlayModeTests` (1개) — `Assets/UIAnimation/Tests/PlayMode/UniTaskSamplePlayModeTests.cs`

The UniTask sample scene (Samples/UniTask), compiled only when UniTask is installed.

1. WhenAll Timeout 이벤트 대기가 로그대로 동작한다

## PlayMode (참고 데모, 패키지 밖) — 9개

asmdef `Reference.Tests.PlayMode` · 원본 재현(AnimationUI, ScreenNavigator)

### `AnimationUISamplePlayModeTests` (5개) — `Assets/Reference/Tests/AnimationUISamplePlayModeTests.cs`

The Unity-AnimationUI demo rebuilt with UI Animation (Assets/Reference/AnimationUI), in real Play mode. Expected values are the original's (Data/AnimationUI-Demo.json, read from the original scenes).

1. Home 시작 페이드 후 Settings로 갔다가 Back으로 되돌아온다
2. Upgrade로 갔다가 Back으로 되돌아온다
3. Start는 카메라를 줌하고 줄무늬로 덮은 뒤 Level1로 간다
4. Level1 박스 등장 후 Win과 Lose 그리고 Home으로
5. 버튼은 hover에 커지고 색이 나타나며 나가면 돌아온다

### `ScreenNavigatorSamplePlayModeTests` (4개) — `Assets/Reference/Tests/ScreenNavigatorSamplePlayModeTests.cs`

The UnityScreenNavigator demo rebuilt with UI Animation (Assets/Reference/ScreenNavigator), in Play mode: Top → Loading → Home → Shop (tabs, buy) → back to Home → back to Top, with the original transition values; the modals (Settings, Confirmation, unit information, portrait viewer with its shared element transition).

1. Top Loading Home Shop을 오가며 원본 전환 수치대로 움직인다
2. 잠긴 메뉴는 LOCKED 표시
3. 설정 모달은 커지며 열리고 바꾼 값을 닫을 때 저장하며 잠긴 항목은 확인 모달을 쌓는다
4. 유닛 정보의 초상화가 뷰어의 위치와 크기로 이어지고 닫으면 돌아간다

## PlayMode (개발용 캡처) — 4개

`UIANIM_CAPTURE=<폴더>`가 있을 때만 실행(없으면 건너뜀) · 프레임을 PNG로 저장

### `RpgCapture` (4개) — `Assets/Dev/Tests/RpgCapture.cs`

Dev-only: plays the RPG sample and saves frames as PNG, to look at the motion without opening the editor. Runs only when UIANIM_CAPTURE is set to an output folder (skipped otherwise, e.g. on CI).

1. CaptureDense
2. Capture
3. CapturePhase2
4. CapturePhase3
