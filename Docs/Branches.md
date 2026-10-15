# 브랜치 전략

## 오래 유지하는 브랜치

| 브랜치 | 내용 | 테스트 |
|---|---|---|
| `main` | 패키지 본체. 개발 프로젝트에 선택 라이브러리(UniTask·R3·UIEffect)를 **설치하지 않음** | 의존성 0으로 컴파일·동작하는지(연동 코드는 컴파일에서 빠져야 함) |
| `integration/unitask` | `main` + UniTask 설치 + UniTask 연동 샘플·테스트 | 연동 코드가 실제로 동작하는지 |
| `integration/r3` | `main` + R3 설치(Unity 패키지 + `Assets/Plugins/R3` NuGet DLL) + R3 연동 샘플·테스트 | 〃 |
| `integration/uieffect` | `main` + UIEffect 설치 + UIEffect 트랙 테스트 | 〃 |
| `feature/benchmarks` | 성능 측정 프로젝트 `Benchmarks/` 작업 | 측정은 CLI로 따로 |

- 머지 후 자동 브랜치 삭제는 꺼져 있습니다. 이 브랜치들은 지우지 않습니다.
- 일반 작업은 `feature/…`·`docs/…`·`chore/…` 브랜치에서 하고 `main`으로 PR합니다.

## 연동 코드가 오가는 길

- 연동 코드(예: `Runtime/UIEffect/`)와 연동 샘플은 **패키지에 들어가야** 사용자가 받을 수 있습니다. 그래서 다음 순서로 다룹니다.
  1. 연동 브랜치에서 라이브러리를 설치한 상태로 개발·테스트합니다.
  2. 같은 변경을 `main`으로 PR합니다. `main`의 CI는 라이브러리 없이 돌아서, 설치돼 있을 때만 컴파일되는 구조(asmdef `versionDefines` / `defineConstraints`)가 맞는지 확인합니다.
- `main`의 변경은 감시 워크플로가 매일 연동 브랜치로 동기화 PR을 엽니다(아래).

## 자동 확인 (`.github/workflows/integration-watch.yml`)

매일 09:00(KST)과 수동 실행(Actions → Integration Watch → Run workflow) 때 라이브러리마다 다음을 합니다.

1. **새 버전 확인**
   - 최신 버전 출처: UniTask·R3는 GitHub 릴리스, UIEffect는 OpenUPM
   - 연동 브랜치에 설치된 버전과 다르면 `bump/<lib>-<버전>` 브랜치에 새 버전을 넣고, 연동 브랜치로 PR을 엽니다.
   - 버전 확인·적용은 [`Tools/integration_bump.py`](../Tools/integration_bump.py)가 합니다.
2. **main 동기화**: `main`이 앞서 있으면 `main`을 합친 `sync/<lib>-<sha>` 브랜치로 PR을 엽니다. 충돌이 나면 이슈를 엽니다.
3. 두 경우 모두 그 브랜치에서 Unity 테스트(`unity-tests.yml`)를 실행합니다.
   - GITHUB_TOKEN으로 연 PR은 테스트를 스스로 시작하지 못해서 `workflow_dispatch`로 실행합니다. 결과는 PR의 검사 표시로 보입니다.

**테스트가 실패하면** 그 PR 브랜치에서 고쳐 연동 브랜치로 머지합니다. 패키지 코드가 바뀌었으면 같은 수정을 `main`으로도 PR합니다.

필요한 저장소 설정: Settings → Actions → General → **Allow GitHub Actions to create and approve pull requests**.

## 손으로 버전 올리기

```
git switch integration/r3
python Tools/integration_bump.py latest r3        # 최신 버전
python Tools/integration_bump.py apply r3 1.3.1   # manifest(+ R3 DLL) 변경
```
