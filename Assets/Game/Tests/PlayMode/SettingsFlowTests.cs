using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class SettingsFlowTests : GameplayTestContext
    {
        [UnityTest]
        public IEnumerator SpeedTestUsesOneLaneAndReportsCalibratedJudgment()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().settingsButton);
            yield return Await(() => Object.FindFirstObjectByType<SettingsView>());
            var view = Object.FindFirstObjectByType<SettingsView>();
            yield return Click(view.gameplayTab);
            view.speedSlider.value = 2;
            view.timingOffset.value = 10;
            var entry = view.gameplayPanel.transform.Find("Speed Test").GetComponent<Button>();
            AssertSettingsButtonStyle(entry, view.back);
            yield return Click(entry);
            var panel = view.transform.Find("Timing Calibration Overlay/Timing Calibration Panel");
            var rail = panel.Find("Speed Test Lane");
            var status = rail.Find("Timing Calibration Status").GetComponent<Text>();
            var judgment = rail.Find("Speed Test Feedback/Judgment Grade").GetComponent<Text>();
            var timing = rail.Find("Speed Test Feedback/Judgment Timing").GetComponent<Text>();
            var start = panel.Find("Start Calibration").GetComponent<Button>();

            Assert.That(rail.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.Find("Timing Calibration Heading").GetComponent<Text>().text, Is.EqualTo("2.0x"));
            Assert.That(((RectTransform)panel).rect.height, Is.GreaterThan(((RectTransform)panel).rect.width));
            Canvas.ForceUpdateCanvases();
            var perspective = rail.Find("Perspective Rail").GetComponent<SpeedTestLaneGraphic>();
            var vertices = perspective.canvasRenderer.GetMesh().vertices;
            Assert.That(vertices.Length, Is.EqualTo(8));
            Assert.That(vertices[3].x - vertices[0].x, Is.GreaterThan(vertices[2].x - vertices[1].x));
            Assert.That(rail.Find("Speed Test Hit Line").GetComponent<Image>().color.b, Is.GreaterThan(.8f));
            Assert.That(panel.Find("Apply Calibration").gameObject.activeSelf, Is.False);

            start.onClick.Invoke();
            double hitAt = Time.realtimeSinceStartupAsDouble + 1.75 + .030;
            yield return Await(() => rail.Find("Beat Note 0").gameObject.activeSelf);
            float before = ((RectTransform)rail.Find("Beat Note 0")).anchoredPosition.y;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(((RectTransform)rail.Find("Beat Note 0")).anchoredPosition.y, Is.LessThan(before));


            while (Time.realtimeSinceStartupAsDouble < hitAt)
            {
                yield return null;
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), hitAt);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), hitAt + .001);
            yield return Await(() => judgment.text == "Perfect");
            Assert.That(timing.text, Does.Match(@"\+\d+ ms"));


            double lateHitAt = hitAt + .5 + .130;
            while (Time.realtimeSinceStartupAsDouble < lateHitAt)
            {
                yield return null;
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), lateHitAt);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), lateHitAt + .001);
            yield return Await(() => judgment.text == "Nope." && timing.text.Contains("ms"));

            yield return Click(start);
            var note = (RectTransform)rail.Find("Beat Note 0");
            Vector2 frozen = note.anchoredPosition;
            Assert.That(status.gameObject.activeInHierarchy, Is.True);
            Assert.That(status.text, Is.EqualTo("일시정지"));
            Assert.That(start.GetComponentInChildren<Text>().text, Is.EqualTo("계속하기"));
            Assert.That(panel.parent.GetComponent<AudioSource>().isPlaying, Is.False);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(note.anchoredPosition, Is.EqualTo(frozen));
            yield return Press(Key.P);
            Assert.That(status.gameObject.activeSelf, Is.False);
            Assert.That(start.GetComponentInChildren<Text>().text, Is.EqualTo("일시정지"));
            yield return Await(() => note.anchoredPosition != frozen);
            yield return Press(Key.Escape);
            Assert.That(panel.gameObject.activeInHierarchy, Is.False);
            Assert.That(flow.Preferences.TimingOffsetMs, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator CalibrationMeasuresSpaceTapsAndWaitsForApply()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().settingsButton);
            yield return Await(() => Object.FindFirstObjectByType<SettingsView>());
            var view = Object.FindFirstObjectByType<SettingsView>();
            yield return Click(view.gameplayTab);
            var entry = view.gameplayPanel.transform.Find("Timing Calibration").GetComponent<Button>();
            AssertSettingsButtonStyle(entry, view.back);
            yield return Click(entry);
            var panel = view.transform.Find("Timing Calibration Overlay/Timing Calibration Panel");
            var start = panel.Find("Start Calibration").GetComponent<Button>();
            var apply = panel.Find("Apply Calibration").GetComponent<Button>();
            var close = panel.Find("Close Calibration").GetComponent<Button>();
            AssertSettingsButtonStyle(start, view.apply);
            AssertSettingsButtonStyle(apply, view.apply);
            AssertSettingsButtonStyle(close, view.back);
            Assert.That(apply.interactable, Is.False);
            start.onClick.Invoke();
            double firstBeat = Time.realtimeSinceStartupAsDouble + .25;
            for (int beat = 4; beat < 16; beat++)
            {
                double timestamp = firstBeat + beat * .5 + .035;
                while (Time.realtimeSinceStartupAsDouble < timestamp)
                {
                    yield return null;
                }

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), timestamp);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), timestamp + .001);
            }

            yield return Await(() => apply.interactable);
            Assert.That(flow.Preferences.TimingOffsetMs, Is.Zero);
            yield return Click(apply);
            Assert.That(view.timingOffset.value, Is.InRange(32, 40));
            Assert.That(flow.Preferences.TimingOffsetMs, Is.EqualTo((int)view.timingOffset.value));
            Assert.That(panel.gameObject.activeInHierarchy, Is.False);
        }

        private static void AssertSettingsButtonStyle(Button button, Button style)
        {
            var frame = button.targetGraphic as PixelDojoFrameGraphic;
            var sourceFrame = (PixelDojoFrameGraphic)style.targetGraphic;
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame.artwork, Is.SameAs(sourceFrame.artwork));
            Assert.That(frame.color, Is.EqualTo(sourceFrame.color));
            Assert.That(frame.borderWidth, Is.EqualTo(sourceFrame.borderWidth));
            Assert.That(button.colors, Is.EqualTo(style.colors));

            var label = button.GetComponentInChildren<Text>();
            var sourceLabel = style.GetComponentInChildren<Text>();
            Assert.That(label.font, Is.SameAs(sourceLabel.font));
            Assert.That(label.material, Is.SameAs(sourceLabel.material));
            Assert.That(label.fontSize, Is.EqualTo(sourceLabel.fontSize));
            Assert.That(label.color, Is.EqualTo(sourceLabel.color));
        }

        [UnityTest]
        public IEnumerator SettingsPopupOpensImmediatelyAndReusesInstanceWithoutLoadingScene()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var selection = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var scene = SceneManager.GetActiveScene();
            int sceneCount = SceneManager.sceneCount;
            EventSystem.current.SetSelectedGameObject(selection.settingsButton.gameObject);

            flow.ShowSettings();
            var view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            Assert.That(view, Is.Not.Null, "Opening must not wait for scene loading.");
            Assert.That(view.backdrop.activeSelf, Is.False);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            Assert.That(SceneManager.GetSceneByPath(AppFlowController.SettingsPath).isLoaded, Is.False);
            Assert.That(selection.GetComponent<GraphicRaycaster>().enabled, Is.False);
            flow.ShowSettings();
            Assert.That(UnityEngine.Object.FindObjectsByType<SettingsView>(
                FindObjectsSortMode.None).Length, Is.EqualTo(1));

            flow.ReturnFromSettings();
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(selection.GetComponent<GraphicRaycaster>().enabled, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(selection.settingsButton.gameObject));
            Assert.That(flow.ApplyBufferSize(), Is.False);

            flow.ShowSettings();
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SettingsView>(), Is.SameAs(view));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            yield return Click(view.gameplayTab);
            Assert.That(view.gameplayPanel.activeSelf, Is.True);
            CaptureSelection("settings-hold-rules", 1920, 1080);
            yield return Press(Key.Escape);
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
        }

        [UnityTest]
        public IEnumerator GameplayTabPreservesAudioAndGraphicsAndSavesTimingLimits()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var selection = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            yield return Click(selection.settingsButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsView>());
            yield return null;

            var view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            var beforeAudio = AudioSettings.GetConfiguration();
            int beforeFrameRate = UnityEngine.Application.targetFrameRate;
            yield return Click(view.gameplayTab);
            Assert.That(view.gameplayPanel.activeSelf, Is.True);
            Assert.That(view.soundPanel.activeSelf, Is.False);
            Assert.That(view.graphicsPanel.activeSelf, Is.False);
            Assert.That(view.speedSlider.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.timingOffset.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.timingOffset.minValue, Is.EqualTo(-200));
            Assert.That(view.timingOffset.maxValue, Is.EqualTo(200));
            Assert.That(view.judgmentPositionList.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.judgmentPositionList.options.Select(option => option.text),
                Is.EqualTo(new[] { "상단", "중앙", "하단" }));
            Assert.That(view.judgmentPositionList.value, Is.EqualTo(1));
            view.judgmentPositionList.value = 2;
            var presentation = JsonUtility.FromJson<RhythmDojo.Services.PresentationPreferences>(
                PlayerPrefs.GetString(RhythmDojo.Services.PresentationPreferences.StorageKey)
            );
            Assert.That(presentation.judgmentPosition, Is.EqualTo(RhythmDojo.Services.JudgmentPosition.Lower));

            foreach (int value in new[] { -200, 0, 200 })
            {
                view.timingOffset.value = value;
                Assert.That(flow.Preferences.TimingOffsetMs, Is.EqualTo(value));
                Assert.That(
                    new RhythmDojo.Services.SelectionPreferences().TimingOffsetMs,
                    Is.EqualTo(value)
                );
            }

            view.speedSlider.value = 3.2f;
            Assert.That(
                new RhythmDojo.Services.SelectionPreferences().Multiplier,
                Is.EqualTo(3.2).Within(.0001)
            );
            yield return Click(view.apply);
            Assert.That(
                AudioSettings.GetConfiguration().dspBufferSize,
                Is.EqualTo(beforeAudio.dspBufferSize)
            );
            Assert.That(UnityEngine.Application.targetFrameRate, Is.EqualTo(beforeFrameRate));
            yield return Click(view.soundTab);
            Assert.That(view.gameplayPanel.activeSelf, Is.False);
            var popupFrame = view.transform.Find("Settings Popup").GetComponentInChildren<PixelDojoFrameGraphic>();
            Assert.That(popupFrame.artwork.name, Is.EqualTo("SettingsPanel"));
            Canvas.ForceUpdateCanvases();
            var border = popupFrame.GetComponentInChildren<Image>();
            Assert.That(border.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(border.fillCenter, Is.False);
            Assert.That(border.raycastTarget, Is.False);
            var panelMesh = border.canvasRenderer.GetMesh();
            Assert.That(panelMesh.vertexCount, Is.EqualTo(32));
            Assert.That(panelMesh.vertices[2].x - panelMesh.vertices[0].x, Is.EqualTo(24f).Within(.01f),
                "The textured frame corners must retain their size instead of stretching with the panel.");
            Assert.That(view.backdrop.GetComponent<Image>().sprite.name, Is.EqualTo("TrainingInterior"));
            foreach (var button in new[] { view.soundTab, view.graphicsTab, view.gameplayTab, view.apply, view.back })
            {
                var frame = button.GetComponentInChildren<PixelDojoFrameGraphic>();
                Assert.That(frame.artwork.name, Is.EqualTo("SettingsControl"));
                Assert.That(frame.mainTexture, Is.SameAs(frame.artwork.texture));
            }
            Assert.That(view.soundPanel.activeSelf, Is.True);
            yield return Click(view.graphicsTab);
            Assert.That(view.gameplayPanel.activeSelf, Is.False);
            Assert.That(view.graphicsPanel.activeSelf, Is.True);
            yield return Click(view.gameplayTab);
            EventSystem.current.SetSelectedGameObject(view.speedSlider.gameObject);
            yield return Press(Key.DownArrow);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(view.timingOffset.gameObject)
            );
            yield return Press(Key.LeftArrow);
            Assert.That(flow.Preferences.TimingOffsetMs, Is.EqualTo(199));
            yield return Press(Key.Escape);
            yield return Await(() => !UnityEngine.Object.FindFirstObjectByType<SettingsView>());
            yield return null;
            Assert.That(selection.timingOffset.value, Is.EqualTo(199));
            Assert.That(selection.speedSlider.value, Is.EqualTo(3.2f).Within(.0001));
            flow.ShowSelection();
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
            yield return null;
            selection = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            Assert.That(selection.speedSlider.value, Is.EqualTo(3.2f).Within(.0001));
            Assert.That(selection.timingOffset.value, Is.EqualTo(199));
        }

        [UnityTest]
        public IEnumerator FrameRateOptionsApplySavedLimitsAndMeasureActualFrames()
        {
            UnityEngine
                .Object.FindFirstObjectByType<SongSelectionView>()
                .settingsButton.onClick.Invoke();
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            yield return null;

            var view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            view.graphicsTab.onClick.Invoke();

            var report = new System.Text.StringBuilder(
                "RequestedFPS,TargetFPS,VSync,MeasuredFPS,Width,Height\n"
            );

            try
            {
                for (
                    int option = 0;
                    option < RhythmDojo.Services.PresentationPreferences.FrameRates.Length;
                    option++
                )
                {
                    view.fpsList.value = option;
                    view.apply.onClick.Invoke();

                    int requested = RhythmDojo.Services.PresentationPreferences.FrameRates[option];
                    Assert.That(UnityEngine.Application.targetFrameRate, Is.EqualTo(requested));
                    Assert.That(QualitySettings.vSyncCount, Is.Zero);

                    var saved = JsonUtility.FromJson<RhythmDojo.Services.PresentationPreferences>(
                        PlayerPrefs.GetString(
                            RhythmDojo.Services.PresentationPreferences.StorageKey
                        )
                    );
                    Assert.That(saved.frameRate, Is.EqualTo(option));
                    yield return new WaitForSecondsRealtime(.5f);

                    int frames = 0;
                    double start = Time.realtimeSinceStartupAsDouble;

                    while (Time.realtimeSinceStartupAsDouble - start < 2)
                    {
                        yield return null;
                        frames++;
                    }

                    double measured = frames / (Time.realtimeSinceStartupAsDouble - start);
                    report.AppendLine(
                        $"{requested},{UnityEngine.Application.targetFrameRate},{QualitySettings.vSyncCount},{measured:F2},{Screen.width},{Screen.height}"
                    );
                    Assert.That(measured, Is.GreaterThan(0));

                    if (requested > 0)
                    {
                        Assert.That(
                            measured,
                            Is.LessThanOrEqualTo(requested * 1.3 + 2),
                            "Actual frame loop must respect the applied cap."
                        );
                    }

                    if (requested > 0)
                    {
                        Assert.That(
                            view.displayState.text,
                            Does.Contain(($"FPS 제한 {requested} FPS"))
                        );
                    }
                    else
                    {
                        Assert.That(view.displayState.text, Does.Contain("FPS 제한 제한 없음"));
                    }

                    Assert.That(view.displayState.text, Does.Not.Contain("측정 중"));
                    Assert.That(
                        view.displayState.text,
                        Does.Contain($"현재 {Screen.width} × {Screen.height}")
                    );
                }
            }
            finally
            {
                System.IO.File.WriteAllText("Logs/frame-rate-measurements.csv", report.ToString());
            }
        }

        [UnityTest]
        public IEnumerator DisplayOptionsReportActualScreenSizeForAllCombinations()
        {
            UnityEngine
                .Object.FindFirstObjectByType<SongSelectionView>()
                .settingsButton.onClick.Invoke();
            yield return Await(
                () => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>(),
                "Display test did not open settings popup."
            );
            yield return null;

            var view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            view.graphicsTab.onClick.Invoke();
            yield return null;

            var report = new System.Text.StringBuilder(
                "Environment,Aspect,Resolution,RequestedWidth,RequestedHeight,BeforeWidth,BeforeHeight,ActualWidth,ActualHeight,FullscreenMode\n"
            );
            var expectedWidths = new[] { 1920, 2560, 1728, 2304 };

            try
            {
                for (int aspectOption = 0; aspectOption < 2; aspectOption++)
                {
                    for (int resolutionOption = 0; resolutionOption < 2; resolutionOption++)
                    {
                        view.aspectList.value = aspectOption;
                        view.resolutionList.value = resolutionOption;
                        yield return null;

                        var before = new Vector2Int(Screen.width, Screen.height);
                        view.apply.onClick.Invoke();
                        yield return Await(() => !UnityEngine.Object.FindFirstObjectByType<AppFlowController>()
                            .Audio.IsBufferValidationPending, "Audio verification did not finish after display apply.", timeoutSeconds: RhythmDojo.Services.AudioSettingsService.ClockValidationTimeoutSeconds + 2);
                        yield return Await(
                            () =>
                                new Vector2Int(Screen.width, Screen.height)
                                == RhythmDojo.Services.PresentationPreferences.Current.DisplaySize,
                            "Actual screen size did not match the applied display settings."
                        );

                        var requested = RhythmDojo
                            .Services
                            .PresentationPreferences
                            .Current
                            .DisplaySize;
                        var actual = new Vector2Int(Screen.width, Screen.height);

                        if (UnityEngine.Application.isEditor)
                        {
                            report.AppendLine(
                                $"Editor,{(aspectOption switch { 0 => "16:9", _ => "16:10" })},{(resolutionOption switch { 0 => "FHD", _ => "QHD" })},{requested.x},{requested.y},{before.x},{before.y},{actual.x},{actual.y},{Screen.fullScreenMode}"
                            );
                        }
                        else
                        {
                            report.AppendLine(
                                $"Player,{(aspectOption switch { 0 => "16:9", _ => "16:10" })},{(resolutionOption switch { 0 => "FHD", _ => "QHD" })},{requested.x},{requested.y},{before.x},{before.y},{actual.x},{actual.y},{Screen.fullScreenMode}"
                            );
                        }

                        Assert.That(
                            requested.x,
                            Is.EqualTo(expectedWidths[aspectOption * 2 + resolutionOption])
                        );
                        Assert.That(
                            requested.y,
                            Is.EqualTo(
                                (
                                    resolutionOption switch
                                    {
                                        0 => 1080,
                                        _ => 1440,
                                    }
                                )
                            )
                        );
                        Assert.That(
                            view.displayState.text,
                            Does.Contain($"{requested.x} × {requested.y}")
                        );

                        var saved =
                            JsonUtility.FromJson<RhythmDojo.Services.PresentationPreferences>(
                                PlayerPrefs.GetString(
                                    RhythmDojo.Services.PresentationPreferences.StorageKey
                                )
                            );
                        Assert.That(saved.aspect, Is.EqualTo(aspectOption));
                        Assert.That(saved.resolution, Is.EqualTo(resolutionOption));
                        Assert.That(
                            actual,
                            Is.EqualTo(requested),
                            "Rendered screen must use the applied resolution and aspect ratio."
                        );
                    }
                }
            }
            finally
            {
                System.IO.File.WriteAllText(
                    "Logs/display-size-measurements.csv",
                    report.ToString()
                );
            }

            view.back.onClick.Invoke();
            yield return Await(() => !UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
        }

        [UnityTest]
        public IEnumerator SettingsPopupTabsSaveAudioAndApplySeparateGraphicsControls()
        {
            var selection = UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>();
            yield return Click(
                UnityEngine.Object.FindFirstObjectByType<SongSelectionView>().settingsButton
            );
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            yield return null;

            var view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            Assert.That(selection.enabled, Is.False, "Background input is suspended.");
            Assert.That(view.soundPanel.activeSelf, Is.True);
            Assert.That(view.graphicsPanel.activeSelf, Is.False);
            Assert.That(view.gameplayPanel.activeSelf, Is.False);
            view.masterVolume.value = 45;
            view.backgroundVolume.value = 30;
            view.gameplayVolume.value = 70;
            Assert.That(AudioListener.volume, Is.EqualTo(.45f).Within(.001));
            Assert.That(
                RhythmDojo.Services.PresentationPreferences.Current.backgroundVolume,
                Is.EqualTo(.3f).Within(.001)
            );
            Assert.That(
                RhythmDojo.Services.PresentationPreferences.Current.gameplayVolume,
                Is.EqualTo(.7f).Within(.001)
            );
            yield return Click(view.graphicsTab);
            Assert.That(view.soundPanel.activeSelf, Is.False);
            Assert.That(view.graphicsPanel.activeSelf, Is.True);
            Assert.That(view.bufferList.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.displayState.gameObject.activeSelf, Is.False);
            Assert.That(view.actualState.gameObject.activeSelf, Is.False);
            Assert.That(view.status.gameObject.activeSelf, Is.False);
            foreach (var dropdown in new[] { view.aspectList, view.resolutionList, view.fpsList, view.bufferList })
            {
                Assert.That(((RectTransform)dropdown.transform).sizeDelta, Is.EqualTo(new Vector2(520, 64)));
                Assert.That(dropdown.captionText.fontSize, Is.EqualTo(24));
                Assert.That(dropdown.image.sprite.name, Is.EqualTo("SettingsControl"));
                Assert.That(dropdown.image.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(dropdown.transform.Find("Settings Expand Indicator"), Is.Not.Null);
                foreach (var toggle in dropdown.template.GetComponentsInChildren<Toggle>(true))
                {
                    Assert.That(toggle.graphic.gameObject.activeSelf, Is.False,
                        "Settings dropdowns must not show a square beside the selected option.");
                }
            }

            Assert.That(view.aspectList.options.Count, Is.EqualTo(2));
            Assert.That(view.resolutionList.options.Count, Is.EqualTo(2));
            view.aspectList.value = 1;
            view.resolutionList.value = 1;
            view.fpsList.value = 2;
            Assert.That(view.qualityButtons, Is.Empty);
            Assert.That(
                view.GetComponentsInChildren<Text>(true).Any(t => t.text == "화질"),
                Is.False
            );

            int beforeMipLimit = QualitySettings.globalTextureMipmapLimit;
            float beforeShadowDistance = QualitySettings.shadowDistance,
                beforeLodBias = QualitySettings.lodBias;
            var beforeFiltering = QualitySettings.anisotropicFiltering;
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var beforeAudio = AudioSettings.GetConfiguration();
            yield return Click(view.apply);

            var presentation = RhythmDojo.Services.PresentationPreferences.Load();
            Assert.That(presentation.DisplaySize, Is.EqualTo(new Vector2Int(2304, 1440)));
            Assert.That(UnityEngine.Application.targetFrameRate, Is.EqualTo(120));
            Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(beforeMipLimit));
            Assert.That(QualitySettings.shadowDistance, Is.EqualTo(beforeShadowDistance));
            Assert.That(QualitySettings.lodBias, Is.EqualTo(beforeLodBias));
            Assert.That(QualitySettings.anisotropicFiltering, Is.EqualTo(beforeFiltering));
            Assert.That(flow.Audio.IsBufferValidationPending, Is.False);
            Assert.That(flow.Audio.HasBufferValidationResult, Is.False);
            Assert.That(AudioSettings.GetConfiguration().dspBufferSize, Is.EqualTo(beforeAudio.dspBufferSize));
            yield return Click(view.back);
            yield return Await(() => !UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            Assert.That(selection.enabled, Is.True);
            yield return Click(
                UnityEngine.Object.FindFirstObjectByType<SongSelectionView>().settingsButton
            );
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            yield return null;
            view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            Assert.That(view.masterVolume.value, Is.EqualTo(45));
            Assert.That(view.backgroundVolume.value, Is.EqualTo(30));
            Assert.That(view.gameplayVolume.value, Is.EqualTo(70));
            Assert.That(view.aspectList.value, Is.EqualTo(1));
            Assert.That(view.resolutionList.value, Is.EqualTo(1));
            yield return Press(Key.Escape);
            yield return Await(() => !UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
        }

        [UnityTest]
        public IEnumerator SettingsButtonOpensSettingsAndBackPreservesSongOptions()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var selection = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            UnityEngine
                .Object.FindFirstObjectByType<SongSelectionScreen>()
                .SelectSong("tempo-pulse");
            flow.Selection.UpdateSelection(
                flow.Selection.SelectedSongId,
                flow.SelectedDifficulty,
                flow.SelectedScrollMode,
                1.5
            );
            yield return Click(selection.settingsButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            yield return null;
            yield return Click(UnityEngine.Object.FindFirstObjectByType<SettingsView>().back);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
            yield return null;
            Assert.That(flow.SelectedSongIndex, Is.EqualTo(1));
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(1.5));
            Assert.That(flow.Selection.SelectedSongId, Is.EqualTo("tempo-pulse"));
        }

        [UnityTest]
        public IEnumerator BufferValidationBlocksEarlyCloseAndShowsTheAppliedConfiguration()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.ShowSettings();
            var view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            yield return Click(view.graphicsTab);
            view.bufferList.value = 1;
            view.apply.onClick.Invoke();
            Assert.That(flow.Audio.IsBufferValidationPending, Is.True);
            Assert.That(view.back.interactable, Is.False);
            Assert.That(view.bufferList.interactable, Is.False);
            yield return Press(Key.Escape);
            Assert.That(view.gameObject.activeInHierarchy, Is.True, "Escape must not resume a song before validation ends.");
            yield return Await(() => !flow.Audio.IsBufferValidationPending, "Audio buffer validation did not finish.", timeoutSeconds: RhythmDojo.Services.AudioSettingsService.ClockValidationTimeoutSeconds + 2);
            AssertClockRateVerified(flow.Audio);
            Assert.That(view.back.interactable, Is.True);
            Assert.That(view.bufferList.value, Is.EqualTo(flow.SelectedBufferOption));
            Assert.That(view.status.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.status.text, Does.Contain($"{AudioSettings.GetConfiguration().dspBufferSize} samples"));
            yield return Click(view.back);
            Assert.That(view.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SettingsMouseAndKeyboardApplyActualBufferAndKeepSelection()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(flow.DefaultBufferSize, Is.EqualTo(originalAudio.dspBufferSize));

            var before = AudioSettings.GetConfiguration();
            Assert.That(flow.ApplyBufferSize(), Is.False);
            Assert.That(
                AudioSettings.GetConfiguration().dspBufferSize,
                Is.EqualTo(before.dspBufferSize)
            );
            yield return Click(ButtonNamed("Settings"));
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            yield return null;
            yield return Click(
                UnityEngine.Object.FindFirstObjectByType<SettingsView>().graphicsTab
            );
            EventSystem.current.SetSelectedGameObject(
                UnityEngine.Object.FindFirstObjectByType<SettingsView>().bufferList.gameObject
            );
            CaptureSelection("settings");

            var dropdown = UnityEngine.Object.FindFirstObjectByType<SettingsView>().bufferList;
            Assert.That(dropdown.options.Count, Is.EqualTo(8));
            yield return Press(Key.Enter);
            yield return Press(Key.DownArrow);
            yield return Press(Key.Enter);
            yield return new WaitForSecondsRealtime(.25f);

            // uGUI restores selection after closing its popup.
            Assert.That(flow.SelectedBufferOption, Is.EqualTo(1));
            Assert.That(
                AudioSettings.GetConfiguration().dspBufferSize,
                Is.EqualTo(before.dspBufferSize),
                "Selection alone must not reset audio."
            );
            Assert.That(
                UnityEngine.Object.FindFirstObjectByType<SettingsView>().actualState.text,
                Does.Contain($"{before.dspBufferSize} samples")
            );
            yield return Press(Key.DownArrow);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(UnityEngine.Object.FindFirstObjectByType<SettingsView>().apply.gameObject)
            );
            yield return Press(Key.Enter);
            Assert.That(
                UnityEngine
                    .Object.FindFirstObjectByType<SettingsView>()
                    .actualState
                    .text,
                Does.Contain($"{AudioSettings.GetConfiguration().dspBufferSize} samples")
            );
            Assert.That(flow.BufferStatus, Does.Contain("Actual:"));
            yield return Await(() => !flow.Audio.IsBufferValidationPending, "Audio verification did not finish.", timeoutSeconds: RhythmDojo.Services.AudioSettingsService.ClockValidationTimeoutSeconds + 2);
            int appliedOption = flow.SelectedBufferOption;
            yield return Press(Key.RightArrow);
            yield return Press(Key.Enter);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
            yield return null;
            EventSystem.current.SetSelectedGameObject(ButtonNamed("Settings").gameObject);
            yield return Press(Key.Enter);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            yield return null;
            dropdown = UnityEngine.Object.FindFirstObjectByType<SettingsView>().bufferList;
            yield return Click(
                UnityEngine.Object.FindFirstObjectByType<SettingsView>().graphicsTab
            );
            Assert.That(dropdown.value, Is.EqualTo(appliedOption));
            dropdown.value = 0;
            yield return Click(UnityEngine.Object.FindFirstObjectByType<SettingsView>().apply);
            yield return Await(() => !flow.Audio.IsBufferValidationPending, timeoutSeconds: RhythmDojo.Services.AudioSettingsService.ClockValidationTimeoutSeconds + 2);
            Assert.That(
                AudioSettings.GetConfiguration().dspBufferSize,
                Is.EqualTo(flow.DefaultBufferSize)
            );
            yield return Click(UnityEngine.Object.FindFirstObjectByType<SettingsView>().back);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
        }
    }
}
