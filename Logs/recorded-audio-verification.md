# 녹음 효과음 검증 — 2026-11-14

브랜치 `emergency`, Unity 6000.3.23f1. 사용자의 기존 작업을 유지한 상태에서 실행했다.
음악 시계·판정·입력 타임스탬프는 변경하지 않았다. 새 효과음 풀과 실제 화면의 이벤트 연결을 검증했다.

## Editor 실제 재생 및 통합 검사

| 범위 | 결과 / 근거 |
| --- | --- |
| 신규 녹음 효과음 통합 | 2/2 통과. 입력 직후 스윙 → 접촉 시 타격, 목록 재개방 후 중복 없음, 연타 시 다른 테이크·음성 수 제한·음소거. [JSON](recorded-audio-focused.json) |
| 기존 GameplayHitFeedback | 5/5 통과. AudioListener 출력 샘플의 실제 비영점 신호, Miss 무음, 화음 중복 억제, 풀 재사용·리셋. [JSON](recorded-audio-GameplayHitFeedbackTests.json) |
| 기존 공통 클릭 | 최종 3/3 통과. [JSON](recorded-audio-click-final.json) |
| 기존 목인 곡 선택 | 최초 7/8 통과, 60초 연타 검사 실패. [최초 JSON](recorded-audio-PracticeSelectionTests.json) |
| 연타 재검사 | 첫 재검사도 실패. 계측만 추가한 마지막 재검사는 통과. [실패](recorded-audio-endurance-retry.json), [최종](recorded-audio-endurance-diagnostic.json) |
| Smoke | 현재 선택된 5/5 통과. 실제 타이틀·선곡·게임·완료 결과 흐름. [JSON](recorded-audio-smoke.json) |
| 모드 화면 목인 접촉 | 1/1 통과. 스윙과 접촉의 분리, 동작 취소 후 잔여 타격 없음. [JSON](recorded-audio-mode-contact.json) |
| 비급 전수 시범·모방 | 1/1 통과. 준비 동작 무음, 주먹/발차기는 사형과 수련생 각 1회 스윙, 공중 동작에 나무 충돌 없음. [JSON](recorded-audio-lesson.json) |
| 실패 결과 / Esc | 1/1 통과. [JSON](recorded-audio-results-failure.json) |

마지막 60초 연타는 572접촉 / 3,598프레임, 50ms 초과 프레임 0, 최장 프레임 36.94ms,
입력 유실 프레임 0, 애니메이션 경로 할당 0bytes였다.
[타이밍](practice-barrage-timing.txt), [기존 성능 계측](practice-barrage-performance.txt).
최초 두 실행은 각각 228접촉과 3접촉으로 실패했다. 최종 진단은 정상 실행을 확인하지만,
이 결과만으로 앞선 실패 원인을 확정하거나 불안정성이 해소되었다고 단정하지 않는다.
성공 기준을 낮추거나 반복 입력을 테스트에 추가해 통과시키지 않았다.

이름 필터를 정규식처럼 사용해 0건이 된 실행과, `ResultsFlowTests` 부분 문자열에 걸린
무관한 Pupa 검사 취소 실행은 성공 집계에서 제외했다. 전체 테스트 통과를 주장하지 않는다.

## 에셋·구성

- 실제 빌더로 Title / ModeSelection / SongSelection / Gameplay / Results와 커서 에셋을 재생성했다.
  [완료 기록](sound-design-scenes-built.txt). 고정 오디오 소스는 직렬화된 씬 구성이다.
- 29개 출력 WAV의 SHA-256을 원본 키가 포함된 `Tools/AudioSources/outputs.json`과 대조했다.
- Editor에서 뱅크 고유 21개 클립의 Loaded / mono / 32kHz / DecompressOnLoad / Preload를 확인했다.
  float PCM 합 2,140,612bytes. [실측](recorded-audio-import-audit.txt).
- 모든 개별 녹음의 CC0와 출처는 [사운드 문서](../Docs/sound-design.md)에 기록했다.
- PCM 어택 검사에서 판정음의 첫 10% 피크는 시작 후 1.47–2.38ms, 최대 피크는 3.84–8.59ms였다.
  목인 첫 어택 3.62–5.53ms, 스윙 4.38–10.12ms, 송판 균열 6.03ms.
  파일 선행 무음 검사이며 실제 입력-스피커 지연 측정은 아니다. [수치](recorded-audio-attack-audit.json).

## 청취 범위

[가공본 비교 페이지](AudioReview/index.html)와 [순서별 WAV](AudioReview/recorded-foley-review.wav)를 제공한다.
개별 파일은 게임 내 게인 적용 전이다. 코드·파형·실제 출력 계측을 통과한 것과 사람이 직접 듣고
음색·피로도·음악과의 균형을 최종 승인한 것은 다르다. 이 자동화 환경에서 사람의 청감 검수는 완료하지 않았다.

## WebGL

Unity의 최종 압축이 진행되는 동안 생성 완료된 동일 WebAssembly / framework / serialized data를
별도 `Builds/WebGL-AudioReview-Uncompressed` 래퍼에서 먼저 실행했다. 이 검사는 최종 Brotli
배포 페이지를 검증한 것으로 계산하지 않는다.

- Chrome 156.0.8078.80, headless, 자동재생 정책 `document-user-activation-required`, 48kHz.
  최초 입력 전 AudioContext=suspended, userActivation=false, 재생 시작 0회.
  실제 Enter/클릭 입장 이후 running 전환 및 출력. [입력 전 계측](webgl-before-input.json).
- 타이틀 → 모드 목인 → 비급 전수 시범 → 자유 연습 곡 선택 → 6초 방향키 연타 →
  실제 Gameplay → 실패 Results → 재시도 후 D/F/J/K 입력 → Results → 곡 선택 복귀를 실행했다.
  실제 판정 결과 Perfect 10 / Good 1을 확인했다. [화면](AudioReview/webgl-gameplay-result.png).
- 6.52초 연타 측정 구간의 decodeAudioData 호출 수는 전후 모두 29였다. 곡/씬 진입 시의 추가
  디코딩과 입력 중 디코딩을 구분했다. 해당 구간 피크 0.2175, 전체 측정 피크 0.2540(-11.90dBFS).
  녹음 파일의 ffmpeg volumedetect도 최대 -11.9dBFS를 확인했다.
- 네이티브 AudioBufferSource 관측 최대 8개(음악·환경·정리 중 소스 포함). Unity는 한 재생에
  임시 노드를 시작한 뒤 0.02–0.13ms 내 정지하고 속성을 적용한 노드로 교체했다.
  따라서 JS start 호출 수를 실제 중복 효과음 횟수로 해석하지 않는다.
  [직접 start/stop 추적](webgl-native-source-trace.json).
- JavaScript 예외/미처리 Promise 거부 0. 경고는 persistentDataPath 동기화 deprecated,
  타이틀 이동 중 active EventSystem 중복, clip 없는 source의 timeSamples 조회였다.
  이번 변경이 해당 경고를 새로 만들었는지 비교 빌드로 확정하지 않았으며 무경고라고 보고하지 않는다.
- [계측 원문](webgl-audio-playback.json), [원본 WebM](AudioReview/webgl-playback.webm),
  [실제 브라우저 출력 WAV](AudioReview/webgl-playback.wav). MediaStreamDestination에서 취득한
  브라우저 믹스이며 스피커/마이크를 통한 물리 출력 녹음은 아니다.

### 최종 압축 배포 빌드

- `Builds/WebGL-AudioReview`: **Succeeded**, 오류 0, 경고 5, 소요 1,619.83초.
  전체 배포 파일 90,146,767bytes(약 85.97MiB); data.br 80,005,102bytes, wasm.br 10,014,597bytes.
  이는 효과음 용량이 아니라 기존 게임 리소스·코드를 포함한 전체 빌드다.
  [작은 요약](recorded-audio-webgl-summary.json), [전체 BuildReport](recorded-audio-webgl-build.json).
- 경고: Player용 RuntimePipelineConfig 없음 1, 기존 수정 영역인 WorkspaceEditor의 CS4014 3,
  DojoFistImprintBaked 커서의 WebGL 128px 제한 적용 1. 효과음 코드의 컴파일 경고는 없다.
- Brotli Content-Encoding을 제공하는 로컬 서버에서 **생성된 기본 index.html 그대로** 실행했다.
  첫 입력 전 suspended → Enter 후 running, 타이틀/문/방/목인/선곡/게임/실패 결과까지 확인했다.
  최종 빌드에서 실제 Perfect 6 / Good 4 판정이 발생했다.
  [목인 화면](AudioReview/webgl-final-selection.png), [결과 화면](AudioReview/webgl-final-gameplay-result.png).
- 최종 페이지의 4초 연타 전후 decodeAudioData=29/29, 전체 관측 피크 0.2475(-12.13dBFS),
  JavaScript 예외 0, 네이티브 노드 최대 8. 위와 동일한 런타임 경고 3종은 남는다.
  [최종 계측](webgl-final-playback.json), [입력 전](webgl-final-before-input.json),
  [최종 빌드 게임 출력 WAV](AudioReview/webgl-final-gameplay.wav).
- 검증 후 StandaloneWindows64로 복원 완료, Play Mode stopped / Editor ready.
  Title 씬은 저장된 구성으로 유지했다. [플랫폼 복원](recorded-audio-restore-target.json).

재실행: 저장소 루트에서 `python Logs/serve_audio_web.py Builds/WebGL-AudioReview 8772` 실행 후
`http://127.0.0.1:8772/`를 연다. 서버는 Brotli·WASM MIME 및 압축 헤더를 제공한다.
파일 탐색기에서 index.html을 직접 여는 방식은 WebGL 실행 검증이 아니다.

WebGL 전체 메모리·물리 입력 지연·Safari/iOS/모바일 기기·장시간 브라우저 플레이는 미측정이다.
localhost 로딩 성공은 실제 배포망의 초기 다운로드 시간을 보장하지 않는다.
