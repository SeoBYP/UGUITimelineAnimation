# CI: Linux self-hosted runner로 Unity 테스트 실행

`main` 대상 PR마다 `.github/workflows/unity-tests.yml`이 **원격 Linux 컴퓨터**에서 Unity CLI로 EditMode 테스트를 실행합니다. `main`의 브랜치 보호 규칙이 `Unity EditMode Tests` 체크 통과를 요구하기 때문에, 테스트가 실패하면 머지할 수 없습니다.

## 왜 self-hosted인가
- Unity Personal 라이선스는 **Unity Hub 로그인으로만** 활성화됩니다. 커맨드라인이나 수동(`.ulf`) 활성화는 Personal에서 지원되지 않습니다 ([Unity 매뉴얼](https://docs.unity3d.com/Manual/LicenseActivationMethods.html)).
- 클라우드 실행기는 매번 새 환경이라 Hub 로그인을 할 수 없습니다.
- 그래서 **Hub로 한 번 로그인해 둔 Linux 컴퓨터**에서 테스트를 실행합니다. Personal 라이선스 파일은 `~/.config/unity3d/Unity/licenses/UnityEntitlementLicense.xml`에 저장됩니다.

## Linux 컴퓨터 준비 (한 번만)
아래 작업은 모두 **실행기를 돌릴 사용자 계정 하나**로 진행합니다. 라이선스는 사용자별로 저장되기 때문입니다.

1. **Unity Hub 설치**: [Unity Hub for Linux](https://docs.unity.com/hub/install-hub-linux)
2. **Hub 로그인 → Personal 라이선스 활성화**
   - Hub는 GUI가 필요합니다. 데스크톱 환경, 원격 데스크톱, `ssh -X` 중 하나로 한 번만 실행하면 됩니다.
   - Preferences → Licenses에서 Personal 라이선스가 보이면 완료입니다.
3. **에디터 설치**: `ProjectSettings/ProjectVersion.txt`와 같은 버전(현재 `6000.6.3f1`)을 설치합니다. 플랫폼 모듈은 필요 없습니다.
   - 기본 설치 위치: `~/Unity/Hub/Editor/6000.6.3f1/Editor/Unity`
   - 다른 위치에 설치했다면 실행기 환경변수 `UNITY_EDITOR_ROOT`에 `.../Hub/Editor` 경로를 지정합니다.
4. **필요한 도구**: `git`, `python3` (테스트 결과 요약에 사용)
5. **실행기 등록**
   - GitHub 레포 → Settings → Actions → Runners → **New self-hosted runner** → Linux를 선택하고, 화면에 나오는 명령을 그대로 실행합니다(다운로드 → `./config.sh ...`).
   - `config.sh`가 레이블을 물으면 **`unity`**를 추가합니다. 워크플로는 `[self-hosted, linux, unity]` 레이블의 실행기만 씁니다.
   - 서비스로 상시 실행:
     ```
     sudo ./svc.sh install <사용자이름>
     sudo ./svc.sh start
     ```
     `<사용자이름>`은 2단계에서 Hub에 로그인한 사용자여야 합니다.
6. **확인**: GitHub Actions 탭 → Unity Tests → **Run workflow**로 수동 실행합니다.

## 동작 방식
- **코드 받기**: 실행기 작업 폴더로 받습니다. `Library/`(Unity 임포트 캐시)는 지우지 않고 다음 실행에 재사용합니다. 첫 실행만 느립니다.
- **테스트 실행**:
  ```
  Unity -batchmode -nographics -projectPath <workspace> -runTests -testPlatform EditMode -testResults results.xml -logFile unity-editor.log
  ```
- **종료 코드**: `0` = 통과, `2` = 실패한 테스트 있음, 그 외 = 실행 오류(로그 마지막 60줄 출력)
- **결과 확인**: 실행 요약에 통과/실패 표와 실패한 테스트 이름이 나옵니다. `editmode-test-results` 아티팩트로 결과 XML과 에디터 로그를 받을 수 있습니다.

## 보안
- 레포가 **공개**이므로, 포크에서 온 PR은 이 컴퓨터에서 **실행하지 않습니다**(워크플로 `if` 조건). 같은 레포의 브랜치에서 만든 PR만 실행됩니다.
- 함께 권장하는 설정: Settings → Actions → General → Fork pull request workflows → "Require approval for all external contributors"
- 실행기 계정에는 이 작업에 필요한 권한만 주세요. 실행기는 레포 코드를 그대로 실행합니다.
