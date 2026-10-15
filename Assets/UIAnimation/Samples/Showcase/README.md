# Showcase

UI Animation으로 만든 게임 UI 화면들을 **테마 3종**으로 보여 주는 샘플입니다. 세 테마는 같은 스크립트를 쓰고, 그림과 **UIAnimation 상태만** 다릅니다. 각 Prefab의 상태는 UI Animation 창에서 열어 고칠 수 있습니다.

| 테마 | 연출 성격 |
|---|---|
| Casual | OutBack·OutBounce·탄성, 눌렀다 놓으면 통통 튐 |
| SciFi | 0.1~0.25초 OutExpo, 깜빡이며 켜짐, 글자·숫자 스크램블 |
| Fantasy | 0.6~1초 InOutSine, 느린 페이드, 글자가 하나씩 떠오름 |

| 화면 | 보여 주는 것 |
|---|---|
| 로비 | 하단 탭 인디케이터(`Tab0`~`Tab2`, 현재 위치에서 출발), 탭 내용은 시트 컨테이너, 토스트(`Show`를 정재생해 보이고 역재생해 숨김) |
| 상점 → 상세 | 공유 요소 전환: 카드 아이콘이 상세 화면 자리로 커지고 닫으면 돌아감(`UIAnimSharedElement`) |
| 보상 | 상자 흔들림(Shake)·튀어오름(Punch), 빛줄기 회전, 아이템 순차 등장 |
| 레벨업 | 띠 펼침, 글자 단위 등장/스크램블, 별, 레벨 숫자 |
| 테마 | 테마 전환: 로비를 기록 없이(`stack: false`) 교체 |

- 장면: `Scenes/Showcase.unity` (1920×1080 기준)
- 그림은 이 샘플용으로 새로 그린 것, 폰트는 Passion One(`Font/OFL.txt`).
