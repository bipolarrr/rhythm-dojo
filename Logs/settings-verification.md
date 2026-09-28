# 검증·마이그레이션 및 오디오 설정 검증

2026-09-28, Windows, Unity 6000.3.23f1. Unity 배치 실행에서 그래픽 및 오디오를 비활성화하지 않고 확인했다.

- Edit Mode: 57/57 통과. [결과](settings-editmode.xml), [로그](settings-editmode.log)
- Play Mode: 6/6 통과. [결과](settings-playmode.xml), [로그](settings-playmode.log)
- 설정 UI: 실제 마우스와 Input System 키보드 입력으로 진입, 선택, 적용, 복귀, 재진입을 확인했다. 기본값 복귀와 실행 중 선택 유지, 화면 전환 중 및 Gameplay에서 적용 거부를 확인했다. [화면](settings.png)
- 생성·검증: 이름에 Hold가 포함된 탭은 통과하고, 이름과 관계없이 홀드 body 및 탭 head 누락, 생성 뷰 개수·참조 누락, Bootstrap 설정과 화면 필수 참조 누락은 실패했다. 런타임은 생성 뷰 누락을 허용하고 선택한 차트로 구성한다. 기존 6레인 및 다른 곡 전환 테스트도 통과했다.
- 마이그레이션: 최초 레거시 값·곡 오프셋 이전, 설정 자산 전체와 레거시가 없는 초기 생성, 이전 완료 후 레거시 오류·부재, Easy 기본 난이도와 수정한 오디오 값 보존을 확인했다. 레거시 없는 준비 시간은 0.15초다. 자산 테스트는 finally에서 원래 자산 파일과 meta/GUID를 복원한다.
- 오디오 테스트는 UnityTearDown에서 원래 AudioConfiguration을 복원한다. 버퍼 선택을 저장하는 코드 또는 PlayerPrefs 사용은 없다.

## 오디오 측정 범위

앱 시작 실제 설정은 1024 샘플, 버퍼 4개, 48000 Hz였다. 각 Reset 이후 0.3초 기다리고 약 1초간 실제 시간과 DSP 시간 진행을 비교했다. [당시 원시 측정](audio-buffers-batch.txt)

| 요청 샘플 | 실제 샘플 × 개수 | DSP / 실제 시간 |
| --- | --- | --- |
| 32 | 32 × 4 | 0.155 |
| 64 | 64 × 4 | 0.157 |
| 128 | 128 × 4 | 0.184 |
| 256 | 256 × 4 | 0.987 |
| 512 | 512 × 4 | 1.003 |
| 1024 | 1024 × 4 | 0.981 |
| 2048 | 2048 × 4 | 0.981 |

모든 Reset 호출은 성공했고 샘플레이트는 48000 Hz였다. 이 배치 실행 환경에서는 32–128 샘플의 DSP 시간이 실제 시간보다 크게 느리게 진행했다. 최초 32 샘플 재생 검사에서도 2초 후 곡 시간이 약 0.16초였다. 따라서 API 적용 성공을 안정적인 재생의 증거로 해석할 수 없다.

256 샘플에서는 곡 시작과 재시작 각각 2초 동안 곡 시간이 1초를 넘고 AudioSource가 재생 중임을 확인했다. 이 검사는 실제 출력의 청취, 오디오 루프백 녹음, 드롭아웃 개수, 입출력 지연 또는 장시간 안정성을 측정하지 않는다. 기본값·지원값과 안정성은 다른 장치 및 일반 실행 환경에서 달라질 수 있다.

추가 점검에서는 배치 모드를 뺀 Editor와 독립 Windows 플레이어에서도 같은 현상이 재현됐으며, 빈 장면·메모리 톤·120 FPS 제한으로 원인을 좁혔다. 사용자가 비교한 DJMAX는 ASIO 출력으로, 현재 Unity 믹서 설정과 직접 비교할 수 없다. 장치의 최소 버퍼 한계나 게임 코드의 최적화 한계로 해석해서는 안 된다. [추가 성능 점검 보고서](audio-performance-audit.md)

## 장면 재생성 및 빌드

전체 통합 검증이 종료 코드 0으로 통과했다. [verification.txt](verification.txt), [settings-foundation.log](settings-foundation.log)

- Bootstrap, SongSelection, Gameplay, Settings 네 장면을 삭제하고 재생성했다. 반복 생성에서 네 장면의 계층·컴포넌트·위치·스케일과 필수 참조 검증이 유지됐다.
- Bootstrap 진입 후 전체 곡 실행 5회가 통과했다: 혼합 13 Perfect / 2 Good / 3 Miss, 재시작 18 Perfect, 무입력 18 Miss, Tempo Shift Constant 및 BPM 모드 각각 8 Perfect. 180 ms 프레임 정지와 홀드·타임스탬프 입력 검증도 통과했다.
- 네 장면을 포함한 Windows x64 플레이어 빌드가 성공했다: `Builds/Windows/RhythmDojo.exe`. 플레이어의 게임 어셈블리는 Domain, Application, Runtime이며 Editor 및 게임 테스트 어셈블리는 포함되지 않았다. Runtime 소스에 UnityEditor 사용은 없다.
- 버퍼 테스트 전후와 최종 빌드 후 `ProjectSettings/AudioManager.asset`의 SHA-256이 동일했다. 레거시 스크립트·스크립트 meta·자산·자산 meta의 SHA-256도 동일했다. 레거시 상속과 GUID는 유지됐다.
- Unity 배치 프로세스는 종료됐으며 Play Mode에 남아 있지 않다.
