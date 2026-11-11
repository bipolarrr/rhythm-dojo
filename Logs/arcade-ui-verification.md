# 모드 선택 아케이드 UI — 2026-11-11

공통 사각 제목판·하단 정보판을 제거하고, 모드별 큰 붓글씨 구도와 공간 이동에 동기화한 제목/설명 모션을 적용했다. 기존 배경·캐릭터·모드 기능은 재사용한다. 고정 구성은 `DojoArcadeLayoutBuilder`와 재생성한 `Title.unity`에 저장된다. [작성·조정 안내](../Docs/dojo-visual-renewal.md).

## 같은 해상도의 변경 전·후

이번 작업 직전 상태와 최종 상태를 각각 Play Mode에서 1280×720으로 렌더했다. 세 모드 모두 동일한 Camera/Canvas 캡처 방식을 사용했다.

| 모드 | 변경 전 | 변경 후 | 확인 |
| --- | --- | --- | --- |
| 비급 전수 | [전](arcade-before/lesson.png) | [후](arcade-after/lesson.png) | 밝은 벽의 큰 먹색 제목, 별도 패널 없는 설명, 사부 안내와의 겹침 제거 |
| 자유 연습 | [전](arcade-before/practice.png) | [후](arcade-after/practice.png) | 왼쪽 두 줄 제목, 수련 동작과 오른쪽 전망 확보 |
| 실력 검정 | [전](arcade-before/summit.png) | [후](arcade-after/summit.png) | 하늘 여백의 강한 제목, 선인·수련생 얼굴 노출 |

- [실제 Game View 캡처](arcade-after/summit-game-view.png): Pipeline `capture_game_view`, source=screen, 1280×720. 비교용 렌더와 같은 구도를 확인했다.
- Full HD: [비급](arcade-after/lesson-1920.png), [자유](arcade-after/practice-1920.png), [검정](arcade-after/summit-1920.png).
- 1024×768: [비급](arcade-mode-0-4x3.png), [자유](arcade-mode-1-4x3.png), [검정](arcade-mode-2-4x3.png), [준비 중](arcade-mode-3-4x3.png).
- [2560×1080 자유 연습](arcade-after/practice-ultrawide.png).
- [전환 영상](arcade-motion.mp4): 1280×720, 무음, 약 34.7초. 실제 Play Mode에서 캡처한 288프레임의 저장 시각 간격을 반영한 가변 프레임 영상이다. 캡처 처리 속도 때문에 약 8fps이므로 실제 게임의 프레임률을 나타내지 않는다. [촬영 스크립트](ArcadeReview.cs), [프레임 간격](arcade-motion.ffconcat).

제목이 먼저 퇴장한 뒤 캐릭터와 공간이 이동하고, 도착 직전에 새 제목과 붓 획이 진입한다. 설명은 한 박자 뒤에 나타난다. 기존 0.82초 이동 안에 완료되며 별도 입력 대기를 추가하지 않았다. 실력 검정은 오버슈트와 진입 거리를 줄였다. 선택 시각 피드백과 기존 효과음도 연결했다. 글자는 전부 Unity Text이며 Dokdo/Maru Buri의 포함된 SIL OFL 라이선스를 확인했다.

## 검증

Unity 6000.3.23f1, 실행 중인 Editor/Pipeline, Unity Test Framework 1.6.0. 각 실행 후 정리를 기다리고 다음 범위를 실행했다.

| 범위 | 결과 | 시간 | 근거 |
| --- | --- | --- | --- |
| MainMenuTests / EditMode | 5 passed | 0.45초 | [결과](arcade-edit-main-tests.json) |
| 저장된 씬 글꼴 / EditMode | 6 passed | 0.30초 | [결과](arcade-typography-final-tests.json) |
| MainMenuFlowTests 전체 / PlayMode | 22 passed, 1 failed | 86.96초 | [결과](arcade-main-final-tests.json) |
| 위 실패 사례 수정 후 재검증: 채보 공방 진입 | 1 passed | 6.59초 | [결과](arcade-editor-final-tests.json) |
| 재입장·전환 취소·즉시 수련 시작 경계 재검증 | 1 passed | 11.26초 | [결과](arcade-cancel-final-tests.json) |
| Smoke / PlayMode | 4 passed | 8.76초 | [요약](arcade-smoke.txt), [XML](arcade-smoke-playmode.xml) |

관련 범위의 실패는 수정 후 재검증했다. 이는 한 번의 최종 전체 테스트 통과가 아니라, 전체 메뉴 실행과 필요한 후속 단건 재검증을 합친 근거다. 중복 사례 수를 합산하지 않는다. 최종 통과 실행에는 skip/inconclusive가 없다. 전체 프로젝트 테스트, Hardware/Endurance 및 Windows 빌드는 실행하지 않았다.

메뉴의 실제 클릭·키보드 이동, 제목 퇴장과 설명 지연, 전환 중 중복 입력 방지, 취소 후 같은 인스턴스와 배치 복원, 비급 수련 완료, 준비 중 모드의 진입 제한, 설정/종료/채보 공방/곡 선택 연결을 검사했다. 기존 화면 캡처 검사에서 네 모드를 순환하며 제목·설명·선택 버튼이 화면 안에 있는지와 설명이 잘리지 않는지를 검증했다. 씬 구성과 글꼴 검사도 통과했다.

작업 중 발견한 붓 획 끝점의 NaN 메시 범위는 삼각함수 오차를 제한해 수정했다. 글꼴 및 공방 버튼의 직계 부모를 가정하던 기존 검사는 새 디자인 계약에 맞췄다. 재입장 직후 비급 수련을 시작할 때 설명 애니메이션이 중간에 멈출 수 있는 경계도 수정했다. 필터가 일치하지 않아 0개로 끝난 실행, 컴파일 수정 중 중단된 실행은 통과 근거에서 제외했다.

최종 촬영 구간의 새 런타임/컴파일 오류는 없었다([Console](arcade-console-final.json)). 기존 커서 텍스처 경고는 남아 있다. 편집한 코드의 `git diff --check`를 통과했다. 새 컴포넌트의 Unity meta를 보존하고, 촬영용 임시 에셋은 원본/meta를 삭제하지 않고 `Logs/arcade-capture-source`로 보관했다.

`main`에서 작업했으며 기존 다른 화면·설정·에디터의 미커밋 수정은 유지했다. Editor는 Play Mode를 종료한 편집 상태다. 커밋·푸시는 실행하지 않았다.

## 후속 수정: 실제 Enter·마우스 입력 불가

사용자 재현 후 Game View에 초점이 있고 메뉴 입력 액션도 활성인 상태에서, 실제 Keyboard/Mouse의 `enabled`가 false이고 테스트용 가상 장치 3개가 남은 것을 확인했다([수정 전 상태](entrance-input-before.json)). 앞선 가상 입력 검증만으로는 테스트 종료 후 물리 장치가 비활성화된 상태를 발견하지 못했다.

현재 장치를 복원하고 남은 테스트 장치를 정리했다. `NativeInputScope`는 생성한 가상 장치와 변경한 물리 장치·입력 설정을 추적하고, 정상 Dispose뿐 아니라 Play Mode 강제 종료와 스크립트 도메인 재로드에서도 복원한다. 재로드 시 Input System이 이미 저장한 비활성 상태까지 복원하도록 Editor SessionState에 소유한 변경만 기록한다. 기존에 비활성인 장치나 테스트 전에 있던 장치는 임의로 활성화하거나 제거하지 않는다. Gameplay 테스트도 입력 설정을 바꾸기 전에 복원 상태를 저장한다.

검증: 새 입력 정리 회귀 검사 1개 통과([결과](entrance-input-scope-tests.json)), Smoke 4개 통과([요약](entrance-input-smoke.txt), [XML](entrance-input-smoke.xml)). 실제 도메인 재로드 및 Play Mode 강제 종료를 재현한 뒤 물리 장치 2개만 활성 상태로 복구되고 입력 설정이 원래 값으로 돌아오는 것을 확인했다([재로드 후](entrance-input-after-reload.json), [강제 종료 후](entrance-input-after-stop.json)). Windows 네이티브 입력을 사용해 Enter 입장, 실제 마우스 클릭 입장, Esc 복귀를 확인했다. 이 확인에서는 Button 콜백이나 가상 InputSystem 장치를 사용하지 않았다([Enter](entrance-input-native-enter.json), [클릭](entrance-input-native-click.json), [Esc](entrance-input-native-escape.json)). 전체 테스트와 빌드는 실행하지 않았다.
