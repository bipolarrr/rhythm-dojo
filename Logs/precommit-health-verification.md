# 항목별 커밋 전 프로젝트 점검 — 2026-11-02

최신 `origin/main` (aacf4d4)의 결과 시작·끝 판정과 결과 미리보기를 현재 UI 변경과 통합한 상태를 점검했다. 플레이어 빌드와 별도 캡처는 실행하지 않았다. 검증은 원본 작업 파일과 분리한 프로젝트 복사본에서 수행했다.

| 범위 | 최종 결과 |
| --- | --- |
| Domain / EditMode | 244 통과 / 0 실패 / 0 건너뜀 |
| PlayMode | 123 통과 / 0 실패 / 0 건너뜀 |
| 저장 씬 | 7개, 누락 스크립트 없음 |
| 프리팹 | 3개, 누락 스크립트 없음 |
| Assets GUID | 중복 없음 |
| 직접 화면 캡처 검사 | 사용자 지시에 따라 1개 제외 |
| 캡처 / 플레이어 빌드 / 푸시 | 실행하지 않음 |

첫 점검에서 예제 데이터를 검증 복사본에 누락한 2건과 구형 글꼴을 기대한 타이틀 검사 1건을 확인했다. 예제 데이터를 보완하고 실제 메뉴 글꼴을 검사하도록 수정했다. 이후 최신 main을 반영해 Domain/EditMode 전체를 재실행했다.

전환 테스트의 실패는 미리 로딩된 곡 선택을 실제 전환 완료로 오인한 대기 조건에서 재현됐다. 활성 씬과 Bootstrap 로드 완료를 기다리도록 고쳤다. 문 연출의 복제 장식은 raycast를 받지 않게 하고 연출 종료 시 입력 차단과 GraphicRaycaster를 명시적으로 해제했다. 관련 타이틀·클릭 테스트 22개를 통과한 뒤 전체 PlayMode를 다시 실행했다.

최신 main과 겹친 결과 레이아웃·상세 보기·관련 테스트 4곳을 조정했다. 나무 UI와 확대 글씨를 유지하면서 시작·끝 판정 기능을 적용했고, 기존 View 필드를 보존했다. 설정 도움말과 확대 선택 박스가 겹치지 않도록 설정창의 세로 여백도 확보했다.

문 전환 프로파일의 목표 FPS는 60이며, 문이 움직이는 동안 최대 프레임 시간은 17.856 ms였다. 곡 선택 로딩 시 문 열림 진행률은 0.00000으로, 로딩이 닫힌 문 뒤에서 완료됐다. 이는 해당 검증 환경에서 측정한 전환 구간의 수치다.

검증에는 타이틀·곡 선택·설정·키 변경/저장/복원·일시정지·입력/판정·실제 오디오·PUPA 재생과 결과 전환·결과 상세 보기·에디터 동작이 포함됐다. `RHYTHM_DOJO_SKIP_CAPTURE=1`을 적용하고 직접 픽셀을 캡처하는 `GameplayWindowRendersJudgmentLineBelowCenter`만 실행 목록에서 제외했다.

결과 파일은 `precommit-integrated-editmode.xml`, `precommit-integrated-playmode.xml`, `precommit-assets-audit.json`이다. 이전 실패와 재검증 결과는 `precommit-health-*`, `precommit-click-rerun.xml`, `precommit-entrance-fix.xml`, `precommit-entrance-ready.xml`에 별도로 남긴다. 첫 시도부터 모두 통과한 것으로 기록하지 않는다.

커밋은 `Docs/ui-branch-commits.md`의 여섯 항목별 로컬 브랜치로 나눈다. 원래 스테이징과 작업 파일은 `Logs/branch-split-backup-*`에 백업했고, 기존 로그 PNG 3개의 수정은 커밋에서 제외한다.

## 후속 요청: 결과창과 더보기 글꼴

전체 검증 이후 사용자 요청에 따라 결과창 전체와 숨겨진 더보기/드롭다운을 곡 선택창의 Dokdo 글꼴·굵기로 통일했다. Results 루트에 글꼴 범위를 지정해 런타임 생성 글씨도 같은 글꼴을 유지한다. 기존 UI 색상과 판정 구분 색은 보존했다.

수정 범위에 대해 편집기 글꼴·레이아웃·상세 보기·미리보기 검사 15개, 결과창 실제 동작 검사 8개가 통과했다. 추가 캡처나 플레이어 빌드는 하지 않았다. 결과는 `results-brush-font-editmode.xml`, `results-brush-font-playmode.xml`이다.

## 후속 요청: 남은 UI 전체 Dokdo 통일

런타임 기본 글꼴 리소스와 빌더 기본 경로를 Dokdo로 변경했다. 저장된 7개 씬과 3개 프리팹을 순회해 숨겨진 항목을 포함한 Text 429개의 글꼴 참조를 확인했고, 플레이 HUD의 별도 font 필드와 UiFontScope도 함께 맞췄다. 타이틀 안내, 채보 에디터, 입력칸 안내와 런타임 생성 글씨도 대상에 포함했다. Mono 폰트는 누락 글리프용 fallback 참조로만 유지하며, fallback이 Dokdo 자신을 참조하지 않도록 분리했다.

후속 편집기 검사 33개와 관련 플레이 검사 53개가 모두 통과했다. 기존 전체 검증 367개를 다시 실행한 것으로 합산하지 않는다. 결과는 `global-dokdo-editmode.xml`, `global-dokdo-playmode.xml`, 글꼴 집계는 `global-dokdo-audit.txt`에 있다. 캡처·플레이어 빌드·푸시는 실행하지 않았다.
