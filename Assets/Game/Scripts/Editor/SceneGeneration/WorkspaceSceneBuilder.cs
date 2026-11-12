using System;
using System.Collections.Generic;
using System.Linq;
using RhythmDojo.Authoring;
using RhythmDojo.UI;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor.Events;

namespace RhythmDojo.EditorTools
{
    public static class WorkspaceSceneBuilder
    {
        [MenuItem("Game Tools/Scenes/Build Extended Chart Editor")]
        public static void Build()
        {
            Positions.Clear();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before generating scenes.");
            }

            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // Build the shared foundation before applying the current editor interface.
            ChartEditorSceneBuilder.BuildBase();
            var scene = EditorSceneManager.OpenScene(ChartEditorSceneBuilder.ScenePath);

            var canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>()).Single();
            var screen = canvas.GetComponentInChildren<ChartEditorScreen>();
            var serializedScreen = new SerializedObject(screen);
            serializedScreen.FindProperty("initialSlot").intValue = (int)RhythmDojo.Application.ChartSlot.Easy;
            serializedScreen.ApplyModifiedPropertiesWithoutUndo();
            var workspace = canvas.gameObject.AddComponent<WorkspaceEditor>();
            var soundSource = canvas.gameObject.AddComponent<AudioSource>();
            soundSource.playOnAwake = false;
            SceneDependencyAssembler.Wire(workspace, "soundSource", soundSource);
            var view = canvas.gameObject.AddComponent<WorkspaceView>();
            workspace.view = view;
            workspace.screen = screen;
            workspace.menu = canvas.GetComponent<ChartEditorMenu>();
            workspace.timeline = screen.GetComponent<ChartAudioTimeline>();
            workspace.grid = canvas.GetComponentInChildren<ChartBeatGrid>();

            var menuBar = canvas.transform.Find("Menu Bar");
            menuBar.GetComponent<Image>().color = MenuSurface;
            ((RectTransform)menuBar).offsetMin = new Vector2(0, -26);
            var activeChart = canvas.GetComponentsInChildren<Text>(true).Single(text => text.name == "Active Chart");
            activeChart.color = MenuText;
            activeChart.fontSize = 14;
            activeChart.alignment = TextAnchor.MiddleRight;
            UiElements.Position(activeChart.rectTransform, new Vector2(-10, -2), new Vector2(210, 22));
            activeChart.rectTransform.anchorMin = activeChart.rectTransform.anchorMax = new Vector2(1, 1);
            activeChart.rectTransform.pivot = new Vector2(1, 1);

            foreach (string name in new[] { "파일", "설정", "도구", "테스트", "File Panel", "Settings Panel", "Tools Panel" })
            {
                var item = canvas.transform.Find(name);
                if (item)
                {
                    item.gameObject.SetActive(false);
                }
            }

            var bindings = new List<WorkspaceActionBinding>();
            var menus = new List<WorkspaceMenuPopup>();
            var dismissArea = Panel("Menu Dismiss Area", canvas.transform, Vector2.zero, Vector2.zero);
            dismissArea.anchorMin = Vector2.zero;
            dismissArea.anchorMax = Vector2.one;
            dismissArea.offsetMin = dismissArea.offsetMax = Vector2.zero;
            dismissArea.GetComponent<Image>().color = Color.clear;
            var dismissEvents = dismissArea.gameObject.AddComponent<EventTrigger>();
            var dismissEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            UnityEventTools.AddVoidPersistentListener(dismissEntry.callback, view.CloseMenus);
            dismissEvents.triggers.Add(dismissEntry);
            view.menuDismissArea = dismissArea.gameObject;
            dismissArea.gameObject.SetActive(false);
            var file = Menu(canvas.transform, view, "파일", 0, menus);
            Action(file, "새로 만들기", WorkspaceAction.New, bindings);
            view.openProjectId = Field(file, "프로젝트 ID", "전체 곡 ID 입력");
            Action(file, "프로젝트 ID로 불러오기", WorkspaceAction.OpenByProjectId, bindings);
            Heading(file, "최근 파일 · 최신 저장 순");
            view.recent = Dropdown(file, "Recent Projects", new[] { "저장된 파일" });
            Action(file, "파일 열기", WorkspaceAction.Open, bindings);
            Action(file, "전체 파일 목록 열기", WorkspaceAction.ShowProjectList, bindings);
            Action(file, "프로젝트 ID / 이전 형식 가져오기", WorkspaceAction.ProjectFiles, bindings);
            Action(file, "저장", WorkspaceAction.Save, bindings);
            Action(file, "다른 이름으로 저장", WorkspaceAction.SaveAs, bindings);
            Action(file, "PUPA 음원 + 채보 가져오기", WorkspaceAction.ImportPupa, bindings);
            Action(file, "종료 · 타이틀로", WorkspaceAction.Exit, bindings);

            var settings = Menu(canvas.transform, view, "설정", 1, menus);
            view.title = Field(settings, "곡 제목", "제목");
            view.artist = Field(settings, "작곡가", "작곡가 이름");
            view.jacketPath = Field(settings, "자켓 이미지 전체 경로", "PNG / JPG 경로 입력 후 Enter");
            Action(settings, "자켓 이미지 적용", WorkspaceAction.ImportJacket, bindings);
            Heading(settings, "난이도");
            var slots = new GameObject("Difficulty", typeof(RectTransform)).GetComponent<RectTransform>();
            slots.SetParent(settings, false);
            UiElements.Position(slots, new Vector2(4, Next(settings, 24)), new Vector2(478, 24));
            view.difficulty = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var button = canvas.transform.Find("Chart Slot " + i).GetComponent<Button>();
                view.difficulty[i] = button;
                button.transform.SetParent(slots, false);
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(i / 3f, 0);
                rect.anchorMax = new Vector2((i + 1) / 3f, 1);
                rect.offsetMin = new Vector2(1, 0);
                rect.offsetMax = new Vector2(-1, 0);
                button.GetComponentInChildren<Text>().text = new[] { "Easy", "Standard", "Hard" }[i];
                StyleMenuButton(button, TextAnchor.MiddleCenter, 14);
                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.offsetMin = new Vector2(4, 0);
                label.rectTransform.offsetMax = new Vector2(-4, 0);
            }
            Heading(settings, "오디오");
            view.audioPath = Field(settings, "음원 전체 경로", "WAV / OGG / MP3 경로 입력 후 Enter");
            Action(settings, "음원 가져오기", WorkspaceAction.ImportAudio, bindings);
            view.volume = Field(settings, "음원 볼륨 (%)", "65");
            view.volume.enabled = false;
            view.volume.textComponent.alignment = TextAnchor.MiddleCenter;
            view.volume.textComponent.raycastTarget = false;
            view.volume.GetComponent<Image>().color = new Color(.78f, .8f, .83f);
            var volumeTrack = Panel("Volume Track", view.volume.transform, Vector2.zero, Vector2.zero);
            volumeTrack.anchorMin = Vector2.zero;
            volumeTrack.anchorMax = Vector2.one;
            volumeTrack.offsetMin = volumeTrack.offsetMax = Vector2.zero;
            volumeTrack.SetAsFirstSibling();
            volumeTrack.GetComponent<Image>().color = new Color(.78f, .8f, .83f);
            var volumeFill = Panel("Volume Fill", volumeTrack, Vector2.zero, Vector2.zero);
            volumeFill.anchorMin = Vector2.zero;
            volumeFill.anchorMax = Vector2.one;
            volumeFill.offsetMin = volumeFill.offsetMax = Vector2.zero;
            volumeFill.SetAsFirstSibling();
            volumeFill.GetComponent<Image>().raycastTarget = false;
            view.volumeControl = volumeTrack.gameObject.AddComponent<Slider>();
            view.volumeControl.transition = Selectable.Transition.None;
            view.volumeControl.fillRect = volumeFill;
            view.volumeControl.direction = Slider.Direction.LeftToRight;
            view.volumeControl.minValue = 0;
            view.volumeControl.maxValue = 100;
            view.volumeControl.wholeNumbers = true;
            view.volumeControl.value = 65;
            view.bpm = Field(settings, "BPM", "120");
            Action(settings, "BPM 자동 측정", WorkspaceAction.EstimateBpm, bindings);
            view.bpmSuggestion = Heading(settings, "");
            Action(settings, "이 BPM으로 설정", WorkspaceAction.ApplyEstimatedBpm, bindings);
            view.applyEstimatedBpm = bindings.Last().button;
            view.applyEstimatedBpm.gameObject.SetActive(false);
            view.offset = Field(settings, "악곡 시작 타이밍 (ms)", "0");
            Action(settings, "설정 적용", WorkspaceAction.ApplySettings, bindings);
            Action(settings, "채보 종료 시간 / 상세 설정", WorkspaceAction.ChartSettings, bindings);
            var gridSettings = workspace.timeline.GetComponent<ChartAudioTimelineView>().gridSettingsControl;
            gridSettings.transform.SetParent(settings, false);
            UiElements.Position(gridSettings.GetComponent<RectTransform>(), new Vector2(4, Next(settings, 24)), new Vector2(478, 24));
            StyleMenuButton(gridSettings, TextAnchor.MiddleLeft, 14);

            var edit = Menu(canvas.transform, view, "편집", 2, menus);
            Action(edit, "실행 취소", WorkspaceAction.Undo, bindings);
            Action(edit, "다시 실행", WorkspaceAction.Redo, bindings);
            Action(edit, "복사", WorkspaceAction.Copy, bindings);
            Action(edit, "현재 커서에 붙여넣기", WorkspaceAction.Paste, bindings);
            Action(edit, "선택 범위 삭제", WorkspaceAction.Delete, bindings);
            Action(edit, "모두 선택", WorkspaceAction.SelectAll, bindings);
            Action(edit, "탭 노트 모두 선택", WorkspaceAction.SelectTaps, bindings);
            Action(edit, "롱노트 모두 선택", WorkspaceAction.SelectHolds, bindings);
            Action(edit, "선택 해제", WorkspaceAction.Deselect, bindings);
            Action(edit, "선택 노트 좌우 반전", WorkspaceAction.Mirror, bindings);
            Action(edit, "선택 노트 랜덤 배치", WorkspaceAction.Randomize, bindings);

            var tools = Menu(canvas.transform, view, "도구", 3, menus);
            Action(tools, "처음 위치 / 기본 확대", WorkspaceAction.ResetView, bindings);
            Action(tools, "선택", WorkspaceAction.Select, bindings);
            Action(tools, "그리기", WorkspaceAction.Pencil, bindings);
            Action(tools, "지우개", WorkspaceAction.Eraser, bindings);
            Action(tools, "잡기", WorkspaceAction.Position, bindings);
            Action(tools, "재생 위치", WorkspaceAction.Scroll, bindings);
            Action(tools, "ON AIR", WorkspaceAction.OnAir, bindings);
            view.position = Field(tools, "작업 위치 (초)", "0");
            BuildToolBar(canvas.transform, view);
            Heading(tools, "퀀타이즈 · 4분음표 기준");
            view.quantization = Dropdown(tools, "Quantization", new[]
            {
                "1/4", "1/8", "1/12", "1/16", "1/24", "1/32", "1/48", "1/64", "1/96", "1/192", "OFF"
            });
            view.quantization.value = 0;
            var timelineView = workspace.timeline.GetComponent<ChartAudioTimelineView>();
            timelineView.extendedQuantization = true;
            timelineView.noteSnapControl.ClearOptions();
            timelineView.noteSnapControl.AddOptions(view.quantization.options.Select(option => option.text).ToList());
            timelineView.noteSnapControl.value = 0;

            var play = Menu(canvas.transform, view, "재생", 4, menus);
            Action(play, "재생 / 일시정지", WorkspaceAction.PlayPause, bindings);
            Action(play, "처음으로", WorkspaceAction.Stop, bindings);
            Action(play, "테스트 플레이", WorkspaceAction.Test, bindings);
            Action(play, "현재 위치에서 테스트 플레이", WorkspaceAction.TestHere, bindings);
            Action(play, "테스트 플레이 (오토)", WorkspaceAction.AutoTest, bindings);
            Action(play, "현재 위치에서 테스트 플레이 (오토)", WorkspaceAction.AutoTestHere, bindings);

            view.status = canvas.GetComponentsInChildren<Text>(true).Single(text => text.name == "Progress Label");
            view.status.text = "";
            view.selection = UiElements.Label("Workspace Selection", canvas.transform,
                "선택 0", new Vector2(820, -265), new Vector2(400, 45), 18);
            var jacket = new GameObject("Workspace Jacket", typeof(RectTransform), typeof(RawImage));
            jacket.transform.SetParent(canvas.transform, false);
            view.jacket = jacket.GetComponent<RawImage>();
            view.jacket.raycastTarget = false;
            view.jacket.enabled = false;
            UiElements.Position(view.jacket.rectTransform, new Vector2(820, -70), new Vector2(150, 150));
            var help = canvas.GetComponentsInChildren<Text>(true).Single(text => text.name == "Help");
            help.text = "Space  재생 / 일시정지   ·   Ctrl+S  저장   ·   Esc  메뉴 닫기";
            help.fontSize = 13;
            help.alignment = TextAnchor.MiddleRight;
            help.transform.SetParent(canvas.transform, false);
            help.rectTransform.pivot = new Vector2(1, 0);
            help.rectTransform.anchorMin = help.rectTransform.anchorMax = new Vector2(1, 0);
            help.rectTransform.anchoredPosition = new Vector2(-20, 8);
            help.rectTransform.sizeDelta = new Vector2(620, 24);
            AddGameplayPreview(canvas, view);
            AddNoteSpeedWarning(canvas.transform, view, bindings);
            AddOnAirDialog(canvas.transform, view, bindings);
            AddProjectList(canvas.transform, view, bindings);

            // Confirmation dialogs stay above the editor and its live preview.
            var popup = Panel("Unsaved Changes", canvas.transform, new Vector2(0, 0), new Vector2(1280, 720));
            popup.anchorMin = Vector2.zero;
            popup.anchorMax = Vector2.one;
            popup.offsetMin = popup.offsetMax = Vector2.zero;
            view.exitPopup = popup.gameObject;
            UiElements.Label("Question", popup, "저장하지 않은 변경이 있습니다.",
                new Vector2(390, -240), new Vector2(560, 60), 28);
            PopupAction(popup, "저장 후 종료", WorkspaceAction.SaveExit, 300, bindings);
            PopupAction(popup, "저장하지 않고 종료", WorkspaceAction.DiscardExit, 550, bindings);
            PopupAction(popup, "취소", WorkspaceAction.CancelExit, 800, bindings);
            popup.gameObject.SetActive(false);

            var newPopup = Panel("New Project Confirmation", canvas.transform, Vector2.zero, new Vector2(1280, 720));
            newPopup.anchorMin = Vector2.zero;
            newPopup.anchorMax = Vector2.one;
            newPopup.offsetMin = newPopup.offsetMax = Vector2.zero;
            view.newPopup = newPopup.gameObject;
            UiElements.Label("Question", newPopup, "현재 파일을 저장하시겠습니까?",
                new Vector2(360, -240), new Vector2(560, 60), 28).alignment = TextAnchor.MiddleCenter;
            PopupAction(newPopup, "YES", WorkspaceAction.SaveNew, 390, bindings);
            PopupAction(newPopup, "NO", WorkspaceAction.DiscardNew, 660, bindings);
            newPopup.gameObject.SetActive(false);

            var saveAsPopup = Panel("Save As Dialog", canvas.transform, Vector2.zero, Vector2.zero);
            saveAsPopup.anchorMin = Vector2.zero;
            saveAsPopup.anchorMax = Vector2.one;
            saveAsPopup.offsetMin = saveAsPopup.offsetMax = Vector2.zero;
            view.saveAsPopup = saveAsPopup.gameObject;
            var dialog = Panel("Dialog", saveAsPopup, Vector2.zero, new Vector2(480, 220));
            dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(.5f, .5f);
            dialog.anchoredPosition = Vector2.zero;
            dialog.GetComponent<Image>().color = MenuSurface;
            UiElements.Label("Title", dialog, "다른 이름으로 저장", new Vector2(24, -16),
                new Vector2(432, 32), 20).color = MenuText;
            Positions[dialog] = 52;
            view.saveAsName = Field(dialog, "이름", "이름을 입력하세요");
            dialog.sizeDelta = new Vector2(480, 220);
            UiElements.Position(dialog.Find("Heading").GetComponent<RectTransform>(), new Vector2(24, -52), new Vector2(432, 22));
            UiElements.Position(view.saveAsName.GetComponent<RectTransform>(), new Vector2(24, -80), new Vector2(432, 30));
            view.saveAsError = UiElements.Label("Error", dialog, "", new Vector2(24, -114), new Vector2(432, 36), 14);
            view.saveAsError.color = new Color(.7f, .12f, .12f);
            var confirmSaveAs = UiElements.Button("Confirm Save As", dialog, "저장", new Vector2(24, -164), new Vector2(204, 32));
            StyleMenuButton(confirmSaveAs, TextAnchor.MiddleCenter, 14);
            bindings.Add(new WorkspaceActionBinding { action = WorkspaceAction.ConfirmSaveAs, button = confirmSaveAs });
            var cancelSaveAs = UiElements.Button("Cancel Save As", dialog, "취소", new Vector2(252, -164), new Vector2(204, 32));
            StyleMenuButton(cancelSaveAs, TextAnchor.MiddleCenter, 14);
            bindings.Add(new WorkspaceActionBinding { action = WorkspaceAction.CancelSaveAs, button = cancelSaveAs });
            saveAsPopup.gameObject.SetActive(false);
            view.actions = bindings.ToArray();
            view.menus = menus.ToArray();
            foreach (var menu in menus)
            {
                SizeMenu(menu);
            }

            UiFontSettings.ApplyTo(canvas.transform);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(s => s.path != WorkspaceEditor.ScenePath)
                .Concat(new[] { new EditorBuildSettingsScene(WorkspaceEditor.ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Rhythm Dojo: chart editor generated with the workspace interface.");
        }

        private static void AddProjectList(Transform parent, WorkspaceView view, List<WorkspaceActionBinding> bindings)
        {
            var overlay = Panel("Project List", parent, Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0, 0, 0, .55f);
            view.projectListPopup = overlay.gameObject;

            var dialog = Panel("Dialog", overlay, Vector2.zero, new Vector2(640, 520));
            dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(.5f, .5f);
            dialog.GetComponent<Image>().color = MenuSurface;
            UiElements.Label("Title", dialog, "전체 파일 목록", new Vector2(20, -14), new Vector2(520, 32), 22)
                .color = MenuText;
            UiElements.Label("Instructions", dialog, "더블 클릭하여 열기   ·   별표로 최근 파일 표시 설정",
                new Vector2(20, -49), new Vector2(600, 28), 15).color = MenuText;

            var viewport = Panel("Viewport", dialog, new Vector2(20, -86), new Vector2(600, 364));
            viewport.GetComponent<Image>().color = new Color(.85f, .86f, .88f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            view.projectListScroll = scroll;

            var row = UiElements.Button("Project Row Template", dialog, "", Vector2.zero, new Vector2(600, 46));
            StyleMenuButton(row, TextAnchor.MiddleLeft, 16);
            view.projectListRow = row.gameObject.AddComponent<WorkspaceProjectRow>();
            view.projectListRow.projectLabel = row.GetComponentInChildren<Text>();
            view.projectListRow.projectLabel.rectTransform.offsetMin = new Vector2(10, 14);
            view.projectListRow.projectLabel.rectTransform.offsetMax = new Vector2(-158, -2);
            view.projectListRow.savedAtLabel = UiElements.Label("Last Saved", row.transform, "",
                new Vector2(10, -24), new Vector2(432, 18), 13);
            view.projectListRow.savedAtLabel.color = new Color(.38f, .38f, .38f);

            var copyId = UiElements.Button("Copy Project ID", row.transform, "ID 복사", Vector2.zero, new Vector2(94, 46));
            StyleMenuButton(copyId, TextAnchor.MiddleCenter, 15);
            view.projectListRow.copyProjectId = copyId;
            var copyIdRect = (RectTransform)copyId.transform;
            copyIdRect.anchorMin = copyIdRect.anchorMax = copyIdRect.pivot = new Vector2(1, 1);
            copyIdRect.anchoredPosition = new Vector2(-48, 0);

            var star = UiElements.Button("Recent Visibility", row.transform, "★", Vector2.zero, new Vector2(48, 46));
            StyleMenuButton(star, TextAnchor.MiddleCenter, 22);
            view.projectListRow.recentToggle = star;
            var starRect = (RectTransform)star.transform;
            starRect.anchorMin = starRect.anchorMax = starRect.pivot = new Vector2(1, 1);
            starRect.anchoredPosition = Vector2.zero;
            row.gameObject.SetActive(false);

            view.projectListStatus = UiElements.Label("Status", dialog, "", new Vector2(20, -461),
                new Vector2(498, 42), 14);
            view.projectListStatus.color = MenuText;
            var close = UiElements.Button("CloseProjectList", dialog, "닫기",
                new Vector2(532, -466), new Vector2(88, 34));
            StyleMenuButton(close, TextAnchor.MiddleCenter, 16);
            bindings.Add(new WorkspaceActionBinding { action = WorkspaceAction.CloseProjectList, button = close });
            overlay.gameObject.SetActive(false);
        }

        public static void AddGameplayPreview(Canvas canvas, WorkspaceView view)
        {
            var panel = canvas.transform.Find("Editor Content/Chart And Preview/Preview Space");
            var help = panel.Find("Help");
            if (help)
            {
                help.gameObject.SetActive(false);
            }
            panel.Find("Next Chart").gameObject.SetActive(false);
            view.jacket.gameObject.SetActive(false);
            var title = UiElements.Label("Preview Heading", panel, "인게임 미리보기",
                new Vector2(12, -6), new Vector2(350, 22), 14);
            title.raycastTarget = false;
            var go = new GameObject("Gameplay Preview", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(panel, false);
            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = new Vector2(10, 42);
            image.rectTransform.offsetMax = new Vector2(-10, -32);
            var clock = UiElements.Label("Preview Clock", panel, "미리보기 · 0.000 s",
                Vector2.zero, new Vector2(350, 18), 11);
            clock.rectTransform.anchorMin = clock.rectTransform.anchorMax = Vector2.zero;
            clock.rectTransform.anchoredPosition = new Vector2(12, 40);
            clock.raycastTarget = false;
            view.selection.transform.SetParent(panel, false);
            view.selection.rectTransform.anchorMin = view.selection.rectTransform.anchorMax = Vector2.zero;
            view.selection.rectTransform.anchoredPosition = new Vector2(12, 21);
            view.selection.rectTransform.sizeDelta = new Vector2(350, 18);
            view.selection.fontSize = 11;
            view.selection.raycastTarget = false;

            var preview = go.AddComponent<ChartGameplayPreview>();
            preview.workspace = canvas.GetComponent<WorkspaceEditor>();
            preview.image = image;
            preview.clock = clock;
            view.noteSpeed = AddNoteSpeedField(panel);
            UnityEventTools.AddPersistentListener(view.noteSpeed.onEndEdit, preview.ApplyNoteSpeed);
            preview.presentation = SceneResources.Require<GameplayPresentationSettings>("Assets/Game/Settings/PresentationSettings.asset");
            preview.scrollSettings = SceneResources.Require<ScrollSettings>("Assets/Game/Settings/ScrollSettings.asset");
            preview.tapTemplate = SceneResources.Require<GameObject>("Assets/Game/Prefabs/TapNote.prefab").GetComponent<NoteView>();
            preview.holdTemplate = SceneResources.Require<GameObject>("Assets/Game/Prefabs/HoldNote.prefab").GetComponent<NoteView>();
            preview.playfield = new GameObject("Editor Preview World").AddComponent<PlayfieldPresenter>();
            var previewRig = new GameObject("Editor Preview Rig");
            SceneDependencyAssembler.Wire(preview, "previewRig", previewRig.transform);
            var previewCamera = new GameObject("Editor Preview Camera").AddComponent<Camera>();
            previewCamera.transform.SetParent(previewRig.transform, false);
            previewCamera.transform.position = new Vector3(0, 10, -10);
            previewCamera.transform.LookAt(new Vector3(0, 0, 9));
            previewCamera.fieldOfView = 52;
            previewCamera.farClipPlane = 180;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.025f, .035f, .065f);
            previewCamera.cullingMask = 1 << 31;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;
            previewCamera.depth = -50;
            previewCamera.enabled = false;
            SceneDependencyAssembler.Wire(preview, "previewCamera", previewCamera);
            SceneDependencyAssembler.Wire(preview, "displayCamera", Camera.main);
            var light = new GameObject("Editor Preview Light").AddComponent<Light>();
            light.transform.SetParent(previewRig.transform, false);
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(50, -25, 0);

            var serialized = new SerializedObject(preview.playfield);
            serialized.FindProperty("highwayMaterial").objectReferenceValue = SceneResources.Require<Material>("Assets/Game/Materials/Highway.mat");
            serialized.FindProperty("railMaterial").objectReferenceValue = SceneResources.Require<Material>("Assets/Game/Materials/Rails.mat");
            serialized.FindProperty("judgmentMaterial").objectReferenceValue = SceneResources.Require<Material>("Assets/Game/Materials/Judgement.mat");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static InputField AddNoteSpeedField(Transform panel)
        {
            var root = new GameObject("Preview Note Speed", typeof(RectTransform));
            root.transform.SetParent(panel, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(-10, 9);
            rect.sizeDelta = new Vector2(150, 24);

            var label = UiElements.Label("Note Speed Label", rect, "노트 속도", Vector2.zero, new Vector2(68, 24), 14);
            label.alignment = TextAnchor.MiddleRight;
            var go = DefaultControls.CreateInputField(new DefaultControls.Resources());
            go.name = "Note Speed Input";
            go.transform.SetParent(rect, false);
            UiElements.Position((RectTransform)go.transform, new Vector2(76, 0), new Vector2(58, 24));
            var field = go.GetComponent<InputField>();
            field.contentType = InputField.ContentType.DecimalNumber;
            field.text = "1.0";
            field.textComponent.fontSize = 14;
            field.textComponent.alignment = TextAnchor.MiddleCenter;
            field.placeholder.gameObject.SetActive(false);

            var unit = UiElements.Label("Note Speed Unit", rect, "x", new Vector2(138, 0), new Vector2(12, 24), 14);
            unit.alignment = TextAnchor.MiddleCenter;
            return field;
        }

        private static void AddNoteSpeedWarning(Transform canvas, WorkspaceView view, List<WorkspaceActionBinding> bindings)
        {
            var overlay = Panel("Note Speed Warning", canvas, Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0, 0, 0, .65f);

            var dialog = Panel("Dialog", overlay, Vector2.zero, new Vector2(480, 126));
            dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(.5f, .5f);
            var message = UiElements.Label("Message", dialog, "속도는 1.0 ~ 10.0 안에서만 조절 할 수 있습니다",
                new Vector2(16, -16), new Vector2(448, 44), 18);
            message.alignment = TextAnchor.MiddleCenter;
            var confirm = UiElements.Button("Close Note Speed Warning", dialog, "확인",
                new Vector2(180, -75), new Vector2(120, 32));
            StyleMenuButton(confirm, TextAnchor.MiddleCenter, 14);
            bindings.Add(new WorkspaceActionBinding { action = WorkspaceAction.CloseNoteSpeedWarning, button = confirm });
            view.noteSpeedWarning = overlay.gameObject;
            overlay.gameObject.SetActive(false);
        }

        private static readonly Dictionary<Transform, float> Positions = new Dictionary<Transform, float>();
        private static readonly Color MenuSurface = new Color(.94f, .94f, .94f);
        private static readonly Color MenuText = new Color(.16f, .16f, .16f);
        private static readonly Color MenuHover = new Color(.78f, .87f, .96f);

        private static RectTransform Menu(Transform parent, WorkspaceView view, string caption,
            int index, List<WorkspaceMenuPopup> menus)
        {
            var root = new GameObject("Workspace " + caption, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            UiElements.Position((RectTransform)root.transform, new Vector2(4 + index * 48, -1), new Vector2(48, 24));
            var trigger = UiElements.Button("Trigger", root.transform, caption, Vector2.zero, new Vector2(48, 24));
            StyleMenuButton(trigger, TextAnchor.MiddleCenter, 14);
            var viewport = Panel("Panel", root.transform, new Vector2(0, -24), new Vector2(510, 570));
            viewport.GetComponent<Image>().color = MenuSurface;
            var outline = viewport.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .25f);
            outline.effectDistance = new Vector2(1, -1);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            UiElements.Position(content, Vector2.zero, new Vector2(510, 570));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;
            Positions[content] = 4;
            var menu = root.AddComponent<WorkspaceMenuPopup>();
            menu.owner = view;
            menu.panel = viewport.gameObject;
            menu.trigger = trigger;
            viewport.gameObject.SetActive(false);
            menus.Add(menu);
            return content;
        }

        private static float Next(Transform parent, float height)
        {
            float y = Positions[parent];
            Positions[parent] += height + 2;
            var rect = (RectTransform)parent;
            rect.sizeDelta = new Vector2(510, Mathf.Max(570, Positions[parent]));
            return -y;
        }

        private static void Action(Transform parent, string caption, WorkspaceAction action,
            List<WorkspaceActionBinding> bindings)
        {
            string shortcut = WorkspaceShortcuts.Label(action);
            var button = UiElements.Button(action.ToString(), parent, caption,
                new Vector2(4, Next(parent, 24)), new Vector2(478, 24));
            StyleMenuButton(button, TextAnchor.MiddleLeft, 14);
            if (!string.IsNullOrEmpty(shortcut))
            {
                var shortcutLabel = AddShortcutLabel(button, shortcut, 14);
                button.GetComponentInChildren<Text>().rectTransform.offsetMax =
                    new Vector2(-shortcutLabel.preferredWidth - 30, 0);
            }
            bindings.Add(new WorkspaceActionBinding { action = action, button = button });
        }

        private static Text AddShortcutLabel(Button button, string shortcut, int fontSize)
        {
            var label = UiElements.Label("Shortcut", button.transform, shortcut, Vector2.zero, Vector2.zero, fontSize);
            label.color = MenuText;
            label.alignment = TextAnchor.MiddleRight;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = new Vector2(-10, 0);
            return label;
        }

        private static void AddOnAirDialog(Transform parent, WorkspaceView view, List<WorkspaceActionBinding> bindings)
        {
            var countdown = UiElements.Label("ON AIR Countdown", parent, "3", Vector2.zero, new Vector2(96, 96), 64);
            countdown.rectTransform.anchorMin = countdown.rectTransform.anchorMax = new Vector2(.5f, .5f);
            countdown.rectTransform.pivot = new Vector2(.5f, .5f);
            countdown.rectTransform.anchoredPosition = new Vector2(-190, 100);
            countdown.alignment = TextAnchor.MiddleCenter;
            countdown.gameObject.SetActive(false);
            view.onAirCountdown = countdown;

            var popup = Panel("ON AIR Confirmation", parent, Vector2.zero, Vector2.zero);
            popup.anchorMin = Vector2.zero;
            popup.anchorMax = Vector2.one;
            popup.offsetMin = popup.offsetMax = Vector2.zero;
            var heading = UiElements.Label("Question", popup, "ON AIR 모드에서는 다른 편집 단축키를 사용할 수 없습니다.\n3초 뒤 현재 위치부터 노트 입력을 시작할까요?",
                new Vector2(250, -230), new Vector2(780, 88), 23);
            heading.alignment = TextAnchor.MiddleCenter;
            PopupAction(popup, "YES", WorkspaceAction.ConfirmOnAir, 390, bindings);
            PopupAction(popup, "NO", WorkspaceAction.CancelOnAir, 660, bindings);
            popup.gameObject.SetActive(false);
            view.onAirPopup = popup.gameObject;
            view.onAirHeading = heading;
        }

        private static void BuildToolBar(Transform parent, WorkspaceView view)
        {
            var tool = parent.Find("Editor Content/Tool Space");
            var toolLabel = tool.Find("Tool Space Label").GetComponent<Text>();
            toolLabel.gameObject.SetActive(false);
            var captions = new[] { "선택", "그리기", "지우개", "잡기", "재생 위치", "ON AIR" };
            var actions = new[] { WorkspaceAction.Select, WorkspaceAction.Pencil,
                WorkspaceAction.Eraser, WorkspaceAction.Position, WorkspaceAction.Scroll, WorkspaceAction.OnAir };
            view.toolActions = new WorkspaceActionBinding[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                view.toolActions[i] = AddToolBarButton(tool, view, actions[i], captions[i], i);
            }
            view.quantizationCycle = UiElements.Button("Quantization Cycle", tool, "",
                Vector2.zero, Vector2.zero);
            StyleMenuButton(view.quantizationCycle, TextAnchor.MiddleCenter, 9);
            var caption = view.quantizationCycle.GetComponentInChildren<Text>();
            caption.gameObject.SetActive(false);
            caption.raycastTarget = false;
            view.quantizationCycleValue = UiElements.Label("Value", view.quantizationCycle.transform,
                "1/4", Vector2.zero, new Vector2(38, 38), 13);
            view.quantizationCycleValue.alignment = TextAnchor.MiddleCenter;
            view.quantizationCycleValue.color = MenuText;
            view.quantizationCycleValue.raycastTarget = false;
            PositionToolBarButton(view.quantizationCycle, actions.Length);
        }

        private static WorkspaceActionBinding AddToolBarButton(Transform tool, WorkspaceView view,
            WorkspaceAction action, string caption, int index)
        {
            var button = UiElements.Button("Tool " + action, tool, caption, Vector2.zero, Vector2.zero);
            StyleMenuButton(button, TextAnchor.MiddleLeft, 10);
            var label = button.GetComponentInChildren<Text>();
            label.gameObject.SetActive(false);
            AddShortcutLabel(button, WorkspaceShortcuts.Label(action), 9);
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(WorkspaceToolIcon))
                .GetComponent<WorkspaceToolIcon>();
            icon.transform.SetParent(button.transform, false);
            icon.kind = (WorkspaceToolIcon.IconKind)index;
            icon.color = MenuText;
            icon.raycastTarget = false;
            if (action == WorkspaceAction.OnAir)
            {
                view.onAirIcon = icon;
            }

            PositionToolBarButton(button, index);
            return new WorkspaceActionBinding { action = action, button = button };
        }

        private static void PositionToolBarButton(Button button, int index)
        {
            UiElements.Position((RectTransform)button.transform, new Vector2(7, -6 - index * 42), new Vector2(38, 38));
            var shortcut = button.transform.Find("Shortcut") as RectTransform;
            if (shortcut)
            {
                UiElements.Position(shortcut, new Vector2(0, -25), new Vector2(35, 11));
                UiElements.Position((RectTransform)button.transform.Find("Icon"), new Vector2(8, -4), new Vector2(22, 22));
            }
        }

        private static void StyleMenuButton(Button button, TextAnchor alignment, int fontSize)
        {
            var image = button.GetComponent<Image>();
            image.sprite = null;
            image.color = Color.white;
            var colors = button.colors;
            colors.normalColor = MenuSurface;
            colors.highlightedColor = MenuHover;
            colors.pressedColor = new Color(.65f, .79f, .93f);
            colors.selectedColor = MenuHover;
            colors.fadeDuration = .08f;
            button.colors = colors;

            var label = button.GetComponentInChildren<Text>();
            label.color = MenuText;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.rectTransform.offsetMin = new Vector2(10, 0);
            label.rectTransform.offsetMax = new Vector2(-10, 0);
        }

        private static void SizeMenu(WorkspaceMenuPopup menu)
        {
            var scroll = menu.panel.GetComponent<ScrollRect>();
            float width = 240;
            foreach (Transform item in scroll.content)
            {
                if (item.name == "Difficulty")
                {
                    float slotWidth = item.GetComponentsInChildren<Text>().Max(text => text.preferredWidth);
                    width = Mathf.Max(width, slotWidth * 3 + 40);
                }
                var label = item.GetComponent<Text>();
                if (!label)
                {
                    var button = item.GetComponent<Button>();
                    if (button)
                    {
                        label = button.GetComponentInChildren<Text>();
                    }
                }

                if (label)
                {
                    float requiredWidth = label.preferredWidth + 32;
                    var shortcut = item.Find("Shortcut");
                    if (shortcut)
                    {
                        requiredWidth = label.preferredWidth + shortcut.GetComponent<Text>().preferredWidth + 52;
                    }
                    width = Mathf.Max(width, requiredWidth);
                }
            }

            float height = Positions[scroll.content] + 2;
            scroll.content.sizeDelta = new Vector2(width, height);
            scroll.viewport.sizeDelta = new Vector2(width, Mathf.Min(620, height));
            foreach (RectTransform item in scroll.content)
            {
                item.sizeDelta = new Vector2(width - 8, item.sizeDelta.y);
            }
        }

        private static Text Heading(Transform parent, string caption)
        {
            var label = UiElements.Label("Heading", parent, caption,
                new Vector2(4, Next(parent, 22)), new Vector2(478, 22), 14);
            label.color = new Color(.38f, .38f, .38f);
            label.alignment = TextAnchor.MiddleLeft;
            return label;
        }

        private static InputField Field(Transform parent, string caption, string initial)
        {
            Heading(parent, caption);
            var go = DefaultControls.CreateInputField(new DefaultControls.Resources());
            go.name = caption;
            go.transform.SetParent(parent, false);
            UiElements.Position((RectTransform)go.transform, new Vector2(4, Next(parent, 24)), new Vector2(478, 24));
            foreach (var text in go.GetComponentsInChildren<Text>(true))
            {
                text.font = UiFontSettings.Current;
                text.fontSize = 14;
                text.alignment = TextAnchor.MiddleLeft;
                text.rectTransform.offsetMin = new Vector2(8, 2);
                text.rectTransform.offsetMax = new Vector2(-8, -2);
            }
            var field = go.GetComponent<InputField>();
            field.text = "";
            ((Text)field.placeholder).text = initial;
            if (double.TryParse(initial, out double numeric))
            {
                field.text = initial;
            }
            return field;
        }

        private static Dropdown Dropdown(Transform parent, string name, string[] options)
        {
            var dropdown = UiElements.Dropdown(name, parent,
                new Vector2(4, Next(parent, 24)), new Vector2(478, 24));
            foreach (var text in dropdown.GetComponentsInChildren<Text>(true))
            {
                text.fontSize = 14;
            }
            dropdown.captionText.rectTransform.offsetMin = new Vector2(8, 2);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-24, -2);
            dropdown.ClearOptions();
            dropdown.AddOptions(options.ToList());
            return dropdown;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = new Color(.075f, .09f, .13f, .98f);
            var rect = (RectTransform)panel.transform;
            UiElements.Position(rect, position, size);
            return rect;
        }

        private static void PopupAction(Transform parent, string caption, WorkspaceAction action, float x,
            List<WorkspaceActionBinding> bindings)
        {
            var button = UiElements.Button(action.ToString(), parent, caption,
                new Vector2(x, -340), new Vector2(230, 52));
            bindings.Add(new WorkspaceActionBinding { action = action, button = button });
        }
    }
}
