# 낮은 DSP 버퍼의 성능 점검

2026-09-28, Unity 6000.3.23f1, Windows, 48000 Hz. 사용자 비교 대상은 DJMAX의 **ASIO 64 샘플**이다.

## 결론

현재 앱에는 ASIO 출력 모드나 ASIO 드라이버 버퍼를 설정하는 코드가 없다. 설정 화면은 `AudioConfiguration.dspBufferSize`만 변경한다. 따라서 이번 Unity DSP 64 샘플 결과를 사용자의 ASIO 64 샘플 재생 능력이나 장치의 최소 버퍼 한계로 해석할 수 없다.

Unity 문서는 DSP 버퍼 크기가 단일 믹서 버퍼의 크기이며 추가 DSP 버퍼 및 플랫폼 오디오 버퍼의 지연은 포함하지 않는다고 설명한다. [Unity DSP 버퍼 문서](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioConfiguration-dspBufferSize.html)

ASIO와 Windows의 저지연 출력 경로는 별도로 고려해야 한다. Windows 공유 출력의 지원 주기와 실제 주기는 `IAudioClient3`로 조회·요청할 수 있다. 이번 작업에서는 Unity가 사용한 네이티브 출력 API 및 드라이버의 실제 주기를 직접 조회하지 않았다. [Microsoft 저지연 오디오 문서](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/low-latency-audio)

## 재현 비교

- 배치 Editor: 64 샘플에서 DSP/실제 시간 약 0.157. [기존 원시 측정](audio-buffers-batch.txt)
- 배치 모드를 뺀 Editor에서도 약 0.157. [측정](audio-buffers-normal-editor.txt), [실행 로그](audio-normal-editor.log), [테스트 결과](audio-normal-editor.xml)
- 독립 Windows 진단 플레이어: `editor=False`, `batch=False`. 백그라운드의 640×360 창, Development Build, 기본 오디오 출력, Run In Background 활성화. [최종 원시 측정](audio-player-probe.csv), [실행 로그](audio-player-probe.log), [빌드 로그](audio-probe-build.log)

각 플레이어 구간에서 Reset 후 0.5초 기다린 뒤 3초씩 측정했다. 모든 설정의 실제 버퍼 개수는 4개였다.

| 구간 | 샘플 | DSP/실제 시간 | 3초간 GC 할당 | GC 수집 |
| --- | --- | --- | --- | --- |
| 빈 장면, 프레임 제한 없음 | 64 | 0.1564 | 0 B | 0 |
| 메모리에서 만든 톤, 기본 AudioSource | 64 | 0.1564 | 0 B | 0 |
| 빈 장면, 120 FPS 제한 | 64 | 0.1564 | 0 B | 0 |
| 게임 재생 | 64 | 0.1551 | 80,980 B | 0 |
| 게임 재시작 | 64 | 0.1564 | 80,980 B | 0 |
| 빈 장면, 프레임 제한 없음 | 128 | 0.1831 | 0 B | 0 |
| 톤 재생 | 128 | 0.1858 | 0 B | 0 |
| 빈 장면, 120 FPS 제한 | 128 | 0.1831 | 0 B | 0 |
| 톤 재생 | 256 | 0.9600 | 0 B | 0 |
| 빈 장면, 120 FPS 제한 | 256 | 0.9867 | 0 B | 0 |
| 게임 재생 | 256 | 0.9902 | 83,550 B | 0 |
| 게임 재시작 | 256 | 0.9920 | 83,168 B | 0 |
| 톤 재생 | 1024 | 1.0026 | 0 B | 0 |

GC 할당은 Unity `ProfilerRecorder`의 `GC Allocated In Frame` 값을 합산했다. 톤·게임 재생·재시작 구간은 측정 끝에서 AudioSource.isPlaying도 true였다.

이 결과는 게임플레이 코드가 없어도 동일하게 발생하는 현상임을 보여준다. **판정 스캔, 노트 Transform 갱신, HUD, 차트 디코딩, 게임의 GC 압력이 이 지연의 주원인이라는 근거는 없다.** 우선 점검할 범위는 Unity 내장 오디오 출력과 네이티브 드라이버 연결·스케줄링이다. 정확히 엔진 문제인지, 드라이버 문제인지, 백그라운드 실행 영향인지까지 확정하지는 않았다.

## 코드 점검

- SongClock은 DSP 시각을 읽고 PlayScheduled를 호출한다. 매 프레임 오디오 데이터를 생성하거나 파일을 읽지 않는다.
- 곡 오디오는 PCM / DecompressOnLoad / preload로 준비된다. 실행 중 스트리밍 디코더는 없다.
- 앱에는 OnAudioFilterRead, 자체 믹서, 오디오 스레드의 사용자 락이 없다.
- 노트 및 재질 생성은 곡 로드 시에 수행한다. 노트 렌더링 경로의 Vector3는 값 형식이며 관리 힙 할당이 아니다.
- HUD 문자열 갱신에는 개선 여지가 있다. 게임 측정에서 약 27 KB/s의 관리 메모리 할당이 관측됐다. 그러나 할당이 없는 빈 장면과 톤에서도 64 샘플의 결과가 같았으므로 이번 문제를 해결할 목적으로 HUD나 노트 구조를 변경하지 않았다.

## 수정 및 재실행

- 설정 UI에 **Unity DSP buffer size / Actual Unity DSP buffer**라고 명시했다. Unity 믹서 버퍼 변경은 ASIO 선택이나 ASIO 드라이버 버퍼 변경이 아니라는 설명을 추가했다.
- 테스트 전용 `AudioBufferProbe`와 Editor 전용 `AudioBufferVerification.BuildPlayer`를 추가했다. 진단 장면은 빌드 후 삭제되고 일반 플레이어에는 진단 컴포넌트와 테스트 어셈블리가 포함되지 않는다. 일반 게임의 오디오 관리 구조는 변경하지 않았다.
- 재현 명령: Editor에서 `-batchmode -executeMethod RhythmDojo.EditorTools.AudioBufferVerification.BuildPlayer -quit`를 실행한 뒤 `Builds/AudioProbe/AudioProbe.exe -audioProbeLog C:/Users/song/RhythmDojo/Logs/audio-player-probe.csv`를 **-batchmode 없이** 실행한다. 앱은 설정을 복원하고 자동 종료한다.

수정 후 네 장면 생성이 성공했고 [Play Mode 테스트 6/6](audio-audit-playmode.xml)이 통과했다. 테스트 어셈블리를 포함하지 않는 [일반 Windows 플레이어 빌드](audio-audit-shipping-build.log)도 성공했다. `ProjectSettings/AudioManager.asset`의 SHA-256은 최초 검증 때와 동일하다. 진단 장면 파일은 남아 있지 않다.

진단은 청취나 루프백 녹음으로 실제 드롭아웃을 측정한 것이 아니며, ASIO 백엔드를 구현하거나 ASIO 64 샘플 재생을 검증한 것도 아니다. 사용자가 플레이하는 일반 전경 창의 결과와 비교하기 위해 추가 측정할 수 있도록 재현 도구를 남겼다.
