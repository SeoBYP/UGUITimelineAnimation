# Unity-AnimationUI 데모 재현

[DhafinFawwaz/Unity-AnimationUI](https://github.com/DhafinFawwaz/Unity-AnimationUI) v1.2.0의 데모(Home·Level1 씬)를
**AnimationUI 없이 UI Animation으로** 다시 만든 샘플입니다. 계획: [`Docs/Reference-Samples-Plan.md`](../../../Docs/Reference-Samples-Plan.md)

| 폴더 | 내용 | 출처·라이선스 |
|---|---|---|
| `Scenes/` | 원본 씬 레이아웃(오브젝트·위치·크기·색) 그대로. AnimationUI·ButtonUI 등 원본 스크립트는 빌더가 떼어 내고 UIAnimation으로 교체 | 원본 MIT ([`LICENSE-AnimationUI.txt`](LICENSE-AnimationUI.txt)) |
| `Art/` | 원본 도형 스프라이트 7장 | 원본 MIT |
| `Font/` | Passion One | SIL OFL 1.1 ([`Font/OFL.txt`](Font/OFL.txt)) |
| `Audio/` | 효과음 4개. 원본 효과음은 출처가 불분명해서 **직접 합성한 소리로 대체** | 이 저장소 |
| `Data/AnimationUI-Demo.json` | 원본 씬 YAML에서 뽑은 연출 수치(단계·시간·값·이징)와 버튼 설정·클릭 연결 | 원본 MIT 데이터에서 추출 |

빌더: `Tools/UIAnimation Dev/Rebuild AnimationUI Sample` (`AnimationUISampleBuilder`).
