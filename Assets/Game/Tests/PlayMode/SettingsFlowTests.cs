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
            yield return Click(view.apply);

            var presentation = RhythmDojo.Services.PresentationPreferences.Load();
            Assert.That(presentation.DisplaySize, Is.EqualTo(new Vector2Int(2304, 1440)));
            Assert.That(UnityEngine.Application.targetFrameRate, Is.EqualTo(120));
            Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(beforeMipLimit));
            Assert.That(QualitySettings.shadowDistance, Is.EqualTo(beforeShadowDistance));
            Assert.That(QualitySettings.lodBias, Is.EqualTo(beforeLodBias));
            Assert.That(QualitySettings.anisotropicFiltering, Is.EqualTo(beforeFiltering));
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
        public IEnumerator BufferSelectionWithoutApplyLeavesActualAudioUnchanged()
        {
            yield return Click(
                UnityEngine.Object.FindFirstObjectByType<SongSelectionView>().settingsButton
            );
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            yield return null;

            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            var before = AudioSettings.GetConfiguration();
            view.bufferList.value = 1;
            yield return null;
            Assert.That(flow.SelectedBufferOption, Is.EqualTo(1));
            Assert.That(
                AudioSettings.GetConfiguration().dspBufferSize,
                Is.EqualTo(before.dspBufferSize)
            );
            Assert.That(view.actualState.text, Does.Contain($"{before.dspBufferSize} samples"));
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
            yield return Press(Key.DownArrow);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(UnityEngine.Object.FindFirstObjectByType<SettingsView>().apply.gameObject)
            );
            yield return Press(Key.Enter);
            Assert.That(
                UnityEngine
                    .Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
                    .Single(t => t.name == "Actual Audio State")
                    .text,
                Does.Contain($"{AudioSettings.GetConfiguration().dspBufferSize} samples")
            );
            Assert.That(flow.BufferStatus, Does.Contain("Actual:"));
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
            Assert.That(dropdown.value, Is.EqualTo(1));
            dropdown.value = 0;
            yield return Click(UnityEngine.Object.FindFirstObjectByType<SettingsView>().apply);
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
