# UniTask 연동 샘플

UniTask(`com.cysharp.unitask`)가 설치돼 있을 때만 컴파일됩니다. 상태 재생을 UniTask로 기다리고 조합합니다.

| API | 내용 |
|---|---|
| `anim.PlayUniTaskAsync("Open", ct)` | `PlayAsync`의 UniTask판. 끝나면 완료, 끝나지 못하면 취소 |
| `anim.PlayReversedUniTaskAsync("Open")` | 역재생 |
| `anim.WaitEventUniTaskAsync("Shown", ct)` | 다음 애니메이션 이벤트까지 대기 |

장면 `Scenes/UniTaskPopup.unity` (`UniTaskPopupSample`):
- **Open**: 팝업과 배경을 함께(`UniTask.WhenAll`)
- **Close**: 닫는 중 다른 재생이 가져가도 오류 없이(`SuppressCancellationThrow`)
- **Slow**: 3초짜리 상태에 1초 제한(`Timeout`) → 넘기면 끝으로 건너뜀(`Complete`)
- **Wait Shown**: 다음 Open의 `Shown` 이벤트를 기다림
- 모두 오브젝트가 사라지면 취소(`destroyCancellationToken`)
