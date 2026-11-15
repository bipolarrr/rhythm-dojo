# 녹음 기반 도장 사운드 — emergency, 2026-11-14

실제 녹음의 나무·충돌·공기 질감을 사용한다. 신규 AI 생성이나 파형 합성은 사용하지 않았다.
기존 음악, DSP 곡 시계, 입력 시각, 판정 규칙, 보정값은 유지한다.

## 실제 적용

| 위치 / 동작 | 소리 / 시점 |
| --- | --- |
| Gameplay 입력 | Swing 3테이크, 입력 이벤트 즉시. 정지·준비·일시정지 상태 제외. 같은 프레임의 화음은 한 번만 재생 |
| Gameplay Perfect / Good | 각각 다른 3테이크. 판정 이벤트 / 롱노트 시작과 끝에서 재생. Good은 더 부드럽고 낮은 레벨, 롱노트 끝은 강도 0.7. 자동 Miss에는 충돌음 없음 |
| 자유 연습·실력 검정의 곡 선택 목인 | 기존 `PracticeStrikeAnimator.Started`에서 휘두르기, 기존 `Contact`에서 충돌. 발차기는 낮고 무거운 별도 스윙. 좌우에 작은 패닝. 단타 회전에는 작은 나무 삐걱임, 연타에는 생략 |
| ModeSelection 자유 연습 목인 | 예비동작 후 0.12초에 스윙, 기존 접촉 프레임 0.22초에 나무 충돌. 반복 클릭은 진행 중인 동작을 다시 시작하지 않음 |
| 모드 방의 자율 수련 | Punch/Kick 동작 진행률 0.18에서 스윙, 기존 0.36 접촉에서 목인 충돌. 화면의 반동과 같은 시점 |
| 비급 전수 방 | 사형 시범과 수련생 모방 각각의 동작 진행률 0.18에 스윙. 공중 시범에 목인 충돌을 붙이지 않음 |
| 모드 방 이동 | 이동 시작에 절제된 녹음 스윙, 기존 보폭 변화에 발소리 3테이크. 움직이지 않는 율보 편찬 화면에서는 발소리 없음 |
| 도장 공간 | ModeSelection의 조용한 마당 바람. 일반 방보다 실력 검정 산정에서 약간 크게. 설정/다른 화면 진입·포커스 상실 때 정리. Gameplay와 곡 미리듣기에 환경음 없음 |
| 타이틀 입장 | 기존 문 두드림과 문 열림 시점의 녹음 나무 소리로 교체. 문 소리는 실제 나무 삐걱임을 부드럽게 가공한 Foley이며 실제 미닫이문 녹음은 아님 |
| 현재 모드 메뉴의 송판 버튼 | 기존 `DojoBoardBreak.Play` 시작과 동시에 실제 나무 균열 두 조각과 나무 타격의 레이어. 사용하지 않는 화면에 새 연결을 만들지 않음 |
| 공통 클릭 | 작은 나무 탭과 낮게 깐 녹음 스윙. 한 프레임 중복 억제, 하나의 소스만 사용하며 정지 화면에서도 동작 |
| Results | 아래 전용 시퀀스에 맞춘 진입·붓결·목재 카운트·통계·성공/실패 낙관. 실제 결과 바인딩과 재진입에서 처음부터 재생 |

## 원본·라이선스

### Results 발표 시퀀스 (2026-11-15)

`Tools/generate_results_audio.py`는 위 파이프라인의 해시 검증된 CC0 원본 중
`wood`, `bamboo`, `creak`만 사용해 `Assets/Game/Audio/Foley/Results*.wav` 8개를 제작한다.
나무 타격의 대역·재생 속도·어택·감쇠를 조절하고 대나무 공기와 짧은 마찰을 겹쳤다.
붓결은 실제 나무 마찰을 종이 위 붓질처럼 가공한 Foley다. 실제 붓이나 전통 악기를
녹음했다는 뜻은 아니며, 사인파·전자 발진음은 사용하지 않았다.

| 시점 | 화면 / 음향 |
| --- | --- |
| 0–0.45초 | 배경과 기록지 등장 / 바람·종이 같은 마찰과 낮은 나무 몸통 |
| 0.20 / 0.46초 | 기존 제목·판정 글리프가 붓결 단위로 공개 / 부드러운 마찰 |
| 0.55–1.58초 | 실제 결과와 분리한 점수 카운트업 / 0.60, 0.78, 1.32, 1.45초의 작은 목재음 |
| 0.90 / 1.04 / 1.18초 | Perfect·Good·Miss 행 / 짧은 목재 타격 |
| 1.32 / 1.45초 | 정확도·콤보 공개 / 위 카운트 박자와 공유 |
| 1.58초 | 최종 점수·기존 낙관 테두리·신기록 강조 / 밝은 성공 타격 또는 낮고 둔한 실패 타격 |
| 1.72–2.02초 | 재시도·보정·상세 메뉴 공개 / 추가 효과음 없이 여운 정리 |

일반적인 총 길이는 2.02초다. 씬 활성화의 긴 프레임이 초반 붓질을 삼키지 않도록
한 프레임의 연출 진행을 50ms로 제한한다. 모든 동작과 큐는 같은 런타임 시각을 쓴다.
카운트 숫자마다 소리를 재생하지 않으며 별도 음악이나 환경음 루프를 추가하지 않는다.

`ResultsSoundPlayer`의 씬에 직렬화한 소스 3개가 종이·목재·낙관을 분리한다.
효과음 설정을 그대로 적용하며, 선택/확인은 기존 `DojoClick.wav`를 공유한다.
포인터 클릭은 기존 `DojoClickFeedback`을 사용해 이중 클릭음을 피한다.
건너뛰기·재바인딩·비활성화·포커스 상실·화면 종료는 소유한 소리를 정지한다.
정상 재생의 마지막 잔향은 약 2.02초에 끝난다.

8개 WAV는 32kHz mono PCM 16-bit, 총 PCM 138,496 bytes다. 상세 길이·피크·RMS·원본 키·
SHA-256은 `Tools/AudioSources/results-outputs.json`에 있다. 기존 뱅크와 게임플레이 음원을
덮어쓰지 않으며 `python Tools/generate_results_audio.py` 후 **Build Results**로 재생성한다.

아래 **각 음원의 개별 페이지**에서 Creative Commons 0 링크를 확인했다.
사용 근거는 [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)이며 상업 이용,
수정, 배포가 가능하고 저작자 표시 의무는 없다. 자발적으로 제작자와 URL을 보존한다.
사이트 전체의 이용조건을 개별 음원의 라이선스로 추정하지 않았다.

| 원본 / 제작자 | 원본 URL | 가공·용도 |
| --- | --- | --- |
| Wood Hit / alegemaate (Allan Legemaate) | https://freesound.org/people/alegemaate/sounds/364691/ | 나무 충돌, 판정의 명료한 어택, UI, 송판의 몸통, 결과 알림 |
| Punch 2_4 / Joao_Janz | https://freesound.org/people/Joao_Janz/sounds/483991/ | Perfect/Good/목인 1번의 몸통 |
| Soft Punch 1_1 / Joao_Janz | https://freesound.org/people/Joao_Janz/sounds/483990/ | Perfect/Good/목인 2번의 몸통 |
| Soft Punch 1_3 / Joao_Janz | https://freesound.org/people/Joao_Janz/sounds/483996/ | Perfect/Good/목인 3번의 몸통 |
| Short Swooshes2.wav / CapsLok | https://freesound.org/people/CapsLok/sounds/181906/ | 실제 나무 막대 스윙의 서로 다른 3테이크. 입력·주먹·전수 시범·방 이동 |
| swoosh_bamboo_03 / Evan_McLoud | https://freesound.org/people/Evan_McLoud/sounds/850166/ | 실제 막대 스윙을 낮춰 발차기의 무게감 표현 |
| wood breaking.aiff / SoundCollectah | https://freesound.org/people/SoundCollectah/sounds/109359/ | 실제 마른 나뭇가지 균열 2조각을 나무 타격에 레이어. 매우 큰 마지막 파열 테이크는 제외. 실제 송판 녹음이라는 주장은 하지 않음 |
| Wood Creak Single V1 / Rudmer_Rotteveel | https://freesound.org/people/Rudmer_Rotteveel/sounds/502505/ | 나무 바닥 삐걱임을 목인 축의 마찰·문 이동·실패의 짧은 잔향으로 가공 |
| Footsteps on hollow wood / florianreichelt | https://freesound.org/people/florianreichelt/sounds/459970/ | 서로 다른 발걸음 3개, 저역 정리·레벨 조정 |
| Soft Wind / florianreichelt | https://freesound.org/people/florianreichelt/sounds/459977/ | 12초 구간을 2초 겹침으로 10초 순환. 저역 바람 소음과 고역을 정리 |

모두 CC0-1.0. 다운로드는 공개 **HQ MP3 미리듣기 파일**이며 로그인 다운로드의 무손실 원본이 아니다.
원본 녹음의 장비/비트 심도를 가공본 품질로 주장하지 않는다. WAV로 바꿔도 MP3의 손실은 복구되지 않는다.
획득 파일은 `Tools/AudioSources/*.mp3`, 음원별 URL·다운로드 URL·취득일·SHA-256은
동명의 JSON에 보존했다. 원본은 Assets 밖에 있어 플레이어 빌드에 포함되지 않는다.
`outputs.json`은 모든 가공본의 원본 키·길이·레벨·해시를 기록한다.

## 미채택·확보하지 못한 항목

- 자연스러운 짧은 무협 기합: [Nox_Sound의 CC0 공격 음성](https://freesound.org/people/Nox_Sound/sounds/474651/)
  후보를 보관했지만 이 게임에 맞는 기합으로 확신할 수 없어 미채택했다. 게임에 음성 클립을 연결하지 않았다.
- [paper-slide / brokenmachinery](https://freesound.org/people/brokenmachinery/sounds/730078/)와
  [singing bowl / s-light](https://freesound.org/people/s-light/sounds/411484/)는 개별 CC0 표시는 확인했으나
  다운로드가 HTTP 403으로 실패하여 사용하지 않았다. 붓의 실제 마찰음이나 종소리가 확보되었다고 주장하지 않는다.
  방 이동에는 실제 녹음 스윙, 결과에는 나무 알림을 사용한다.
- 실제 송판 전용 녹음·미닫이문·목인 회전 전용 녹음은 확보하지 않았다. 위 표의 실제 나무 녹음을
  명시적으로 Foley로 가공했다. 유튜브·다른 게임에서 추출한 오디오는 없다.
- AI 대안으로 [ElevenLabs Sound Effects 약관 Exhibit 4](https://elevenlabs.io/creative-terms)를 검토했다
  (2026-11-14 확인). 게임에 결합하는 사용과 달리 독립 오디오 파일·샘플 배포는 제한되고,
  상업 이용도 허용 플랜을 전제로 한다. 이번의 원본 보존·재배포 가능한 CC0 자산 정책에 맞춰
  채택하지 않았다. AI 서비스 약관에 의존하는 에셋은 없다.

## 레벨·타이밍·수명

`DojoSoundPlayer`는 씬 빌더가 만든 충돌 3개 / 동작 2개 / 세부음 1개와 환경음 1개를 사용한다.
`PlayOneShot`의 숨은 동시 음성은 사용하지 않는다. 충돌과 스윙 슬롯을 분리해 연타 중 입력음을 보호한다.
이 제한은 새 플레이어의 풀에 적용된다. 기존 음악·공통 클릭·입장/송판 전환 소스는 별도이며,
모드 화면의 최대 구성은 이 풀 6개 + 환경음 1개 + 음악·클릭·메뉴 전환 각 1개이다.
새 타격은 비어 있는 슬롯을 쓰고, 모두 차면 가장 오래된 잔향을 교체한다. 28ms 이내 어택은 보호한다.
겹치는 개수에 따라 새 소리의 레벨을 낮춘다. 각 소스의 피치는 독립적이고 이미 재생 중인 소리의 피치를 바꾸지 않는다.
변형은 직전 테이크를 피하고 ±2.5% 피치 / 0.94–1.0 볼륨 변화를 사용한다.
게임 전역 `UnityEngine.Random`을 소비하지 않아 기존 애니메이션·게임의 무작위 순서에 영향을 주지 않는다.

입력과 판정 콜백은 기존 순서를 유지하며 오디오를 위해 기다리지 않는다. Gameplay 입력은 0.65 강도의 스윙,
Perfect/Good은 각각 0.30/0.24 기본 게인이다. 목인 단타는 0.40, 발소리는 0.13 이하이다.
기존 masterVolume과 gameplayVolume이 적용된다. 환경음 기본 게인은 일반 방 0.026, 산정 0.055이다.
일시정지·리셋·비활성화·포커스 상실 시 고정 소스를 정리한다.

원본의 무음 앞부분·불필요한 저역을 정리하고 어택을 보존했다. 가공본은 32kHz mono 16-bit PCM이다.
길이는 스윙 약 0.14–0.22초, 충돌 약 0.32초, 삐걱임 약 0.77초, 문 약 1.06초,
바람 순환 10초 등 내용별로 다르다. 모든 효과음을 같은 길이로 자르지 않는다.
현재 가공본 29개의 PCM 합은 1,440,374 bytes(약 1.37MiB)이며, 모두 48kHz float mono로 풀어도 약 4.12MiB이다.
실제 씬에서 안 쓰는 호환 경로가 일부 포함된 보수적인 전체 합이다. 브라우저/Unity 내부의 추가 복사 비용은 별도다.
Editor에서 뱅크가 참조하는 고유 클립 21개를 실제 로드해 측정한 float PCM 합은
2,140,612 bytes(32kHz, mono)이다. 이것은 WebGL 메모리를 직접 측정한 값이 아니다.

## 재생성·WebGL

```
python Tools/prepare_recorded_audio.py
```

소스가 없을 때만 `--fetch`를 사용할 수 있다. 기존 파일의 SHA-256과 CC0 메타데이터가 다르면 중단한다.
오래된 `generate_dojo_audio.py`, `generate_strike_audio.py` 명령도 이 파이프라인을 호출하므로
개선된 녹음 파일을 예전 합성음으로 덮어쓰지 않는다.
Unity Refresh 후 해당 씬 빌더(Title, Mode Selection, Song Selection, Gameplay, Results)를 실행한다.
커서 프리팹은 `DojoCursorAssetBuilder`가 유지한다. 필요한 참조가 없으면 명시적으로 검증에 실패한다.

짧은 효과음과 환경음은 Preload / Decompress On Load를 사용한다. 입력 경로에서 파일 읽기·디코딩·
AddComponent·AudioClip.Create·리버브 DSP를 수행하지 않는다. 외부 IR/플러그인이나 WebGL에서 지원하지 않는 DSP를 요구하지 않는다.
[Unity 6000.3 Web 오디오 문서](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/develop/audio)에
따라 Web 빌드에서는 AAC 인코딩과 브라우저의 디코딩·샘플레이트 변환이 개입한다.
브라우저 자동재생 제한은 기존 Title의 사용자 클릭/키 입력 흐름을 통해 해제하며 강제로 우회하지 않는다.
브라우저에서의 실제 소리 시작 시간은 오디오 장치·프레임·브라우저 버퍼의 영향을 받으므로 소프트웨어 검사로
물리적인 입력-스피커 지연을 보장하지 않는다. iOS 무음 스위치·Safari 등 별도 기기는 따로 확인해야 한다.

검증 결과와 실제 재생 범위는 [검증 기록](../Logs/recorded-audio-verification.md)에 기록한다.

2026-11-14 검증: 관련 PlayMode·Smoke·실제 AudioListener 출력 확인. 60초 연타는 최초 두 번
실패 후 진단 추가 재검사에서 통과했으며 실패 원인은 확정하지 않았다. WebGL 최종 빌드는 성공
(오류 0, 오디오 외 경고 5). Chrome의 기본 배포 페이지에서 타이틀 → 목인 → 게임 → 결과를
실행하고 Perfect/Good 출력과 연타 중 추가 디코딩 없음, 클리핑 없는 출력을 확인했다.
전체 배포 크기는 기존 리소스를 포함해 약 85.97MiB다. Safari/모바일·물리 지연·사람의 최종 청감
검수까지 완료한 것으로 해석하지 않는다. [청취 페이지](../Logs/AudioReview/index.html)에
개별 가공본과 실제 브라우저 믹스를 함께 제공한다.
