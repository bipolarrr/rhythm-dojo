# UI 항목별 로컬 브랜치

새 브랜치는 기존 명명 방식인 `ui/yunjang-작업대상`을 따른다. 기존 `ui/yunjang-titlescene`, `ui/yunjang-settingscene`을 덮어쓰지 않도록 새 화면 작업에는 `-v2`를 붙였다.

공통 글꼴·스타일과 화면 간 전환 코드가 연결되어 있으므로 다음 순서의 브랜치와 항목별 커밋으로 나눈다. 뒤 브랜치는 앞 브랜치의 변경을 포함한다.

| 순서 | 브랜치 | 항목 |
| --- | --- | --- |
| 1 | `ui/yunjang-fontcolor` | 공통 글꼴, 글꼴 범위, 나무 UI 표면·색상, 공통 아트 리소스 |
| 2 | `ui/yunjang-settingscene-v2` | 설정창, 키 셋팅·저장·HUD 반영, 일시정지 설정, 관련 오디오 복구 |
| 3 | `ui/yunjang-mousecursor` | 주먹 커서, 클릭 사운드·접촉 효과, 입력 통과 |
| 4 | `ui/yunjang-titlescene-v2` | 타이틀 문·로고·메뉴, 곡 선택 미리 로딩, 에디터 왕복 |
| 5 | `ui/yunjang-songselectionscene` | 곡 선택 배경·정보·기록, 모드 창, 정렬 선택 UI |
| 6 | `ui/yunjang-resultscene` | 결과창·상세 판정 UI, 전체 화면 글꼴 검사와 최종 점검 기록 |

새 브랜치의 기준은 `origin/main`이다. 그동안 main에 추가된 결과 시작·끝 판정 및 결과 미리보기 기능을 유지하면서 현재 UI를 통합했다. `AppFlowController`와 `UiTypographyTests`처럼 여러 항목에 걸친 파일은 변경 내용을 단계별로 나눴다.

기존 `main`, `ui-result-scene`, 이전 UI 브랜치는 이동하지 않는다. 커밋 후 작업 위치는 마지막 브랜치 `ui/yunjang-resultscene`이다. GitHub로 푸시하지 않는다.

기존 `Logs/grid-timeline.png`, `Logs/grid-timeline-advanced.png`, `Logs/grid-timeline-dropdown-small.png` 변경은 이번 UI 커밋에서 제외하고 작업 폴더에 남긴다. 별도 캡처와 플레이어 빌드는 수행하지 않는다.

최종 검증 범위와 결과는 `Logs/precommit-health-verification.md`에 기록한다. 원래 작업 파일과 스테이징 상태의 백업은 Git에서 제외되는 `Logs/branch-split-backup-*`에 보관한다.
