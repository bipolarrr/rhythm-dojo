# 도장 Phase 1 도트 아트 재구성 — 2026-11-18

`emergency`의 첫 번째 도장 페이즈를 새로 그린 픽셀 아트 레이어 중심으로 교체했다. 반복 목재 텍스처를 붙인 건축 메시 대신, 먹색 전경·큰 문틀·산수화 문짝·청록빛 산수 원경과 독립 천/종이등을 사용한다. 판정 기반 진행도, 문 개방 감쇠, 접근/탈출 시점, 음악 시간에 따른 페이즈 전환은 기존 구현을 유지한다.

## 실제 기준 이미지와 아트 분석

작업 전에 다음 원본과 프로젝트의 실제 사용 경로를 확인했다.

- Title: `TitleSkyLayoutBuilder`가 사용하는 [LandscapeSource](../Assets/Game/Art/TitleSky/LandscapeSource.png), 문 프레임 원본 [HanokEntrance](../Assets/Game/Art/DojoRenewal/HanokEntrance.png).
- ModeSelection: [PracticeHall](../Assets/Game/Art/DojoRenewal/PracticeHall.png), 실제 자유 연습 방 캡처 `Logs/arcade-mode-1.png`.
- SongSelection: 동일한 방의 크롭/접근 구도와 [실제 곡 목록 캡처](../Docs/verification-images/hover-library.png). 오래된 검은 목록 UI 캡처는 현행 스타일 기준에서 제외했다.
- 수정 전 도장: [실제 Unity 렌더](../Docs/verification-images/pixel-dojo-before.png).
- 로컬 `main`의 `PixelGameplayStyle`, `DojoGameplayAssets`, `DojoEntranceView`도 읽고 에디터에서 조립하고 런타임에서 포즈만 변경하는 패턴을 따랐다. 새 도장 클래스는 `main`에 아직 없다.

| 관찰 항목 | 기존 에셋의 실제 특징 | 이번 적용 |
| --- | --- | --- |
| 픽셀 크기/밀도 | PracticeHall은 1672×941 고해상도 래스터이며 작은 계단형 경계와 가는 하이라이트가 모여 형태를 만든다. 엄밀한 16색/32색 이미지나 거대한 8비트 블록이 아니다. | Chamber와 Valley도 1672×941. 문/소품은 동일 스타일의 1263×1246 아틀라스에서 추출. 최종 표시 크기를 원화의 디테일과 대조했다. |
| 팔레트/대비 | 적갈색·구리색 목재, 크림색 종이/햇빛, 먹색·남색 그림자, 청록/청색 원경의 온도 대비. | 어두운 외곽, 밝은 종이 문짝, 문이 열리면 나타나는 차가운 산수 풍경. |
| 명암/질감 | 연속적인 재질 반사보다 큰 그림자 덩어리, 끊어진 나뭇결 하이라이트와 불규칙한 작은 색면. | 원화에 명암과 재질을 직접 그렸다. 법선광·PBR·자동 노이즈·픽셀화 후처리 없음. |
| 먹/붓/도트 | 메뉴 글씨 뒤 거친 먹판, 원형 붓자국, 얇고 불연속적인 질감. | 천의 붓 원형, 문짝의 산수 먹그림, 전경의 어두운 실루엣. 기존 HUD의 먹판은 그대로. |
| 형태/실루엣 | 굵고 비정형인 목재 윤곽과 겹친 지붕, 소나무 실루엣으로 공간을 잡음. | 큰 중앙 문과 휘어진 대들보, 최소한의 매달린 소품. 무기대·매트·의자 등 기존 사실적 소품은 제거. |
| 원근/깊이 | 밝기/채도 차이가 있는 산의 중첩, 바닥의 수렴선, 전경 프레임, 레이어 이동. | 전경 5.2, 등 5.8, 천 7.5, 문틀 8, 문 8.035, 원경 46의 깊이. 기존 전진 곡선으로 서로 다른 시차를 만듦. |

## 구현 및 에셋

신규 원본은 built-in `image_gen`으로 제작했다. [전체 프롬프트와 참조](../Docs/pixel-dojo-art-prompts.json)를 남겼다. CLI/API 생성이나 단순 도형/절차적 질감으로 원화를 대체하지 않았다.

- [Chamber.png](../Assets/Game/Art/DojoRoom/Pixel/Chamber.png): 문 안쪽이 투명한 도장 프레임과 바닥. 초안의 흰 안개는 생성 도구로 제거했다.
- [MovingArt.png](../Assets/Game/Art/DojoRoom/Pixel/MovingArt.png): 독립 좌우 문짝, 천, 종이등. 에디터가 네 개의 투명 PNG를 추출한다. 문 아래 틈을 만든 인접 매듭 픽셀은 추출 범위를 분리해 해결했다.
- [Valley.png](../Assets/Game/Art/DojoRoom/Pixel/Valley.png): 문밖 산수 원경. 첫 페이즈의 탈출 풍경이며 다른 페이즈를 제작한 것이 아니다.

[GameplayBackgroundBuilder](../Assets/Game/Scripts/Editor/UILayout/GameplayBackgroundBuilder.cs)가 배경 카메라와 22개 렌더러를 고정 조립한다. 모두 그림을 표시하는 평면/천 그리드이며, 기존 323개 배경 렌더러의 건축 구조는 더 이상 생성하지 않는다. 바닥도 반복 3D 마루 메시가 아닌 설계된 원화의 원근이다. 문은 문틀 뒤로 들어가고, 전경이 먼저 화면 밖으로 이동하며 문턱을 통과한다.

[DojoRoomPhaseView](../Assets/Game/Scripts/Runtime/UI/Views/DojoRoomPhaseView.cs)는 기존 진행 입력을 새 문짝 위치와 천/종이등의 독립 위상에 연결한다. 먼지 12개는 기존 고정 풀을 유지한다. 별도 실시간 광원 없이 [DojoPigment.shader](../Assets/Game/Art/DojoRoom/Pixel/DojoPigment.shader)가 원화 색과 개방에 따른 노출만 표현한다. 천만 저작된 그리드에서 미세하게 변형한다. 카메라 FOV 보정은 배경 전용이며 넓은 화면에서 카드 바깥이 보이지 않게 한다.

모든 사용 텍스처는 Point, Clamp, mipmap 없음, 무압축, NPOT 크기 유지로 가져온다. 전체 화면을 저해상도로 렌더링하거나 렌더 결과에 도트 필터를 씌우지 않는다. 서로 다른 깊이 때문에 접근할수록 전경 픽셀이 커지는 것은 의도한 투영이며, 화면 전체의 픽셀 크기를 고정했다고 주장하지 않는다. 공유 재질과 원본은 런타임에서 복제/변환하지 않는다.

재생성: **Game Tools > Scenes > Build Gameplay**. 고정 구성은 저장된 [Gameplay.unity](../Assets/Game/Scenes/Gameplay.unity)에 들어 있다. 기존 메뉴 원본과 GUID는 변경하지 않았다. 이전 DojoRoom 원본은 비교/이력용으로 보존했지만 새 배경에서 참조하지 않는다.

## 렌더 증거

| 상태 | 파일 |
| --- | --- |
| 수정 전 3D 도장 | [before](../Docs/verification-images/pixel-dojo-before.png) |
| 새 도장, 닫힌 문 | [start](../Docs/verification-images/pixel-dojo-start.png) |
| 실제 정상 판정 후 중간 개방 | [middle](../Docs/verification-images/pixel-dojo-middle.png) |
| 기존 레인/HUD와 실제 게임 합성 | [gameplay](../Docs/verification-images/pixel-dojo-gameplay.png) |
| 성과 충족 후 문턱 통과 | [exit](../Docs/verification-images/pixel-dojo-exit.png) |
| 5초 문 개방/전진 영상 | [motion MP4](../Docs/verification-images/pixel-dojo-motion.mp4) |

정지 화면은 `DojoBackgroundFlowTests`의 실제 판정 이벤트 경로에서 촬영했다. 영상은 `GameplayBackgroundPreview.CaptureMotion()`의 960×540, 24fps, 120프레임 **가상 성과 미술 프리뷰**이며 실제 곡 플레이 영상은 아니다. `Logs/dojo-room/`에 미스, 전환 직전/중간/이후의 추가 게임/배경 PNG도 남겼다.

16:9, 16:10, 4:3, 21:9의 닫힘·중간 개방·탈출 배경을 별도 PreviewScene에서 렌더링했다. [화면비별 12개 PNG](pixel-dojo/aspects/)와 [캡처 스크립트](pixel-dojo/capture-aspects.cs)를 남겼다. 가로로 늘이지 않고 프레임을 크롭한다. 좁은 화면에서는 외곽 종이등 일부가 화면 밖으로 나가지만 중심 문과 원경은 유지된다. 이 화면비 검사는 배경 전용이며 전체 HUD의 모든 화면비를 새로 검증한 것은 아니다.

## 검증

Unity 6000.3.23f1 재컴파일 성공 후 도장 관련 PlayMode와 Smoke를 순차 실행해 모두 통과했다. 도장 검사는 정상 입력/미스/빈 입력, 소품 상시 운동, 일시정지/재개 대기, 재시작 시 동일 객체 유지, 문턱 통과, 성과와 독립적인 시간 전환, 기존 레인 카메라 보존을 포함한다. Smoke는 타이틀/선택/플레이/결과/복귀 경로를 검사했다. 저장된 Gameplay를 다시 열어 필수 참조 검증도 통과했다.

| 범위 | 결과 | 실행 시간 | 상세 |
| --- | --- | --- | --- |
| DojoBackgroundFlowTests | 1 passed, 0 failed/skipped/inconclusive | 6.44초 | [결과 JSON](pixel-dojo/room-tests.json) |
| Smoke 카테고리 | 5 passed, 0 failed/skipped/inconclusive | 37.52초 | [결과 JSON](pixel-dojo/smoke-tests.json) |

작업 직전 Gameplay YAML과 재생성한 YAML을 오브젝트 경로/컴포넌트 기준으로 정규화해 비교했다. **Dojo Room Spatial Stage 밖의 직렬화 객체 변경은 0개**였다. [비교 결과](pixel-dojo/composition-comparison.txt), [비교 스크립트](pixel-dojo/compare-composition.py). 이번 작업에서 Domain/Application, 판정, 음악, 레인/노트/HUD, Director의 진행 계산 코드는 수정하지 않았다. 새 에셋의 `.meta` 존재도 확인했다.

CLI eval의 기본 5초 제한과 Unity의 생성 씬 저장 대화상자로 일부 빌드/캡처 명령 응답이 시간 초과됐다. 생성 완료와 파일 갱신을 별도로 확인하고, 긴 캡처는 같은 설치된 Pipeline의 인증된 로컬 API에서 명시적 제한 시간으로 실행했다. 화면비 캡처의 첫 eval 스크립트는 namespace 구문 때문에 실패해 수정 후 12개 렌더를 완료했다. 이 중간 실패들을 테스트 성공에 포함하지 않았다.

Windows Player 빌드, 실제 PUPA 완주, Hardware/Endurance 전체, GPU/CPU 프레임 시간과 메모리 프로파일은 수행하지 않았다. 렌더러 수 감소를 실제 성능 측정으로 간주하지 않는다. 최종 화면의 미적 만족 여부는 링크한 실제 렌더로 판단할 수 있다.
