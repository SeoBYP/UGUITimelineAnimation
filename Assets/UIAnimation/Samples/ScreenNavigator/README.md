# UnityScreenNavigator 데모 재현

[Haruma-K/UnityScreenNavigator](https://github.com/Haruma-K/UnityScreenNavigator) v1.8.0의 데모를 **UnityScreenNavigator 없이
UI Animation의 화면 전환 컨테이너로** 다시 만든 샘플입니다. 계획: [`Docs/Reference-Samples-Plan.md`](../../../../Docs/Reference-Samples-Plan.md)

| 폴더 | 내용 | 출처·라이선스 |
|---|---|---|
| `Prefabs/` | 원본 Prefab 8개(Top, Loading, Home, Home 메뉴 버튼, Shop, 아이템 세트 시트, 아이템, 썸네일). 레이아웃·GUID 그대로, 원본 스크립트와 Timeline 오브젝트는 빌더가 떼고 UIAnimScreen·전환 상태로 교체 | 원본 MIT ([`LICENSE-UnityScreenNavigator.md`](LICENSE-UnityScreenNavigator.md)) |
| `Art/` | 그림 43장. 원본 일러스트는 외부 소재라 **같은 크기·같은 GUID(.meta)의 대체 그림**(그라데이션·실루엣·단순 도형)으로 바꿈 | 이 저장소 |
| `Scripts/` | 데모 흐름(R3·UniTask 없이 `Awaitable`) | 이 저장소 |
| `Scenes/ScreenNavigatorDemo.unity` | 원본 DemoEntryPoint처럼 Screen Space - Camera 캔버스 3개(페이지, 모달, Connecting) | 빌더가 생성 |

- 범위: D1(Top → Loading → Home), D2(Shop 탭·구매), D3(Settings·Confirmation·유닛 정보·초상화 뷰어 모달).
- 공유 요소 전환: `prefab_unit_portrait_viewer_modal`의 `Enter`/`Exit` 상태(초상화 위치·크기, 0.3초 OutQuart)가 연출이고, `UsnPortraitViewerModal`은 상대 쪽 키 값(유닛 정보 모달의 초상화 영역)만 재생 전에 채웁니다.
- 빌더: `Tools/UIAnimation Dev/Rebuild ScreenNavigator Sample` (`ScreenNavigatorSampleBuilder`). 원본 Prefab은 `Original~`에 그대로 둡니다.
