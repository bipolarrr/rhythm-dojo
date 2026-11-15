# Results 시청각 공개 시퀀스 — emergency, 2026-11-15

기존 기록지·앨범·점수·판정 배치를 유지했다. `main`의 Results 및 조립 패턴을 확인하고,
현재 `emergency`의 작업 중 변경 위에 Results 범위만 수정했다.

## 실제 동작

- `ScreenCompositionRoot` → `ResultsScreen.Initialize/Display`가 결과를 바인딩한 뒤
  `ResultsPresentation.Begin`을 실행한다. 미리보기도 같은 Display 경로를 사용한다.
- 배경·기록지 → 붓결 제목/판정 → 0.55초부터 점수 카운트업 → 순차 통계/정확도/콤보 →
  1.58초 낙관·최종 점수·신기록 강조 → 2.02초 메뉴 공개 완료.
- 기존 글꼴의 실제 글리프를 가는 붓결 띠로 공개한다. 씬 로딩의 긴 프레임은 연출 시각에
  최대 50ms만 반영하며, 모든 시청각 큐는 같은 연출 시각을 따른다.
- 카운트업은 `PlayResult.Score`의 복사값만 사용한다. 점수/판정/기록/추천 보정은 수정하지 않는다.
- Results 전용 빌더에서 ‘곡 선택’ 버튼 생성과 키보드 선택 연결을 제거했다.
  기존 재시도·상세 판정·조건부 박자 보정을 유지한다.
- Esc는 기존 `AppFlowController.ReturnFromGameplay`로 복귀한다. 일반 플레이는 원래 수련방,
  에디터 플레이테스트는 원래 에디터 세션 경로를 그대로 사용한다.
- Enter/Space/클릭은 연출 중 즉시 최종 상태를 공개한다. 입력을 놓고 다음 프레임이 지난 뒤
  메뉴가 활성화되어 같은 입력이 재시도나 상세 화면 열기로 이어지지 않는다.
- Results 오브젝트의 재활성화는 처음부터 공개한다. 박자 보정 모달이 컨트롤러만 일시 중지했다
  복원하는 경우에는 재생하지 않고 메뉴 입력을 그대로 복원한다.
- 성공/실패 낙관을 포함한 새로운 녹음 기반 WAV 8개, 고정 소스 3개를 씬에 직렬화했다.
  재생성 도구와 해시/레벨/원본 목록은 `Tools/generate_results_audio.py` 및
  `Tools/AudioSources/results-outputs.json`에 있다. 건너뛰기·비활성화·종료 때 음성을 정리한다.

## 증거

- [초반 붓결](results-reveal/01-brush.png), [카운트업](results-reveal/02-count.png), [통계 공개](results-reveal/03-statistics.png),
  [성공](results-reveal/04-complete.png), [실패](results-reveal/05-failed.png)
- 각 테스트의 CLI 시작/최종 JSON을 이 폴더에 보관한다. 0건으로 끝난 필터 시도는 통과로 계산하지 않는다.
- `output-peak.txt`는 실제 Play Mode의 Results 음성이 AudioListener 출력에 도달한 측정값이다.

## 검증 결과

Unity 6000.3.23f1의 실행 중인 Editor/Pipeline을 사용했다. 최종 컴파일 오류 없음.
각 항목의 최신 결과를 합친 고유 검사 **33개가 모두 통과**했으며, skip/inconclusive는 없다.
이는 전체 프로젝트 테스트 실행을 뜻하지 않는다. [최종 집계](results-reveal/final-results.json)에
원래 실행과 수정 후 재검증을 연결했다.

| 범위 | 결과 |
| --- | --- |
| Results 레이아웃·결과 상세/재시도 정책·미리보기 데이터 EditMode | 고유 13개 통과 |
| Results 및 PUPA 결과 흐름 PlayMode | 고유 16개 통과 |
| 기존 Smoke 프로필 | 5개 통과, 그중 Results 전체 흐름 1개는 위 범위와 중복 |

실제 곡 종료와 자동 Results 이동, 정지 실패와 실패 완주, 재시도 후 새 결과,
순차 공개·중간 점수·최종 점수 보존, 같은 화면 재표시/재활성화, 누르고 있는 Space의
건너뛰기/재시도 분리, 상세 판정, 보정 적용/모달 복귀, 에디터 플레이테스트 복귀를 확인했다.
긴 실제 음원 종료 검사는 최종 실행에서 134.02초 동안 정상 완료했다.
새 Foley는 실제 AudioListener 출력에서 peak **0.006424534**를 측정했다.

처음 발견한 보정 모달 복귀 시 재생/입력 차단 문제는 컨트롤러 중지와 화면 비활성화를
구분해 수정하고 해당 경로 및 화면 재활성화 검사를 다시 통과했다. 긴 음원 검사의 첫 실패는
검증 도중 별도 Pipeline 진단 요청의 5초 시간 초과 오류 로그 때문이었으며, 진단 요청 없이
재실행하여 통과했다. EditMode의 SendMessage 호출 오류는 해당 검사에서 직접 메서드 호출로
정정한 뒤 통과했다. 초기 실패 JSON도 원인 추적을 위해 남겼다.

저장된 씬을 다시 열어 검증했다. 직렬화한 모션 대상 22개, Results 오디오 소스 3개,
기본 활성 버튼 Retry/More와 조건부 보정 버튼이 있으며 곡 선택 버튼은 없다.
테스트 종료 후 Play Mode가 stopped/Editor ready이고, native Keyboard/Mouse/XInput 장치가
모두 활성 상태이며 테스트용 가상 장치는 남지 않은 것을 확인했다.

Windows 플레이어 빌드는 수행하지 않았다. 이번 범위는 Results UI/음향이며 플랫폼·빌드 설정은
변경하지 않았다. 결과 화면 캡처는 위 링크에 보관했다.

