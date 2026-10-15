# R3 연동 샘플

R3(`com.cysharp.r3`)가 설치돼 있을 때만 컴파일됩니다. UI Animation의 상태와 애니메이션 이벤트를 Observable로 다룹니다.

| API | 내용 |
|---|---|
| `anim.PlayAsObservable("Open")` | 구독하면 재생, 끝나면 `Unit` 하나 내보내고 완료. 끝나지 못하면(다른 상태가 가져감·다시 재생·비활성) `OperationCanceledException`으로 완료. 구독을 끊으면 그 자리에서 멈춤 |
| `anim.EventsAsObservable()` | 모든 애니메이션 이벤트 |
| `anim.EventAsObservable("Shown")` | 이름이 같은 이벤트만 |

장면 `Scenes/R3Popup.unity` (`R3PopupSample`):
- Open: 0.5초 안의 연타는 한 번으로(`ThrottleFirst`) → `Open` 재생 → 끝나면 로그
- Close: 닫는 중 다시 누르면 앞의 것을 버리고 새로(`Switch`)
- `Open` 안의 `Shown` 이벤트를 시간과 함께 로그로

R3 설치: Unity 패키지(`https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity`)와 NuGet의 R3 본체 DLL(NuGetForUnity 등으로)이 필요합니다.
