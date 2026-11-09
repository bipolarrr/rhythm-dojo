# UI 글꼴

리듬도장의 UI 기본 글꼴은 네이버 마루부리이다. `UiFontSettings` 리소스가 세 굵기의 OTF 에셋을 참조한다. 실제 굵기별 파일을 사용하므로 Unity Text의 Font Style은 Normal로 설정한다.

| `UiTextStyle.role` | 글꼴 | 용도 |
| --- | --- | --- |
| `Title` | Bold | 화면 제목, 타이틀 메뉴, 주요 플레이 버튼, 콤보와 주요 점수 |
| `Body` | SemiBold | 난이도, 설정 항목, 레인 키와 일반 UI |
| `Help` | Regular | 도움말, 단축키 설명, 입력 자리표시자 |

`UiElements.Label`과 `Button`의 `role` 인자 기본값은 `Body`이다. 빌더와 런타임 생성부가 제목·도움말 역할을 명시한다. 객체 이름이나 부모 이름으로 역할을 추론하지 않는다. `UiFontSettings.Apply`와 `ApplyTo`는 개별 Text와 비활성 자식을 포함한 트리에 같은 규칙을 적용한다.

`UiTextStyle.customFont`의 개별 설정과 `UiFontScope`의 커스텀 글꼴을 우선 적용한다. 공통 적용 이후 직접 지정한 개별 글꼴도 보존한다. 에디터의 NotoSansKR/Normal 설정은 유지한다. 공통 글꼴을 참조하는 scope에서는 각 Text의 역할로 굵기를 결정한다.

`UiFontScope.useSmoothRendering`을 활성화하면 공통 픽셀 머티리얼만 기본 머티리얼로 복원한다. 별도 커스텀 머티리얼은 보존한다. scope 설정 전후의 반복 적용과 pixel↔smooth 전환에 같은 규칙을 사용한다. 빌더 변경 후 **Build All Scenes**로 저장 씬과 SettingsPopup을 함께 재생성한다.

마루부리에 없는 기호는 기존 Mona12 Text KR 대체 글꼴에서 표시한다. 타이틀 로고처럼 글자가 그림에 포함된 에셋은 이미지로 표시한다. 배포 조건과 출처는 `Assets/Game/Resources/UI/MaruBuri-OFL.txt`를 참조한다.

EOD
