# 동작별 검증 가이드

테스트는 **준비 → 입력/동작 → 관찰 가능한 결과**로 작성합니다. 실패한 테스트 이름만 읽어도 어떤 약속이 깨졌는지 알 수 있어야 합니다. 하나의 긴 플레이 순서에 여러 기능의 성공 여부를 묶지 않습니다.

## 실행

Unity **6000.3.23f1**의 **Game Tools > Verification** 메뉴를 사용합니다.

| 메뉴 | 범위 | 빌드 |
| --- | --- | --- |
| Verify UI Actions | 타이틀, 곡 선택, 설정, 게임 HUD의 버튼/선택/화면 전환 | 없음 |
| Verify Gameplay Actions | 입력, 판정, 재시작, 포커스, 모드 어댑터, 실제 오디오 | 없음 |
| Verify Authoring Actions | 문서 플레이테스트, 취소, 데이터 캡처, 음원 해제 | 없음 |
| Verify Foundation (Play Mode and Player Build) | Domain/EditMode 전체 → PlayMode 전체 → Windows 빌드 | 테스트 전체 성공 시 |
| Build Player Only | 현재 씬으로 Windows 빌드 | 테스트 생략 |

**Window > General > Test Runner**에서 개별 fixture나 테스트 하나만 선택해도 같은 검증입니다. 메뉴 전용 판정 로직은 없습니다. PlayMode의 `UI`, `Gameplay`, `Authoring` 카테고리로 범위를 선택할 수 있습니다.

검증 시작 시 기존 Play Mode 시작 씬을 보관하고, 검증 중에는 Title 강제 시작을 중단합니다. 테스트가 직접 필요한 씬을 로드하며, 완료/실패 후 이전 시작 씬을 복원합니다. 에디터 자체가 강제 종료되면 정상 종료 시의 복원 절차를 실행할 수 없습니다.

Test Framework 1.6.0의 결과 콜백은 임시 씬/프로젝트 설정 정리보다 먼저 호출됩니다. 실행기는 정리 작업 종료까지 기다린 뒤 다음 테스트, 빌드, 에디터 종료로 진행합니다. 이 버전에 공개된 작업 완료 API가 없어 `FoundationVerification`의 작은 어댑터에서 내부 작업 상태를 읽습니다. Test Framework를 업그레이드할 때 이 어댑터와 임시 씬 정리/시작 씬 복원을 함께 확인하세요.

전체 검증의 XML은 `Logs/verification-editmode.xml`, `Logs/verification-playmode.xml`에 저장됩니다. 부분 검증은 `Logs/verification-ui.xml`, `Logs/verification-gameplay.xml`, `Logs/verification-authoring.xml`을 사용합니다. `Logs/verification.txt`는 마지막 메뉴 실행의 동작별 결과입니다. 실패, 취소, 미실행 또는 0개 실행을 성공으로 간주하지 않습니다.

## 버튼과 동작의 기대 결과

| 구역 | 입력/동작 | 기대 결과 | 테스트 |
| --- | --- | --- | --- |
| Title | 배경 클릭 | Bootstrap을 거쳐 곡 선택, 중복 시작 방지 | MainMenuFlowTests |
| Title | 설정 아이콘 | 현재 사양은 placeholder: 화면 전환/게임 시작 없음 | MainMenuFlowTests |
| Title | 종료 아이콘 | 확인 팝업 표시, 배경 메뉴 잠금 | MainMenuFlowTests |
| Title | 아니요 | 팝업 닫기, 메뉴 다시 활성화 | MainMenuFlowTests |
| Title | 예 | 종료 요청 1회, 게임 시작 없음 | MainMenuFlowTests |
| 곡 선택 | 곡/난이도/스크롤/배속 변경 | 해당 선택 반영, 독립 옵션 유지 | SongSelectionFlowTests |
| 곡 선택 | Play | 선택한 곡과 옵션으로 Gameplay 로드 | SongSelectionFlowTests |
| 곡 선택/설정 | Settings / Back | 설정 진입/복귀, 기존 곡 선택 유지 | SettingsFlowTests |
| 설정 | 버퍼 선택 | 요청값만 변경, 실제 오디오는 그대로 | SettingsFlowTests |
| 설정 | Apply | 실제 DSP 설정과 결과 표시, Default 복원 | SettingsFlowTests |
| Gameplay HUD | Ready/Completed의 Return | 곡 선택으로 복귀, 세션/음원 정리 | GameplayHudFlowTests |
| Gameplay HUD | 플레이 중 | Return 숨김, Escape로 복귀 가능 | GameplayHudFlowTests |
| Gameplay | Space / 재시작 / 포커스 상실 | 재생 시작, 상태 초기화, 눌린 키 재입력 차단, Ready 복귀 | GameplayFlowTests, GameplayJudgmentTests |
| Gameplay | 타임스탬프가 있는 D/F/J/K | 탭/홀드/동시 입력과 180ms 지연에도 13 Perfect / 2 Good / 3 Miss | GameplayJudgmentTests |
| Gameplay | 재시작 후 정확한 입력 / 무입력 | 18 Perfect / 18 Miss 각각 독립 검증 | GameplayJudgmentTests |
| Gameplay | 템포 경계의 홀드 | Constant와 BPM 각각 8 Perfect, 완료 HUD 시간 유지 | GameplayJudgmentTests |
| 오디오 | 각 버퍼 적용 / 재생 / 재시작 | 실제 설정 보고, DSP 진행, 게임 중 변경 거부 | AudioFlowTests |
| 문서 | 플레이테스트/취소/복귀 | 스냅샷과 세션 ID 유지, 음원 해제, 재시도 | AuthoringFlowTests |
| 씬 생성 | 5개 씬 재생성/반복 생성 | Title 포함 참조 유효, GUID 및 콘텐츠 보존 | SceneRegenerationTests, ContentTests |

종료 테스트는 가짜 `IMainMenuActions`를 사용해 종료 요청까지 검증합니다. 실제 프로세스를 종료하면 Test Runner도 중단되므로 OS 프로세스 종료 자체를 자동 검증했다고 해석하지 않습니다. 타이틀 설정 버튼에 기능이 추가되면 placeholder 테스트의 기대 결과도 함께 바꿉니다.

버튼 경로는 가능한 한 가상 마우스 입력으로 uGUI/Input System을 통과시킵니다. 공통 클릭 도우미는 버튼이 활성화되어 있고 다른 UI에 가려지지 않았는지도 확인합니다. 드롭다운 값 변경 테스트는 선택 콜백과 결과를 확인하며, 별도 키보드 테스트가 실제 선택 UI 경로를 확인합니다. 서비스 메서드를 직접 호출한 테스트만으로 버튼 연결까지 검증했다고 하지 않습니다.

## 팀원이 기능을 추가할 때

1. 버튼/키/상태 변화의 기대 결과를 먼저 한 문장으로 정합니다.
2. UI는 해당 화면 fixture에, 판정/입력은 Gameplay에, 문서 기능은 Authoring에 독립 테스트를 추가합니다.
3. 입력 전 상태를 만들고 실제 동작을 수행한 뒤 화면, 서비스 상태, 해제된 리소스 등 결과를 검사합니다.
4. 비동기 대기에는 시간 제한을 두고, 실패해도 입력 장치/오디오/이벤트 구독을 정리합니다.
5. 관련 fixture를 먼저 실행합니다. 공통 흐름이나 씬 조립을 바꿨다면 Foundation 전체를 실행합니다.

레이아웃은 빌더에 반영합니다. 테스트도 UI 객체 이름보다 View의 명시적 참조를 우선합니다. 기존 판정 수치는 검증용 차트의 계약이므로 검증용 차트를 의도적으로 바꾸면 함께 검토해야 합니다.

## 배치 실행

해당 프로젝트를 연 Unity를 닫고 저장소 루트 PowerShell에서 실행합니다. 각 프로세스가 종료될 때까지 기다리고 `-quit`는 붙이지 않습니다.

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
& $unity -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testCategory UI -testResults "$PWD/Logs/ui.xml" -logFile "$PWD/Logs/ui.log"
# Gameplay 또는 Authoring도 같은 방법으로 선택합니다.
& $unity -batchmode -projectPath "$PWD" -executeMethod RhythmDojo.EditorTools.FoundationVerification.RunAll -logFile "$PWD/Logs/foundation.log"
```

오디오 테스트는 출력 장치에 의존합니다. 실제 DSP 설정/시간 진행을 검사하며 사람이 느끼는 입력 지연이나 잡음이 없음을 보증하지 않습니다. 스크린샷은 `Logs/`에 남는 보조 자료이고 자동 시각 비교는 아닙니다.
