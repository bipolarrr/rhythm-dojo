# UI 글꼴

리듬도장의 UI 기본 글꼴은 네이버 마루부리입니다. `UiFontSettings` 리소스가 세 굵기의 OTF 에셋을 참조합니다. 실제 굵기별 파일을 사용하므로 Unity Text의 Font Style은 Normal로 설정합니다.

- Bold — 화면 제목, 타이틀 메뉴, 주요 플레이 버튼, 콤보와 주요 점수
- SemiBold — 곡명, 난이도, 설정 항목, 레인 키와 일반 UI
- Regular — 도움말, 단축키 설명, 입력 자리표시자

씬 빌더와 런타임 UI 팩터리는 `UiFontSettings.Apply`와 `ApplyTo`를 사용합니다. 현재 기본 글꼴을 참조하는 기존 씬 범위에도 같은 굵기 규칙을 적용합니다. 마루부리에 없는 기호는 기존 Mona12 Text KR 대체 글꼴에서 표시합니다.

타이틀 로고처럼 글자가 그림에 포함된 에셋은 이미지로 표시됩니다. 마루부리 배포 조건과 출처는 `Assets/Game/Resources/UI/MaruBuri-OFL.txt`에 있습니다.
