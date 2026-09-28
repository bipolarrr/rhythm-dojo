# Unity Git 협업 초기 설정

GitHub에서 받은 프로젝트를 위한 Windows 설정 안내다. 에이전트는 필요한 도구와 로컬 협업 설정이 없으면 아래 절차를 실행한다. 이미 올바른 설정은 유지하고 전역 Git 설정이나 Unity 설치 파일을 수정하지 않는다.

## 필요한 도구

`Get-Command scoop`으로 확인하고, Scoop이 없으면 일반 사용자 PowerShell에서 [공식 설치 명령](https://scoop.sh/)을 실행한다.

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
Invoke-RestMethod -Uri https://get.scoop.sh | Invoke-Expression
```

`Get-Command git`으로 확인하고, Git이 없으면 공식 main 버킷에서 설치한다.

```powershell
scoop install main/git
```

프로젝트는 GitHub의 기존 저장소를 clone하여 사용한다. 이미 clone되어 있으면 그대로 사용하고 새로 초기화하지 않는다. ZIP으로 받아 `.git`이 없다면 기존 GitHub 이력을 유지하도록 별도 폴더에 clone한다. 기존 폴더의 작업물을 덮어쓰지 않는다.

```powershell
git clone https://github.com/bipolarrr/rhythm-dojo.git
```

## clone 후 로컬 설정

1. `ProjectSettings/ProjectVersion.txt`에 지정한 Unity를 사용한다. Force Text와 Visible Meta Files를 확인한다.
2. 저장소 루트의 PowerShell에서 `./Tools/Initialize-UnityGit.ps1`을 실행한다. 표준 Hub 경로가 아니면 같은 버전의 실행 파일을 `-UnityYamlMergePath`로 지정한다. 기존 다른 Git 훅이 있으면 통합하고 덮어쓰지 않는다.
3. `.gitattributes`가 씬과 프리팹에 `merge=unityyamlmerge`를 지정하는지 `git check-attr merge -- Assets/Game/Scenes/Bootstrap.unity`로 확인한다. 로컬 드라이버와 `core.hooksPath`도 확인한다.
4. `./Tools/Test-UnityGitSetup.ps1`로 임시 저장소에서 병합과 검사 훅을 검증하고, `./Tools/Test-UnityMeta.ps1`로 실제 스테이징 상태를 확인한다.
5. **설정과 검증이 모두 완료되면 `AGENTS.md`에서 이 설정 문서로의 임시 안내와 UnityYAMLMerge 관련 언급을 모두 제거한다. 다른 프로젝트 지침은 유지한다.** 이 문서와 README의 링크, 설정 스크립트와 훅은 보존한다.

## 병합 도구

UnityYAMLMerge는 씬/프리팹의 구조를 이해하는 병합 도구다. Git 드라이버는 base, theirs, ours, output 순서로 호출하며, output은 Git의 ours 임시 파일이다. `--fallback none`으로 다른 GUI 프로그램을 띄우지 않고 해결되지 않은 충돌을 실패로 남긴다. `.meta`는 일반 텍스트 병합한다. 필요하면 `git mergetool --tool=unityyamlmerge -- <scene-or-prefab>`를 사용한다.

단독 `--help` 호출은 올바른 명령이 아니며 오류창이 뜰 수 있으므로 사용하지 않는다. 검증은 유효한 Unity YAML 복사본과 정상적인 `merge` 명령으로 한다. 실제 프로젝트 에셋을 병합 시험용으로 수정하지 않는다.

공식 참고: [Unity Smart Merge](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html), [Git merge attributes](https://git-scm.com/docs/gitattributes).

## 고아 .meta 방지

UnityYAMLMerge 자체가 고아 `.meta`를 막지는 않는다. 에셋과 `.meta`는 생성·이동·이름 변경·삭제 시 함께 스테이징한다. GUID는 참조 식별자이므로 충돌 해결을 위해 임의 재생성하지 않는다.

pre-commit 훅은 작업 폴더가 아니라 Git 인덱스를 검사한다. 에셋/폴더의 `.meta` 누락, 대상이 없는 `.meta`, 잘못되거나 중복된 GUID, 미해결 충돌을 차단한다. 따라서 파일이 작업 폴더에만 존재하는 부분 스테이징도 감지한다. 검사기는 파일을 삭제하거나 GUID를 수정하지 않는다.

Git은 빈 디렉터리를 기록하지 않는다. 유지할 빈 에셋 폴더에는 `.gitkeep`을 넣고 폴더 `.meta`와 함께 커밋한다. Unity가 무시하는 점 파일에는 `.meta`가 필요 없다. 폴더를 없애려면 폴더 `.meta`도 함께 삭제한다.

훅은 clone만으로 자동 활성화되지 않으므로 새 Windows 작업 환경에서 설정 스크립트를 실행한다. PowerShell 7 또는 Windows PowerShell 5.1을 사용한다. 훅 우회나 다른 도구의 직접 푸시까지 보장하는 서버 측 정책은 아니다.

## 추적 범위

Assets와 대응 `.meta`, Packages manifest/lock, ProjectSettings, 소스·문서·설정 도구를 추적한다. Library, Builds, Temp, UserSettings, 생성된 IDE 프로젝트, 개인 IDE 설정과 임시 IDE 복구 도구는 제외한다. Logs는 Markdown 검증 보고서만 추적한다. 인증 토큰과 개인 Git 설정은 저장소에 기록하지 않는다.
