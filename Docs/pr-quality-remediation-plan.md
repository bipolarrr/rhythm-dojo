# PR #5·#6·#7 품질 개선 작업 장부

## 실행 계약

기능과 시각적 결과를 유지하며 아래 작업을 순서대로 완료한다. 각 작업은 전담 구현 에이전트의 재현·수정·회귀 테스트, 메인 에이전트의 계약 검토·Unity 실행, 별도 읽기 전용 검토 에이전트의 검토, 지적 해결, 작업별 커밋 순서로 진행한다. 구현은 한 번에 하나만 진행한다. Unity 실행과 공통 계약, 장부, 커밋은 메인 에이전트가 소유한다.

- 작업 위치: `C:/Users/song/RhythmDojo-pr-quality`
- 통합 브랜치: `integrate/pr-quality-remediation`
- 원래 작업 위치 `C:/Users/song/RhythmDojo`의 `Results.unity` 변경과 `InitTestScenecf8cf22c-8089-4362-bb81-93985c9fac3f.unity` 및 `.meta`는 보존한다.
- 기존 이름·공개 경로·GUID를 유지한다. Domain/Application에 Unity 의존성을 추가하지 않는다.
- 과거 문서의 실패는 현재 실패로 간주하지 않는다. 기존 실패 / 새 회귀 / 환경 장애 / 미실행을 구분한다. 테스트 기대값 완화나 로그 무시로 실패를 숨기지 않는다.
- 외부 오디오 회귀 테스트는 임시 WAV와 실제 외부 로더를 사용한다. PUPA 또는 개인 파일에 의존하지 않는다.
- 소스 변경 후 refresh·컴파일, 한 번에 한 테스트 실행, 결과 후 Play Mode 종료와 Editor ready를 확인한다. 통과 결과는 동일 커밋·범위에만 재사용한다.
- 증거 위치: `Logs/pr-quality-remediation/`. 이 디렉터리는 기존 `.gitignore`에 의해 무시되므로 커밋할 증거는 명시적으로 선택한다.
- GitHub 게시, 기존 PR 변경, main 병합, 강제 푸시는 범위 밖이다.

## 기준 커밋과 환경

2026-11-02 실행 시 GitHub PR 메타데이터와 fetch한 이력을 확인했다.

| 기준 | SHA | 의존 관계 |
| --- | --- | --- |
| main | `aacf4d4a063606533faf58693dafe92457a59c23` | 기준 |
| PR #5 | `4efdc625c6fd4553dc48aa929ee61dc82bd4f560` | main 기반 |
| PR #6 | `6610c1f6c6847e5eda14b1dd72fecef998314e2c` | #5 기반; 설정 커밋 `0927fb4` 포함 |
| PR #7 | `d34b688c9411817e02f84a2f49b8d1cd7f90192a` | #6 기반; 통합 시작 커밋 |

`git merge-base --is-ancestor`로 #5 → #6 → #7 관계를 확인했다. Unity 버전은 `6000.3.23f1 (09d2ecc7fb28)`이며 설치 실행 파일이 존재한다. manifest/lock 기준 Test Framework `1.6.0`, Pipeline `0.8.0-exp.1`, Input System `1.20.0`, uGUI `2.0.0`이다. 등록 씬 7개는 Title, Bootstrap, SongSelection, Gameplay, Settings, Results, ChartEditor이며 모두 enabled다. `Assets/Game/Audio`의 6개 WAV와 `.meta`가 존재한다(저장소 전체 WAV는 Resources/UI의 3개를 포함해 9개).

원래 프로젝트의 CLI 연결은 메인 에이전트가 ready로 확인했다. 새 worktree의 첫 검증은 별도 batch Editor에서 실행하며 원래 Editor를 종료하거나 수정하지 않는다. WMI의 USB, NVIDIA, Steam, Realtek 장치는 OK 상태였으나 실제 출력 검증을 의미하지 않는다.

## 작업 상태

### 작업 0 — 기준 상태 확보: 완료 (기존 실패 보존)

- 재현/측정: 변경 전 #7에서 `Verify All Tests (No Build)`를 한 번 실행한다. 수동 콘텐츠 재생성은 하지 않는다. 기존 ContentTests 및 SceneRegenerationTests가 BuildAll을 호출하므로 PlayMode 기준은 d34b688 소스와 테스트가 재생성한 씬의 조합이다. 생성 diff를 보존하고 테스트 종료 후 복원한다.
- 결정: 메인 에이전트가 아래 명령으로 baseline batch process 13084를 시작했다. batch 실행은 종료했다. EditMode 241 통과; PlayMode 114 통과, 7 실패, 3 skip. 일반 Editor에서 커서 5/5 및 렌더링 1/1 통과, Results 전환 시간 초과와 곡 선택 불일치 각각 1건은 재현됐다. 전체 통과가 아니다.
- 실행 명령: `& 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Users/song/RhythmDojo-pr-quality' -executeMethod RhythmDojo.EditorTools.FoundationVerification.RunTestsOnly -logFile 'C:/Users/song/RhythmDojo-pr-quality/Logs/pr-quality-remediation/baseline-editor.log'`
- 증거: `Logs/pr-quality-remediation/baseline-editor.log`; runner 출력은 `Logs/verification-editmode.xml`, `Logs/verification-playmode.xml`, `Logs/verification.txt`이며 완료 후 baseline 이름으로 보존한다. 정적 환경 증거는 `Logs/pr-quality-remediation/baseline-environment.md`.
- 커밋: `c569072` (기준 기록 및 증거). 테스트 소스 기준 `d34b688`.
- 미해결: runner는 EditMode 실패/skip/inconclusive 시 PlayMode를 실행하지 않는다. 그 경우 PlayMode를 별도 실행해 미실행 범위를 채운다. PUPA 전용 3개 테스트는 `LocalAudio/PUPA.wav` 부재 시 Ignore하며 파일은 현재 없다. 이를 통과로 표시하지 않는다.
- 다음 행동: 원본 XML과 baseline-generated.patch를 보존하고 생성 변경을 복원했다. 독립 검토의 씬 재생성 조건/WAV 집계 지적을 수정했다. CLI 일반 Editor PID1424 ready 및 PlayMode 종료 확인. 작업 1로 진행하며 기존 Results/선택 실패는 최종 검증 전에 해결한다.

### 작업 1 — 정지 입력 판정 시각: 완료

- 재현: 일시정지 중 해제와 재입력, 보정값 0/양수/음수에서 정지 전후 동일 논리 입력 비교.
- 결정/범위: 정지 순간 논리 곡 시각과 보정값을 고정한다. 정지 중 판정하지 않고 재개 시 대기 입력을 한 번 반영한다. 다시 누른 레인은 해제를 취소한다. 일반/합성 보정 계산은 Application 공통 경로로 모은다.
- 검증: CoordinatorTests; Release/Automatic 홀드, 판정창 경계, 해제 후 재입력, 재시작/폐기 시 대기 입력 제거.
- 커밋/증거: 없음 / 미실행.
- 결과: 수정 전 CoordinatorTests 60개 중 24 실패; 수정 후 60/60 통과. GameplayPauseFlowTests 2/2 통과. 각 실행 후 PlayMode 종료와 Editor ready 확인. 독립 검토에서 수정 요구 없음. 증거: task1.md, task1-red.json, task1-green.json, task1-pause-flow.json (Logs/pr-quality-remediation).
- 커밋: `82e5575`. 미해결 없음. 다음 행동: 작업 2 외부 음원 복구.

### 작업 2 — 외부 음원 복구: 완료

- 재현: 실제 임시 WAV 로더로 로딩한 클립에서 오디오 설정 재설정.
- 결정/범위: SongAudioLease에 재로딩 공급 경로를 추가하고 DocumentSongLoader → PlayableSong에 수명을 전달한다. 기존 생성자와 내장 대여를 유지한다. PlayableSong이 현재 lease/복구를 소유하고 SongClock에 외부 경로를 넣지 않는다. 마지막 설정 세대만 채택하며 이전/취소 결과는 정확히 한 번 해제한다. 복구 중 나가기는 허용하고 재개는 차단한다. 실패 시 정지·오류를 유지하며 재시도한다.
- 검증: 외부 WAV/내장 클립, 연속 재설정, 파일 삭제, 복구 중 나가기, 중복 해제, 에디터 테스트 플레이 복귀; 참조/isPlaying 외 샘플과 실제 출력 확인.
- 커밋/증거: 없음 / 미실행.
- 결과: 실제 WAV reset 후 GetData=false/peak=0 재현. 복구 후 외부/내장 mixer peak 0.11653/0.06048. Coordinator 61/61, AudioLeaseRecovery 5/5, AudioRecoveryFlow 6/6, GameplayPauseFlow 2/2, Smoke 4/4 통과. 각 실행 후 PlayMode 종료·Editor ready 확인. Gameplay만 재생성. 초기화 전 무효화 검토 지적을 수정하고 재검토 통과.
- 증거: Logs/pr-quality-remediation/task2*. 미해결 없음. 커밋: `9e2694b`. 다음 행동: 작업 3 공통 DSP 재개 기준점.

### 작업 3 — 재개 DSP 기준점: 완료

- 재현: 여러 오프셋/버퍼에서 정지·재개 후 오디오와 논리 시각 측정.
- 결정/범위: 오디오 시각 = 곡 시각 − 채보 오프셋으로 샘플을 계산한다. 사용자 보정은 제외한다. 오디오·곡 시계가 하나의 예약 DSP 기준점을 공유하고 그 전에는 세션도 정지한다. 음원 시작 전/끝 이후/설정 변경/반복 정지를 처리한다. 끝 이후 마지막 샘플을 재생하지 않는다.
- 검증: 변환 오차 1샘플 이내; 20회 반복, 양수/음수 오프셋, 여러 버퍼; 실제 오차에 DSP 블록·관측 프레임 불확실성을 기록한다.
- 커밋/증거: 없음 / 미실행.
- 결과: 수정 전 시계 2/2 실패. Coordinator 67/67, 샘플 변환 37/37, 시계 7건 + 반복 측정 1건, PauseFlow 2/2, AudioRecoveryFlow 6/6 통과. 80회 재개·640관측 모두 블록+프레임 오차 한계 이내. 검토 지적(늦은 입력/첫 프레임 역행) 해결 후 재검토 통과.
- 측정 한계: 원시128 버퍼 DSP 약0.2배속 실패 보존. 실제 설정 서비스128→256 복구 및1024 적용에서 통과(최대 관측오차14.7/32.0ms). 원시128 정상으로 해석하지 않는다. 증거: Logs/pr-quality-remediation/task3*. 다음 행동: 작업4 진행률 정책.

### 작업 4 — DSP 시계 진행률 검사: 대기

- 재현: 가짜 backend 정상/0.98/1.02 진행률, 블록 갱신·프레임 지연.
- 결정/범위: 양 끝의 샘플레이트·DSP 블록 관측 오차를 포함한 진행률 구간 전체가 1 ±1% 안에 연속 두 번 들어와야 정상이다. 불확실하면 연장하며 전체 20초 시간 초과는 확인 실패다. 정지/설정 변경 시 관측을 무효화한다. 측정 정책과 더 큰 후보 버퍼 복구 정책을 분리하고 실제 적용값을 확인한다. UI는 출력 품질/체감 지연 보증이 아닌 시계 진행률 검사로 설명한다.
- 검증: 시계 리셋, 장치 거부/보정, 취소/시간 초과 포함; 실제 장치 요청값/적용값/관측 구간/판정 근거 기록.
- 커밋/증거: 없음 / 미실행.
- 미해결/다음 행동: 작업 3 완료 후 정책 재현.

### 작업 5 — 모드별 키 설정: 대기

- 재현: 서로 다른 4레인 모드 격리와 6레인 설정 화면.
- 결정/범위: GameModeDefinition에서 기본 바인딩/표시명/레인 수를 읽고 원래 ModeId별 변경분을 저장한다. 타이틀 기본 모드/선택 곡 모드/플레이 요청 모드를 설정 대상으로 고정한다. 기존 View 필드를 유지하며 행 템플릿·스크롤 컨테이너를 추가한다. 중복·시작/복귀 바인딩 충돌을 검사하고 Esc 취소를 유지한다. v1은 기존 기본 4레인에 한 번만 이전하며 새 저장소 우선, 원본 보존, 초기화는 해당 모드만 적용한다. 비키보드 기본값은 보존하고 편집을 비활성화한다.
- 검증: 기본값/초기화/v1 이전/손상 데이터/예약 키/플레이 중 변경/HUD·결과 일치; 빌더에 PC PlayerPrefs를 굽지 않는다.
- 커밋/증거: 없음 / 미실행.
- 미해결/다음 행동: 작업 4 완료 후 계약과 저장 경로 추적.

### 작업 6 — UI 중복 구현: 대기

- 재현: 기존 크기와 작은 크기의 artwork 프레임을 비교한다.
- 결정/범위: 두 로더를 유지하며 importer·sprite 생성·저장을 Editor 공통 헬퍼로 이동한다. 호출자가 crop/border를 명시하고 GUID/meta를 보존한다. PixelDojoFrameGraphic artwork는 Image.Type.Sliced와 중앙/테두리 tint 레이어를 사용하며 절차적 경로를 유지한다. UiFontSettings.Apply/ApplyTo의 글꼴 결정을 공유한다. 빌더 수정 후 영향받는 씬/프리팹만 재생성한다.
- 검증: 테두리/중앙 tint/마스크/클릭 영역 시각 비교, 타이포그래피·레이아웃·씬 재생성 및 GUID 유지.
- 커밋/증거: 없음 / 미실행.
- 미해결/다음 행동: 작업 5 완료 후 시각 기준 확보.

### 작업 7 — 씬 준비 수명·입력 차단: 대기

- 재현: 느린 준비 중 에디터 이동, 모달과 준비 완료 중첩, 중복 Play, 실패 후 재시도.
- 결정/범위: ModalInputScope의 상태 보존/복원을 추출하여 중첩 소유자의 마지막 해제 때 복원한다. 준비 객체가 로드 작업/씬/차단/Canvas 순서/세대를 소유한다. 사전 준비와 클릭 로딩은 같은 활성화 경로를 사용한다. AppFlow는 이동을 조율한다. 취소 불가 로드는 무효화하고 늦은 씬은 서비스 연결/활성화 없이 언로드한다. 실패/시간 초과/이탈/재진입 상태·포커스를 복원한다. 문 연출은 초기화와 이전 씬 정리 완료 신호를 받는다.
- 검증: 타이틀 재진입, 준비 없는 진입, 직접 씬 실행 포함; 관련 화면 테스트+Smoke, 잔류 씬/EventSystem/AudioListener/차단 상태 없음.
- 커밋/증거: 없음 / 미실행.
- 미해결/다음 행동: 작업 6 완료 후 수명 경로 추적.

### 최종 통합 검증: 대기

- 범위: 소스 refresh·컴파일 후 Verify Foundation 전체 테스트와 Windows 빌드. 실패 수정 후 재검증하며 부분 실행은 전체 통과로 표기하지 않는다.
- 플레이어: 외부 WAV 로드/플레이/정지/버퍼 변경/재개/재시작, 모드별 키 저장, 타이틀·설정·에디터·선택 왕복, 재실행 설정 유지, 실제 출력 확인.
- 진단: 수치 측정은 Editor API에 의존하지 않는 개발 빌드 진단으로 두고 제품 화면에 노출하지 않는다.
- 검토: 별도 최종 에이전트가 전체 diff의 하드코딩, 계약 중복, 소유권 누락, Editor 의존성, 잘못된 테스트 통과 조건을 확인한다.
- 커밋/증거: 없음 / 미실행.
- 미해결: 기준 BuildPlayer의 명시 scenes 목록에 Results가 누락돼 있다. EditorBuildSettings와 불일치하며 실제 플레이어 검증 전에 확인한다.
- 다음 행동: 작업 7까지 완료 후 전체 검증·최종 검토·장부 마감.
