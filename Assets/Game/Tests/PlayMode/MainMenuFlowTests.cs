using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RhythmDojo.Core;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [PrebuildSetup(typeof(TestRunStartup))]
    [PostBuildCleanup(typeof(TestRunStartup))]
    [Category("UI")]
    public sealed class MainMenuFlowTests
    {
        private NativeInputScope nativeInput;
        private string originalRules;
        private bool hadRules;

        [SetUp]
        public void IsolateInput()
        {
            nativeInput = new NativeInputScope();
            hadRules = PlayerPrefs.HasKey(PlayRulesService.StorageKey);
            originalRules = PlayerPrefs.GetString(PlayRulesService.StorageKey);
        }

        [TearDown]
        public void RestoreInput()
        {
            nativeInput?.Dispose();
            nativeInput = null;
            if (hadRules)
            {
                PlayerPrefs.SetString(PlayRulesService.StorageKey, originalRules);
            }
            else
            {
                PlayerPrefs.DeleteKey(PlayRulesService.StorageKey);
            }
            PlayerPrefs.Save();
        }

        private MainMenuView view;
        private MainMenuScreen screen;
        private Mouse mouse;
        private InputSettings input;
        private InputSettings.EditorInputBehaviorInPlayMode editorBehavior;
        private InputSettings.BackgroundBehavior backgroundBehavior;

        private sealed class Actions : IMainMenuActions
        {
            public bool Transitioning { get; set; }

            public int Starts,
                Quits,
                Settings;

            public void StartGame()
            {
                Starts++;
                Transitioning = true;
            }

            public void ShowSettings()
            {
                Settings++;
                Transitioning = true;
            }

            public void QuitGame()
            {
                Quits++;
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            input = InputSystem.settings;
            editorBehavior = input.editorInputBehaviorInPlayMode;
            backgroundBehavior = input.backgroundBehavior;
            input.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            input.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            yield return SceneManager.LoadSceneAsync(MainMenuActions.ModeSelectionScenePath);
            yield return null;
            view = UnityEngine.Object.FindFirstObjectByType<MainMenuView>();
            screen = UnityEngine.Object.FindFirstObjectByType<MainMenuScreen>();
            Assert.That(view, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mouse != null && mouse.added)
            {
                InputSystem.RemoveDevice(mouse);
            }

            input.editorInputBehaviorInPlayMode = editorBehavior;
            input.backgroundBehavior = backgroundBehavior;

            if (DojoEntranceTransition.Active)
            {
                DojoEntranceTransition.Active.Cancel();
            }

            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();

            if (flow)
            {
                UnityEngine.Object.Destroy(flow.gameObject);
            }

            yield return null;
        }

        private IEnumerator ClickAt(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            yield return null;
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator ModeReentryAndRepeatedTravelKeepSubmitTargetFreeOfLegacyArtwork()
        {
            for (int entry = 0; entry < 2; entry++)
            {
                yield return OpenMenu();
                var button = screen;
                var actions = new Actions();
                screen.Initialize(actions);
                screen.Initialize(actions);
                screen.ResumeAfterModal();
                AssertSubmitHasNoArtwork();

                for (int step = 0; step < 8; step++)
                {
                    int expected = (view.modeSelection.SelectedIndex + 1) % 4;
                    if (step % 2 == 0)
                    {
                        yield return Click(view.modeSelection.next);
                    }
                    else
                    {
                        var keyboard = InputSystem.AddDevice<Keyboard>();
                        try
                        {
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
                            yield return null;
                            yield return null;
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                            yield return null;
                        }
                        finally
                        {
                            InputSystem.RemoveDevice(keyboard);
                        }
                    }
                    double deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (view.modeSelection.IsMoving || view.modeSelection.IsArriving)
                    {
                        Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                        AssertSubmitHasNoArtwork();
                        yield return null;
                    }
                    Assert.That(view.modeSelection.SelectedIndex, Is.EqualTo(expected));
                    Assert.That(screen, Is.SameAs(button));
                    AssertSubmitHasNoArtwork();
                    Capture("Logs/mode-clean-" + expected + ".png", 1920, 1080);
                }

                if (entry == 0)
                {
                    yield return SubmitStart();
                }
                else
                {
                    var gamepad = InputSystem.AddDevice<Gamepad>();
                    try
                    {
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(screen.gameObject);
                        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
                        yield return null;
                        yield return null;
                        InputSystem.QueueStateEvent(gamepad, new GamepadState());
                        yield return null;
                    }
                    finally
                    {
                        InputSystem.RemoveDevice(gamepad);
                    }
                }
                double submitDeadline = Time.realtimeSinceStartupAsDouble + 5;
                while (actions.Starts == 0)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(submitDeadline));
                    AssertSubmitHasNoArtwork();
                    yield return null;
                }
                Assert.That(actions.Starts, Is.EqualTo(1));
                if (entry == 0)
                {
                    yield return LoadTitle();
                }
            }
        }

        private void AssertSubmitHasNoArtwork()
        {
            Assert.That(view.transform.Find("Menu Buttons/Mode Stages/Play"), Is.Null,
                "The retired start button must not survive as an invisible focus target.");
            Assert.That(screen.GetComponent<UnityEngine.UI.Selectable>(), Is.Null);
            Assert.That(screen.transform.Find("Dojo Focus Border"), Is.Null,
                "Screen confirmation must not acquire a button focus rectangle.");
        }

        private IEnumerator SubmitStart()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(screen.gameObject);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private IEnumerator Click(Button button)
        {
            Canvas.ForceUpdateCanvases();

            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center)
            );
            var hits =
                new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            var events = UnityEngine.EventSystems.EventSystem.current;
            events.RaycastAll(
                new UnityEngine.EventSystems.PointerEventData(events) { position = point },
                hits
            );
            Assert.That(hits.Count, Is.GreaterThan(0), "No raycast hit for " + button.name);
            Assert.That(
                hits[0].gameObject.GetComponentInParent<Button>(),
                Is.EqualTo(button),
                "Wrong raycast target for " + button.name
            );
            yield return ClickAt(point);
        }

        private IEnumerator OpenMenu()
        {
            if (!view.modeSelection)
            {
                yield return Click(view.GetComponent<TitleSkyView>().start);
                yield return WaitForMenuScene(MainMenuActions.ModeSelectionScenePath);
            }
            yield return null;
            Assert.That(view.menuPanel.alpha, Is.EqualTo(1f));
            Assert.That(view.menuPanel.blocksRaycasts, Is.True);
        }

        private IEnumerator LoadTitle()
        {
            yield return SceneManager.LoadSceneAsync(MainMenuActions.TitleScenePath);
            yield return WaitForMenuScene(MainMenuActions.TitleScenePath);
        }

        private IEnumerator WaitForMenuScene(string path)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (SceneManager.GetActiveScene().path != path)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                AssertHasDisplayCamera();
                yield return null;
            }
            string previous = MainMenuActions.TitleScenePath;
            if (path == MainMenuActions.TitleScenePath)
            {
                previous = MainMenuActions.ModeSelectionScenePath;
            }
            while (SceneManager.GetSceneByPath(previous).IsValid())
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            yield return null;
            view = MainMenuCompositionRoot.InScene(SceneManager.GetActiveScene()).View;
            screen = view.GetComponent<MainMenuScreen>();
            Assert.That(Array.FindAll(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None),
                item => item.isActiveAndEnabled), Has.Length.EqualTo(1));
            Assert.That(Array.FindAll(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None),
                item => item.isActiveAndEnabled), Has.Length.EqualTo(1));
        }

        private IEnumerator ReturnToTitle()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitForMenuScene(MainMenuActions.TitleScenePath);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator SlidingDoorsRevealDummiesAndReverseWithoutStartingEarly()
        {
            yield return LoadTitle();
            var entrance = view;
            var leftDoor = view.entrance.leftDoor;
            var rightDoor = view.entrance.rightDoor;
            var sky = view.GetComponent<TitleSkyView>();
            var entranceScreen = view.GetComponent<MainMenuEntranceScreen>();
            var cloudPosition = sky.clouds[1].anchoredPosition;
            var birdPosition = sky.birds[0].rectTransform.anchoredPosition;
            var birdFrame = sky.birds[0].sprite;
            bool flapped = false;
            double idleDeadline = Time.realtimeSinceStartupAsDouble + 1.2;
            while (Time.realtimeSinceStartupAsDouble < idleDeadline)
            {
                flapped |= sky.birds[0].sprite != birdFrame;
                Assert.That(entranceScreen.State, Is.EqualTo(MainMenuEntranceScreen.EntranceState.Idle));
                Assert.That(entrance.entrance.Opening, Is.Zero);
                yield return null;
            }
            Assert.That(flapped, Is.True, "Birds must flap while waiting for input.");
            Assert.That(sky.clouds[1].anchoredPosition, Is.Not.EqualTo(cloudPosition));
            Assert.That(sky.birds[0].rectTransform.anchoredPosition, Is.Not.EqualTo(birdPosition));
            Assert.That(sky.logo.alpha, Is.EqualTo(1));
            Assert.That(view.startPrompt.gameObject.activeInHierarchy, Is.False);
            Assert.That(SceneManager.GetSceneByPath(MainMenuActions.ModeSelectionScenePath).isLoaded, Is.False);
            Directory.CreateDirectory("Logs/sky-intro");
            Capture("Logs/sky-intro/idle.png");
            yield return Click(sky.start);
            Assert.That(entranceScreen.State, Is.EqualTo(MainMenuEntranceScreen.EntranceState.CameraDescending));
            sky.start.onClick.Invoke();
            view.entranceTrigger.onClick.Invoke();
            float previousDescent = 0f;
            bool captured = false;
            double descentDeadline = Time.realtimeSinceStartupAsDouble + 5;
            while (entranceScreen.State == MainMenuEntranceScreen.EntranceState.CameraDescending)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(descentDeadline));
                Assert.That(sky.Descent, Is.GreaterThanOrEqualTo(previousDescent));
                Assert.That(entrance.entrance.Opening, Is.Zero, "Doors must wait for camera descent.");
                if (!captured && sky.Descent >= .5f)
                {
                    Capture("Logs/sky-intro/descending.png");
                    captured = true;
                }
                previousDescent = sky.Descent;
                yield return null;
            }
            Assert.That(sky.Descent, Is.EqualTo(1));
            Assert.That(sky.logo.alpha, Is.Zero);
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (entrance.entrance.Opening <= 0)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(MainMenuActions.TitleScenePath));
            var mode = MainMenuCompositionRoot.InScene(SceneManager.GetSceneByPath(MainMenuActions.ModeSelectionScenePath));
            var character = mode.View.modeSelection.character;
            Assert.That(mode.View.modeSelection.InputEnabled, Is.False);
            Assert.That(mode.GetComponent<MainMenuScreen>().CanSubmit, Is.False);
            Assert.That(entrance.entrance.leftDoor, Is.SameAs(leftDoor));
            Assert.That(entrance.entrance.rightDoor, Is.SameAs(rightDoor));
            Assert.That(entrance.entrance.interiorBackdrop.alpha, Is.Zero);
            Assert.That(entrance.entrance.frame.alpha, Is.EqualTo(1));
            entrance.logo.onClick.Invoke();
            mode.GetComponent<MainMenuScreen>().OnSubmit(null);
            yield return WaitForMenuScene(MainMenuActions.ModeSelectionScenePath);
            Assert.That(view.modeSelection.character, Is.SameAs(character),
                "Door reveal must use the same authored room and actors as the active scene.");
            Assert.That(entrance == null, Is.True);
            Assert.That(view.modeSelection.InputEnabled, Is.True);
            yield return ReturnToTitle();
            Assert.That(view.entrance.Opening, Is.Zero);
            Assert.That(view.modeSelection, Is.Null);
            Assert.That(view.menuPanel, Is.Null);
            Assert.That(view.GetComponent<MainMenuEntranceScreen>().State,
                Is.EqualTo(MainMenuEntranceScreen.EntranceState.Idle));
            Assert.That(view.GetComponent<TitleSkyView>().Descent, Is.Zero);
            Assert.That(view.GetComponent<TitleSkyView>().logo.alpha, Is.EqualTo(1));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator EscapeCancelsEntryAndTravelWithoutReplacingTheStage()
        {
            yield return LoadTitle();
            var doorwayApproach = view.entrance.doorwayApproach;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var sky = view.GetComponent<TitleSkyView>();
                yield return Click(sky.start);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(view.GetComponent<MainMenuEntranceScreen>().State,
                    Is.EqualTo(MainMenuEntranceScreen.EntranceState.Idle));
                Assert.That(sky.Descent, Is.Zero);
                Assert.That(sky.start.interactable, Is.True);
                yield return Click(view.GetComponent<TitleSkyView>().start);
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (view.entrance.Opening <= 0)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitForMenuScene(MainMenuActions.TitleScenePath);
                Assert.That(view.entrance.Opening, Is.Zero);
                Assert.That(view.entrance.doorwayApproach, Is.SameAs(doorwayApproach));
                yield return OpenMenu();
                yield return WaitForModeMotion(view.modeSelection);
                var character = view.modeSelection.character;
                var typography = view.modeSelection.typography[1];
                var titlePosition = ((RectTransform)typography.titleGroup.transform).anchoredPosition;
                yield return Click(view.modeSelection.next);
                yield return new WaitForSecondsRealtime(.20f);
                Assert.That(view.modeSelection.IsMoving, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(view.modeSelection.IsMoving, Is.False);
                yield return WaitForMenuScene(MainMenuActions.TitleScenePath);
                Assert.That(character == null, Is.True, "Departing actors must be destroyed with their scene.");
                yield return OpenMenu();
                yield return WaitForModeMotion(view.modeSelection);
                Assert.That(view.modeSelection.SelectedIndex, Is.EqualTo(1));
                Assert.That(view.modeSelection.travelCover.gameObject.activeSelf, Is.False);
                Assert.That(((RectTransform)view.modeSelection.typography[1].titleGroup.transform).anchoredPosition,
                    Is.EqualTo(titlePosition));
                yield return ReturnToTitle();
                yield return OpenMenu();
                Assert.That(UnityEngine.Object.FindObjectsByType<DojoModeView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator PlayOpensDoorsWithLogoHalvesAttachedUntilFullyOutsideTheDoorway()
        {
            // The entrance now completes before choosing a mode. Verify movement and contact on authored objects.
            var actions = new Actions();
            screen.Initialize(actions);
            yield return OpenMenu();
            var mode = view.modeSelection;
            var character = mode.character;
            var heading = mode.heading;
            int contacts = mode.ContactCount;
            double deadline = Time.realtimeSinceStartupAsDouble + 4;
            while (mode.ContactCount == contacts)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            Assert.That(mode.props[1].localRotation, Is.EqualTo(Quaternion.identity),
                "The target must not react before the contact has propagated.");
            yield return new WaitForSecondsRealtime(.065f);
            Assert.That(mode.props[1].localRotation, Is.Not.EqualTo(Quaternion.identity));
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(mode.IsMoving, Is.True);
                yield return new WaitForSecondsRealtime(.13f);
                Assert.That(mode.character.sprite, Is.Not.EqualTo(mode.poses[0]));
                view.modeSelection.next.onClick.Invoke();
                screen.OnSubmit(null);
                Assert.That(actions.Starts, Is.Zero);
                while (mode.TravelProgress < .35f)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                Assert.That(Mathf.Abs(mode.middleLayers[2].anchoredPosition.x),
                    Is.GreaterThan(Mathf.Abs(mode.farLayers[2].anchoredPosition.x)));
                Assert.That(Mathf.Abs(mode.nearLayers[2].anchoredPosition.x),
                    Is.GreaterThan(Mathf.Abs(mode.middleLayers[2].anchoredPosition.x)));
                Assert.That(mode.stages[1].alpha, Is.EqualTo(1));
                Assert.That(mode.stages[2].alpha, Is.EqualTo(1));
                Assert.That(mode.typography[1].titleGroup.alpha, Is.Zero,
                    "The departing title must leave before the new space arrives.");
                Assert.That(mode.typography[2].titleGroup.alpha, Is.Zero);
                Assert.That(mode.heading, Is.SameAs(heading), "Do not replace visible copy during departure.");
                Capture("Logs/hanok-mode-travel-test.png");
                while (mode.TravelProgress < .71f)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                Assert.That(mode.typography[2].titleGroup.alpha, Is.GreaterThan(.5f));
                Assert.That(mode.typography[2].copyGroup.alpha,
                    Is.LessThan(mode.typography[2].titleGroup.alpha), "Description follows the title on a later beat.");
                deadline = Time.realtimeSinceStartupAsDouble + 2;
                while (mode.IsMoving)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                Assert.That(mode.SelectedIndex, Is.EqualTo(2));
                Assert.That(mode.heading.text, Is.EqualTo("실력 검정"));
                Assert.That(mode.description.text, Is.EqualTo("하산할 준비가 되었다고 생각하나?"));
                Assert.That(mode.character, Is.SameAs(character));
                Assert.That(mode.typography[2].copyGroup.alpha, Is.EqualTo(1));
                Assert.That(mode.typography[2].titleGroup.transform.localScale, Is.EqualTo(Vector3.one));
                contacts = mode.ContactCount;
                var sageVertices = ReadVertices(mode.sageMotion);
                yield return new WaitForSecondsRealtime(.45f);
                AssertGroundedMotion(mode.sageMotion, sageVertices);
                Assert.That(mode.sageGesture.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(mode.ContactCount, Is.EqualTo(contacts), "Summit greeting must not punch a prop.");
                Capture("Logs/emergency-summit.png");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator EditorStaysBehindDoorsAndOpensFromInsideTheDojo()
        {
            yield return LoadTitle();
            Assert.That(view.editor, Is.Null);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(MainMenuActions.TitleScenePath));
            yield return OpenMenu();
            Assert.That(view.editor.transform.IsChildOf(view.menuPanel.transform), Is.True);
            Assert.That(view.editor.gameObject.activeInHierarchy, Is.False);
            yield return ReturnToTitle();
            Assert.That(view.editor, Is.Null);
            yield return OpenMenu();
            for (int i = 0; i < 2; i++)
            {
                yield return Click(view.modeSelection.next);
                yield return WaitForModeMotion(view.modeSelection);
            }
            yield return SubmitStart();
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (SceneManager.GetActiveScene().path != ChartEditorMenu.ScenePath)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }


        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator SettingsRequestsSettingsWithoutStartingTheGame()
        {
            var actions = new Actions();
            screen.Initialize(actions);
            yield return OpenMenu();
            yield return Click(view.settings);
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(actions.Settings, Is.EqualTo(1));
            Assert.That(actions.Starts, Is.Zero);
            Assert.That(actions.Quits, Is.Zero);
            Assert.That(view.quitPopup.activeSelf, Is.False);
            Assert.That(SceneManager.GetActiveScene().path, Does.EndWith("/ModeSelection.unity"));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator QuitPopupBlocksBackgroundAndNoRestoresMenu()
        {
            var actions = new Actions();
            screen.Initialize(actions);
            yield return OpenMenu();

            for (int repeat = 0; repeat < 2; repeat++)
            {
                yield return Click(view.quit);
            yield return new WaitForSecondsRealtime(.45f);
                Assert.That(view.quitPopup.activeSelf, Is.True);
                Assert.That(
                    screen.CanSubmit || view.settings.interactable || view.quit.interactable,
                    Is.False
                );
                yield return ClickAt(new Vector2(Screen.width * .2f, Screen.height * .8f));
                Assert.That(actions.Starts, Is.Zero);
                Assert.That(view.quitPopup.activeSelf, Is.True);
                yield return Click(view.cancelQuit);
                Assert.That(view.quitPopup.activeSelf, Is.False);
                Assert.That(
                    screen.CanSubmit && view.settings.interactable && view.quit.interactable,
                    Is.True
                );
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator YesRequestsQuitExactlyOnceWithoutStarting()
        {
            var actions = new Actions();
            screen.Initialize(actions);
            yield return OpenMenu();
            yield return Click(view.quit);
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(
                view.quitPopup.activeSelf,
                Is.True,
                "Quit popup must open before confirming."
            );
            yield return Click(view.confirmQuit);
            view.confirmQuit.onClick.Invoke();
            Assert.That(actions.Quits, Is.EqualTo(1));
            Assert.That(actions.Starts, Is.Zero);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator LogoExpandsMenuAndPlayStartsOnceWithoutDuplicateListeners()
        {
            var oldActions = new Actions();
            screen.Initialize(oldActions);

            var actions = new Actions();
            screen.Initialize(actions);
            yield return ClickAt(new Vector2(Screen.width * .3f, Screen.height * .7f));
            Assert.That(actions.Starts, Is.Zero);
            yield return new WaitForSecondsRealtime(MainMenuScreen.MenuExpansionSeconds + .1f);
            Assert.That(view.menuPanel.blocksRaycasts, Is.True);
            yield return SubmitStart();
            yield return WaitForStart(actions);
            screen.OnSubmit(null);
            Assert.That(actions.Starts, Is.EqualTo(1));
            Assert.That(oldActions.Starts, Is.Zero);
            Assert.That(screen.CanSubmit, Is.False);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator PlayDismissesMenuImmediatelyWithoutWaitingForBoardDelay()
        {
            var actions = new Actions();
            screen.Initialize(actions);
            yield return OpenMenu();
            Assert.That(screen.GetComponent<DojoBoardBreak>(), Is.Null);
            Assert.That(view.menuPanel.alpha, Is.EqualTo(1f));
            screen.OnSubmit(null);
            Assert.That(view.modeSelection.roomRoot.localScale.x, Is.GreaterThan(1f),
                "Play must begin the approach immediately.");
            Assert.That(screen.CanSubmit, Is.False);
            Assert.That(actions.Starts, Is.Zero);
            yield return WaitForStart(actions);
            Assert.That(actions.Starts, Is.EqualTo(1));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator TitlePreparesSelectionWithoutShowingItOrStealingInput()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            double deadline = Time.realtimeSinceStartupAsDouble + 10d;
            while (!flow.IsSelectionPrepared)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                Assert.That(SceneManager.GetActiveScene().path, Does.EndWith("/ModeSelection.unity"));
                yield return null;
            }

            var selection = UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>();
            Assert.That(selection.enabled, Is.False);
            Assert.That(selection.GetComponent<Canvas>().sortingOrder, Is.LessThan(view.GetComponent<Canvas>().sortingOrder));
            Assert.That(selection.GetComponent<CanvasGroup>().interactable, Is.False);
            Assert.That(selection.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.gameObject.scene.path, Does.EndWith("/ModeSelection.unity"));
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.SameAs(screen.gameObject));
            Assert.That(Array.FindAll(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None),
                item => item.isActiveAndEnabled), Has.Length.EqualTo(1));
            Assert.That(DojoEntranceTransition.Active, Is.Null);
            yield return OpenMenu();
            Assert.That(screen.CanSubmit, Is.True);
        }

        [UnityTest]
        [Category("Smoke")]
        [Category("SceneIntegration")]
        public IEnumerator StartLoadsBootstrapThenExistingSongSelection()
        {
            yield return LoadTitle();
            bool bootstrapLoaded = false;
            int selectionLoads = 0;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> loaded = (scene, mode) =>
            {
                if (scene.path == AppFlowController.SelectionPath)
                {
                    selectionLoads++;
                }
                if (scene.path == MainMenuActions.BootstrapScenePath)
                {
                    bootstrapLoaded = true;
                    AssertHasDisplayCamera();
                }
            };
            SceneManager.sceneLoaded += loaded;

            try
            {
                yield return OpenMenu();
                double preparedDeadline = Time.realtimeSinceStartupAsDouble + 20;
                while (!UnityEngine.Object.FindFirstObjectByType<AppFlowController>().IsSelectionPrepared)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(preparedDeadline));
                    yield return null;
                }
                int preparedHandle = SceneManager.GetSceneByPath(AppFlowController.SelectionPath).handle;
                selectionLoads = 0;
                Assert.That(view.editor.gameObject.activeInHierarchy, Is.False);
                AssertSubmitHasNoArtwork();

                var keyboard = InputSystem.AddDevice<Keyboard>();
                try
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                    yield return null;
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                }
                finally
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                double deadline = Time.realtimeSinceStartupAsDouble + 20;

                while (SceneManager.GetActiveScene().path != AppFlowController.SelectionPath
                    || UnityEngine.Object.FindFirstObjectByType<AppFlowController>().Transitioning)
                {
                    AssertHasDisplayCamera();
                    Assert.That(
                        Time.realtimeSinceStartupAsDouble,
                        Is.LessThan(deadline),
                        "Song selection transition timed out."
                    );
                    yield return null;
                }

                while (DojoEntranceTransition.Active)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    AssertHasDisplayCamera();
                    yield return null;
                }

                Assert.That(UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>().enabled, Is.True);
                var selectionView = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
                Assert.That(selectionView.practice.gameObject.activeInHierarchy, Is.True);
                Assert.That(selectionView.libraryPanel.activeSelf, Is.False);
                Assert.That(selectionView.practice.rig.jacket.sprite, Is.SameAs(selectionView.album.sprite));
                var backdrop = selectionView.practice.rig.jacket;
                Assert.That(backdrop, Is.Not.Null);
                Assert.That(selectionView.entrancePanels, Is.EqualTo(new[] { (RectTransform)selectionView.practice.interfaceGroup.transform }));
                Canvas.ForceUpdateCanvases();
                var selectionCorners = new Vector3[4];
                backdrop.rectTransform.GetWorldCorners(selectionCorners);


                AssertHasDisplayCamera();
                Assert.That(bootstrapLoaded, Is.False,
                    "The title must enter selection directly, avoiding an extra scene activation stall.");
                Assert.That(selectionLoads, Is.Zero, "Play must reuse the warmed scene without loading it again.");
                Assert.That((int)SceneManager.GetActiveScene().handle, Is.EqualTo(preparedHandle));
                Assert.That(SceneManager.GetSceneByPath(MainMenuActions.ModeSelectionScenePath).isLoaded, Is.False,
                    "The hidden title must finish unloading before the entrance releases selection input.");
                Assert.That(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None),
                    Has.Length.EqualTo(1));
                Assert.That(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
                Assert.That(
                    SceneManager.GetActiveScene().path,
                    Does.EndWith("/SongSelection.unity")
                );
            }
            finally
            {
                SceneManager.sceneLoaded -= loaded;
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator DirectSelectionEntryUsesPracticeWithoutAModeSelectionOverride()
        {
            foreach (string path in new[] { MainMenuActions.BootstrapScenePath, AppFlowController.SelectionPath })
            {
                var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
                UnityEngine.Object.Destroy(flow.gameObject);
                yield return null;
                yield return SceneManager.LoadSceneAsync(path);

                double deadline = Time.realtimeSinceStartupAsDouble + 20;
                while (SceneManager.GetActiveScene().path != AppFlowController.SelectionPath)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), path);
                    yield return null;
                }
                yield return null;
                flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
                while (flow.Transitioning)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), path);
                    yield return null;
                }

                var selectionView = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
                Assert.That(selectionView.practice.gameObject.activeInHierarchy, Is.True, path);
                Assert.That(selectionView.libraryPanel.activeSelf, Is.False, path);
                Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
                string song = flow.Selection.SelectedSongId;
                Assert.That(selectionView.Rows.Count, Is.GreaterThan(1), path);
                Assert.That(selectionView.practice.next.interactable, Is.True, path);
                int contacts = selectionView.practice.SelectionContactCount;
                // The second scene owns a new UI input module. Send a new pointer position
                // before clicking the same screen coordinates used by the first scene.
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.zero });
                yield return null;
                yield return Click(selectionView.practice.next);
                while (selectionView.practice.IsStriking)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), path);
                    yield return null;
                }
                Assert.That(selectionView.practice.SelectionContactCount, Is.EqualTo(contacts + 1), path);
                Assert.That(flow.Selection.SelectedSongId, Is.Not.EqualTo(song), path);
                Assert.That(selectionView.practice.rig.title.text, Is.EqualTo(selectionView.songTitle.text));
            }
        }

        [UnityTest]
        [Category("Compatibility")]
        [Category("SceneIntegration")]
        public IEnumerator BootstrapFallbackStillStartsWithoutATitleView()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            double deadline = Time.realtimeSinceStartupAsDouble + 10d;
            while (!flow.IsSelectionPrepared)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }

            bool bootstrapLoaded = false;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> loaded = (scene, mode) =>
            {
                if (scene.path == MainMenuActions.BootstrapScenePath)
                {
                    bootstrapLoaded = true;
                }
            };
            SceneManager.sceneLoaded += loaded;
            try
            {
                new MainMenuActions(flow.Settings).StartGame();
                while (!bootstrapLoaded || SceneManager.GetActiveScene().path != AppFlowController.SelectionPath)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                yield return null;
                Assert.That(bootstrapLoaded, Is.True);
                Assert.That(UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>().enabled, Is.True);
                Assert.That(DojoEntranceTransition.Active, Is.Null);
            }
            finally
            {
                SceneManager.sceneLoaded -= loaded;
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator CaptureMenuAndKoreanQuitPopup()
        {
            yield return LoadTitle();
            Capture("Logs/main-menu.png");
            Capture("Logs/main-menu-1920.png", 1920, 1080);
            Capture("Logs/main-menu-4x3.png", 1024, 768);
            Capture("Logs/main-menu-16x10.png", 1920, 1200);
            Capture("Logs/main-menu-ultrawide.png", 2560, 1080);
            yield return OpenMenu();
            Capture("Logs/main-menu-expanded.png");
            Capture("Logs/main-menu-expanded-4x3.png", 1024, 768);
            Capture("Logs/main-menu-expanded-ultrawide.png", 2560, 1080);
            for (int i = 0; i < 4; i++)
            {
                yield return Click(view.modeSelection.next);
                double deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (view.modeSelection.IsMoving)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                Capture("Logs/arcade-mode-" + view.modeSelection.SelectedIndex + ".png");
                Capture("Logs/arcade-mode-" + view.modeSelection.SelectedIndex + "-4x3.png", 1024, 768);
            }
            yield return Click(view.quit);
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(view.quitPopup.activeSelf, Is.True);
            Capture("Logs/main-menu-quit.png");
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator LogoCanCollapseMenuAndHiddenButtonsCannotStart()
        {
            yield return OpenMenu();
            var character = view.modeSelection.character;
            var start = screen;
            yield return ReturnToTitle();
            Assert.That(character == null, Is.True);
            Assert.That(start == null, Is.True);
            Assert.That(view.logo.targetGraphic.raycastTarget, Is.True);
            Assert.That(view.modeSelection, Is.Null);
            yield return OpenMenu();
            Assert.That(view.modeSelection.SelectedIndex, Is.EqualTo(1));
            Assert.That(screen.CanSubmit, Is.True);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator TitleSettingsBackReturnsToTitle()
        {
            yield return OpenMenu();
            yield return Click(view.settings);
            yield return new WaitForSecondsRealtime(.45f);

            double deadline = Time.realtimeSinceStartupAsDouble + 20d;

            while (!UnityEngine.Object.FindFirstObjectByType<SettingsScreen>())
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }

            yield return null;

            var settingsView = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            Assert.That(SceneManager.GetActiveScene().path,
                Is.EqualTo(MainMenuActions.ModeSelectionScenePath));
            Assert.That(SceneManager.GetSceneByPath(AppFlowController.SettingsPath).isLoaded,
                Is.False);
            Assert.That(settingsView.backdrop, Is.Not.Null);
            Assert.That(settingsView.backdrop.activeSelf, Is.False);
            Assert.That(view.modeSelection.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.entrance, Is.Null);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }

            while (settingsView.gameObject.activeInHierarchy)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }

            yield return null;
            Assert.That(UnityEngine.Object.FindFirstObjectByType<MainMenuView>(), Is.SameAs(view));
            Assert.That(settingsView.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.menuPanel.alpha, Is.EqualTo(1f));
            Assert.That(
                view.menuPanel.gameObject.activeSelf,
                Is.True,
                "Popup preserves the expanded title menu."
            );
            Assert.That(screen.enabled, Is.True);
            Assert.That(screen.CanSubmit, Is.True);
            yield return null;
            Assert.That(view.menuPanel.gameObject.activeSelf, Is.True);
        }

        private static void AssertHasDisplayCamera()
        {
            Assert.That(
                System.Array.Exists(
                    Camera.allCameras,
                    camera =>
                        camera.isActiveAndEnabled
                        && camera.targetDisplay == 0
                        && !camera.targetTexture
                ),
                Is.True,
                "Display 1 must have a rendering camera throughout the transition."
            );
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator BreakingBoardDefersActionAndBlocksOtherButtons()
        {
            var actions = new Actions();
            screen.Initialize(actions);
            yield return OpenMenu();
            view.settings.onClick.Invoke();
            screen.OnSubmit(null);
            view.quit.onClick.Invoke();
            view.settings.onClick.Invoke();
            Assert.That(actions.Settings, Is.Zero);
            Assert.That(actions.Starts, Is.Zero);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(view.settings.GetComponent<Image>().enabled, Is.True);
            Assert.That(view.settings.GetComponentInChildren<DojoBoardFragments>(), Is.Null);
            Assert.That(view.settings.interactable, Is.False);
            Capture("Logs/main-menu-breaking.png");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(actions.Settings, Is.EqualTo(1));
            Assert.That(actions.Starts, Is.Zero);
            Assert.That(view.quitPopup.activeSelf, Is.False);

            actions.Transitioning = false;
            screen.ResumeFromSettings();
            Assert.That(view.settings.GetComponent<Image>().enabled, Is.True);
            Assert.That(screen.CanSubmit, Is.True);
        }

        [UnityTest]
        [Category("Compatibility")]
        [Category("SceneIntegration")]
        public IEnumerator EntranceWaitsForReadyAndRestoresSelectionInput()
        {
            yield return LoadTitle();
            var transition = DojoEntranceTransition.Begin(
                view.entrance, view.doorSlide, view.startPrompt.font, () => { });
            yield return new WaitForSecondsRealtime(DojoEntranceTransition.DoorOpeningSeconds + .2f);
            Assert.That(DojoEntranceTransition.Active, Is.SameAs(transition));
            Assert.That(transition.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(transition.GetComponentInChildren<DojoEntranceView>().frame.alpha, Is.EqualTo(1f));
            Assert.That(transition.GetComponentInChildren<DojoEntranceView>().Opening, Is.Zero,
                "Door motion must wait for selection loading and UI initialization.");

            var host = new GameObject("Selection readiness test", typeof(CanvasGroup));
            var selection = host.AddComponent<SongSelectionScreen>();
            var panel = new GameObject("Selection panel", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            panel.SetParent(host.transform, false);
            panel.anchoredPosition = new Vector2(10f, 20f);
            host.AddComponent<SongSelectionView>().entrancePanels = new[] { panel };
            DojoEntranceTransition.SelectionReady(selection);
            Assert.That(selection.enabled, Is.False);
            Assert.That(host.GetComponent<CanvasGroup>().interactable, Is.False);
            Assert.That(host.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.Zero);
            yield return new WaitForSecondsRealtime(DojoEntranceTransition.DoorOpeningSeconds + .4f);
            Assert.That(DojoEntranceTransition.Active, Is.SameAs(transition));
            Assert.That(transition.GetComponentInChildren<DojoEntranceView>().transform.localScale.x,
                Is.InRange(1.001f, DojoEntranceView.InteriorScale));
            Assert.That(transition.GetComponentInChildren<DojoEntranceView>().frame.alpha, Is.EqualTo(1f));
            Assert.That(transition.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(selection.enabled, Is.False);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(host.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.InRange(.01f, .99f));
            Assert.That(panel.anchoredPosition.y, Is.LessThan(20f));
            Assert.That(selection.enabled, Is.False);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(DojoEntranceTransition.Active, Is.Null);
            Assert.That(selection.enabled, Is.True);
            Assert.That(host.GetComponent<CanvasGroup>().interactable, Is.True);
            Assert.That(host.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(Vector2.Distance(panel.anchoredPosition, new Vector2(10f, 20f)), Is.LessThan(.001f));
            Assert.That(panel.localScale, Is.EqualTo(Vector3.one));
            Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            UnityEngine.Object.Destroy(host);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator PlaqueRejectsCornersAndKeyboardCanExpandAndStart()
        {
            yield return LoadTitle();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(view.GetComponent<TitleSkyView>().start.gameObject);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitForMenuScene(MainMenuActions.ModeSelectionScenePath);
                var actions = new Actions();
                screen.Initialize(actions);
                Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,
                    Is.EqualTo(screen.gameObject));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitForStart(actions);
                Assert.That(actions.Starts, Is.EqualTo(1));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        [Category("Compatibility")]
        [Category("SceneIntegration")]
        public IEnumerator EntranceTimeoutOffersRecoveryAndCleansUp()
        {
            bool recovered = false;
            yield return LoadTitle();
            var transition = DojoEntranceTransition.Begin(
                view.entrance, view.doorSlide, view.startPrompt.font, () => recovered = true);
            typeof(DojoEntranceTransition).GetField(
                "readyTimeout",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            ).SetValue(transition, .05f);
            yield return new WaitForSecondsRealtime(DojoEntranceTransition.DoorOpeningSeconds + .2f);
            Assert.That(transition.Failed, Is.True);
            transition.GetComponentInChildren<Button>().onClick.Invoke();
            yield return null;
            Assert.That(recovered, Is.True);
            Assert.That(DojoEntranceTransition.Active, Is.Null);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator ChallengeStageEntersExistingChallengeSelection()
        {
            yield return OpenMenu();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            double preparedDeadline = Time.realtimeSinceStartupAsDouble + 20;
            while (!flow.IsSelectionPrepared)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(preparedDeadline));
                yield return null;
            }
            int preparedHandle = SceneManager.GetSceneByPath(AppFlowController.SelectionPath).handle;

            yield return Click(view.modeSelection.next);
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (view.modeSelection.IsMoving)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            yield return SubmitStart();
            deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (SceneManager.GetActiveScene().path != AppFlowController.SelectionPath)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            yield return null;
            Assert.That(flow.Rules.Snapshot.Mode, Is.EqualTo(RhythmDojo.Gameplay.PlayMode.Challenge));
            var selectionView = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            Assert.That(selectionView.modeLabel.text, Does.Contain("Challenge"));
            Assert.That(selectionView.practice.heading.text, Is.EqualTo("실력 검정"));
            Assert.That(selectionView.libraryPanel.activeSelf, Is.False);
            Assert.That((int)SceneManager.GetActiveScene().handle, Is.EqualTo(preparedHandle));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator LessonRespondsToRhythmAndCompletesWithoutLeavingTitle()
        {
            yield return OpenMenu();
            var character = view.modeSelection.character;
            var freePosition = view.modeSelection.characterMarks[1].anchoredPosition;
            yield return Click(view.modeSelection.previous);
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (view.modeSelection.IsMoving)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            Assert.That(view.modeSelection.character, Is.SameAs(character));
            Assert.That(Vector2.Distance(character.rectTransform.anchoredPosition, freePosition), Is.GreaterThan(80));
            int contacts = view.modeSelection.ContactCount;
            var teacherVertices = ReadVertices(view.modeSelection.teacherMotion);
            yield return new WaitForSecondsRealtime(.16f);
            AssertGroundedMotion(view.modeSelection.teacherMotion, teacherVertices);
            Assert.That(view.modeSelection.teacherGesture.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(view.modeSelection.props[0].localRotation, Is.EqualTo(Quaternion.identity),
                "The target must wait for the player's answer to the senior.");
            while (view.modeSelection.IsArriving)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            Assert.That(view.modeSelection.ContactCount, Is.EqualTo(contacts + 1));
            Assert.That(view.modeSelection.description.text, Is.EqualTo("처음이지? 내가 옆에서 박자를 짚어 줄게."));
            Assert.That(view.modeSelection.lessonCue.gameObject.activeSelf, Is.False,
                "The lesson cue should appear only after starting the lesson.");
            Capture("Logs/renewal-lesson.png");
            yield return SubmitStart();
            Assert.That(view.modeSelection.lessonCue.gameObject.activeSelf, Is.True);
            double startTime = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < 4; i++)
            {
                while (Time.realtimeSinceStartupAsDouble < startTime + i * .75)
                {
                    yield return null;
                }
                yield return SubmitStart();
            }
            Assert.That(view.modeSelection.LessonHits, Is.EqualTo(4));
            Assert.That(view.modeSelection.traineeIdle.IsPlaying, Is.False);
            Assert.That(view.modeSelection.teacherIdle.IsPlaying, Is.False);
            Assert.That(view.modeSelection.detail.text, Does.Contain("기초 수련 완료"));
            Assert.That(view.modeSelection.lessonCue.text, Does.Contain("잘했다"));
            foreach (var mark in view.modeSelection.lessonMarks)
            {
                Assert.That(mark.color.r, Is.EqualTo(1));
            }
            Assert.That(SceneManager.GetActiveScene().path, Does.EndWith("/ModeSelection.unity"));
            Capture("Logs/emergency-lesson.png");
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator UndefinedFourthModeCannotStartButNavigationRemainsAvailable()
        {
            // Retain the historical case name: the formerly undefined slot now opens the editor.
            yield return OpenMenu();
            var character = view.modeSelection.character;
            var backdrop = view.modeSelection.farLayers[3].GetComponent<Image>().sprite;
            for (int i = 0; i < 2; i++)
            {
                yield return Click(view.modeSelection.next);
                double deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (view.modeSelection.IsMoving)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                yield return null;
            }
            Assert.That(view.modeSelection.SelectedIndex, Is.EqualTo(3));
            Assert.That(screen.CanSubmit, Is.True);
            Assert.That(view.modeSelection.heading.text, Is.EqualTo("율보 편찬"));
            Assert.That(view.modeSelection.description.text, Is.EqualTo("속세에서는 그것을 에디터라 부른다더라"));
            Assert.That(character.enabled, Is.False, "The manuscript close-up must not have a fighting figure over it.");
            Capture("Logs/scribing-menu-1920.png", 1920, 1080);
            Capture("Logs/scribing-menu-4x3.png", 1024, 768);
            Capture("Logs/scribing-menu-ultrawide.png", 2560, 1080);
            yield return Click(view.modeSelection.next);
            double navigationDeadline = Time.realtimeSinceStartupAsDouble + 3;
            while (view.modeSelection.IsMoving)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(navigationDeadline));
                yield return null;
            }
            yield return null;
            Assert.That(view.modeSelection.SelectedIndex, Is.Zero);
            Assert.That(screen.CanSubmit, Is.True);
            Assert.That(character.enabled, Is.True);
            yield return Click(view.modeSelection.previous);
            yield return WaitForModeMotion(view.modeSelection);
            Assert.That(view.modeSelection.character, Is.SameAs(character));
            Assert.That(view.modeSelection.farLayers[3].GetComponent<Image>().sprite, Is.SameAs(backdrop));
            Assert.That(character.enabled, Is.False);

            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(screen.gameObject);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                double deadline = Time.realtimeSinceStartupAsDouble + 20;
                while (SceneManager.GetActiveScene().path != ChartEditorMenu.ScenePath)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator FailedModeEntryRestoresControlsAndCanRetry()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var field = typeof(AppFlowController).GetField("selectionPreparation",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var preparation = (PreparedSelectionScene)field.GetValue(flow);
            flow.CancelSelectionPreparation();
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (preparation.IsBusy)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            preparation.Dispose();
            bool ready = false;
            preparation = new PreparedSelectionScene(flow, () => ready, readyTimeout: .3);
            field.SetValue(flow, preparation);
            yield return OpenMenu();
            yield return SubmitStart();
            deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!view.modeSelection.detail.text.Contains("다시 수련 시작"))
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            Assert.That(screen.CanSubmit, Is.True);
            ready = true;
            yield return SubmitStart();
            while (SceneManager.GetActiveScene().path != AppFlowController.SelectionPath)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator StandingFiguresStayGroundedAndUnusedTargetsStayAtRest()
        {
            yield return OpenMenu();
            var mode = view.modeSelection;
            mode.traineeIdle.enabled = false;
            mode.teacherIdle.enabled = false;
            mode.sageIdle.enabled = false;
            var character = mode.character;
            for (int i = 0; i < 3; i++)
            {
                if (i == 1)
                {
                    yield return Click(mode.previous);
                }
                else if (i == 2)
                {
                    yield return Click(mode.next);
                    yield return WaitForModeMotion(mode);
                    yield return Click(mode.next);
                }
                yield return WaitForModeMotion(mode);
                // Let the physical target finish settling after the one arrival demonstration.
                yield return new WaitForSecondsRealtime(.9f);
                var vertices = ReadVertices(mode.traineeMotion);
                var position = character.rectTransform.anchoredPosition;
                var companionPosition = mode.companions[mode.SelectedIndex].anchoredPosition;
                int contacts = mode.ContactCount;
                double until = Time.realtimeSinceStartupAsDouble + 1.2;
                while (Time.realtimeSinceStartupAsDouble < until)
                {
                    Assert.That(character.rectTransform.anchoredPosition, Is.EqualTo(position));
                    Assert.That(character.rectTransform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(mode.companions[mode.SelectedIndex].anchoredPosition, Is.EqualTo(companionPosition));
                    Assert.That(mode.companions[mode.SelectedIndex].localRotation, Is.EqualTo(Quaternion.identity));
                    Assert.That(mode.props[mode.SelectedIndex].localRotation, Is.EqualTo(Quaternion.identity));
                    if (mode.banners[mode.SelectedIndex])
                    {
                        Assert.That(mode.banners[mode.SelectedIndex].localRotation, Is.EqualTo(Quaternion.identity));
                    }
                    Assert.That(mode.ContactCount, Is.EqualTo(contacts), "Idle must never fabricate a strike.");
                    yield return null;
                }
                AssertGroundedMotion(mode.traineeMotion, vertices);
                Assert.That(mode.character, Is.SameAs(character));
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator PracticeTargetWaitsForContactSettlesAndCancelsPendingStrike()
        {
            yield return OpenMenu();
            var mode = view.modeSelection;
            yield return WaitForModeMotion(mode);
            yield return new WaitForSecondsRealtime(.9f);
            int contacts = mode.ContactCount;
            yield return Click(mode.practiceTarget);
            Assert.That(mode.ContactCount, Is.EqualTo(contacts), "Input begins anticipation, not contact.");
            Assert.That(mode.props[1].localRotation, Is.EqualTo(Quaternion.identity));
            yield return Click(mode.practiceTarget);
            double deadline = Time.realtimeSinceStartupAsDouble + 2;
            while (mode.ContactCount == contacts)
            {
                Assert.That(mode.props[1].localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            Assert.That(mode.character.sprite, Is.SameAs(mode.poses[3]));
            Assert.That(mode.props[1].localRotation, Is.EqualTo(Quaternion.identity));
            yield return new WaitForSecondsRealtime(.065f);
            Assert.That(mode.props[1].localRotation, Is.Not.EqualTo(Quaternion.identity));
            yield return new WaitForSecondsRealtime(.9f);
            Assert.That(mode.ContactCount, Is.EqualTo(contacts + 1), "Repeated input must not restart a pending blow.");
            Assert.That(mode.props[1].localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(mode.traineeAnimation.Current, Is.EqualTo(DojoIdleVariation.Motion.None));
            yield return Click(mode.practiceTarget);
            mode.CancelMotion();
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(mode.ContactCount, Is.EqualTo(contacts + 1), "Cancelled contact must never replay later.");
            Assert.That(mode.props[1].localRotation, Is.EqualTo(Quaternion.identity));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator IdleVariationsTakeTurnsRespondAndYieldToInputAndTravel()
        {
            yield return OpenMenu();
            var mode = view.modeSelection;
            yield return WaitForModeMotion(mode);
            yield return Click(mode.previous);
            yield return WaitForModeMotion(mode);
            var teacher = mode.teacherMotion;
            var teacherSprite = mode.teacherPoses[0];
            var originalCooldown = mode.teacherIdle.cooldown;
            var originalPlayerCooldown = mode.traineeIdle.cooldown;
            var originalWeights = Array.ConvertAll(mode.teacherIdle.variations, item => item.weight);
            try
            {
                // Shorten only waits; keep authored gesture durations and actual frame updates.
                mode.teacherIdle.cooldown = new Vector2(.2f, .3f);
                mode.traineeIdle.cooldown = new Vector2(.3f, .4f);
                mode.teacherIdle.Cancel();
                mode.traineeIdle.Cancel();
                var seen = new System.Collections.Generic.HashSet<DojoIdleVariation.Motion>();
                bool responding = false;
                bool sawSprite = false;
                int contacts = mode.ContactCount;
                double deadline = Time.realtimeSinceStartupAsDouble + 22;
                foreach (var motion in new[] { DojoIdleVariation.Motion.Ready,
                    DojoIdleVariation.Motion.Kick, DojoIdleVariation.Motion.Punch })
                {
                    foreach (var variation in mode.teacherIdle.variations)
                    {
                        variation.weight = 0;
                        if (variation.motion == motion)
                        {
                            variation.weight = 1;
                        }
                    }
                    int completed = mode.lessonSequence.CompletedSets;
                    var previousPhase = DojoLessonSequence.Step.Waiting;
                    double delayStarted = 0;
                    while (mode.lessonSequence.CompletedSets == completed)
                    {
                        Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                        Assert.That(mode.traineeIdle.IsPlaying, Is.False,
                            "A shared lesson owns both actors; no independent player lottery runs.");
                        var phase = mode.lessonSequence.Phase;
                        Assert.That(phase, Is.Not.EqualTo(DojoLessonSequence.Step.Signal),
                            "Each demonstration starts directly from idle, without a shared opening gesture.");
                        Assert.That(mode.teacherAnimation.Current,
                            Is.EqualTo(DojoIdleVariation.Motion.None).Or.EqualTo(motion).Or.EqualTo(DojoIdleVariation.Motion.Approve),
                            "The senior must only demonstrate the selected motion.");
                        Assert.That(mode.traineeAnimation.Current, Is.EqualTo(DojoIdleVariation.Motion.None).Or.EqualTo(motion),
                            "The trainee must imitate only the selected motion, including its first frame.");
                        if (phase != previousPhase)
                        {
                            if (previousPhase == DojoLessonSequence.Step.Waiting)
                            {
                                Assert.That(phase, Is.EqualTo(DojoLessonSequence.Step.Demonstration));
                            }
                            else
                            {
                                Assert.That((int)phase, Is.EqualTo(((int)previousPhase + 1) % 6));
                            }
                            if (phase == DojoLessonSequence.Step.Delay)
                            {
                                delayStarted = Time.realtimeSinceStartupAsDouble;
                            }
                            if (phase == DojoLessonSequence.Step.Imitation)
                            {
                                Assert.That(Time.realtimeSinceStartupAsDouble - delayStarted,
                                    Is.InRange(.32, .55));
                            }
                            previousPhase = phase;
                        }
                        if (phase == DojoLessonSequence.Step.Demonstration)
                        {
                            Assert.That(mode.teacherAnimation.Current, Is.EqualTo(motion));
                            Assert.That(mode.traineeAnimation.Current, Is.EqualTo(DojoIdleVariation.Motion.None));
                            seen.Add(motion);
                            sawSprite |= teacher.sprite != teacherSprite;
                        }
                        if (phase == DojoLessonSequence.Step.Imitation)
                        {
                            Assert.That(mode.traineeAnimation.Current, Is.EqualTo(motion));
                            Assert.That(mode.teacherAnimation.Current, Is.EqualTo(DojoIdleVariation.Motion.None));
                            responding = true;
                        }
                        yield return null;
                    }
                }
                Assert.That(seen.Count, Is.EqualTo(3));
                Assert.That(responding && sawSprite, Is.True);
                Assert.That(mode.ContactCount, Is.EqualTo(contacts), "Idle gestures never hit the target.");
                for (int i = 0; i < originalWeights.Length; i++)
                {
                    mode.teacherIdle.variations[i].weight = originalWeights[i];
                }
                deadline = Time.realtimeSinceStartupAsDouble + 6;
                while (mode.lessonSequence.Phase != DojoLessonSequence.Step.Imitation)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                mode.Practice();
                Assert.That(mode.lessonSequence.Phase, Is.EqualTo(DojoLessonSequence.Step.Waiting));
                Assert.That(mode.teacherIdle.IsPlaying, Is.False);
                Assert.That(mode.traineeIdle.IsPlaying, Is.False);
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.That(mode.teacherIdle.IsPlaying, Is.False, "The entire guided lesson owns the actors.");
                Assert.That(mode.traineeIdle.IsPlaying, Is.False);
                mode.Move(1);
                Assert.That(teacher.sprite, Is.SameAs(teacherSprite));
                Assert.That(mode.teacherIdle.IsPlaying, Is.False);
                yield return WaitForModeMotion(mode);
                deadline = Time.realtimeSinceStartupAsDouble + 8;
                while (!mode.traineeIdle.IsPlaying)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                mode.PracticeTarget();
                Assert.That(mode.traineeIdle.IsPlaying, Is.False, "Input cancels the random pose immediately.");
                mode.InputEnabled = false;
                Assert.That(mode.teacherIdle.IsPlaying, Is.False);
                mode.CancelMotion();
                Assert.That(mode.character.sprite, Is.SameAs(mode.poses[0]));
                Assert.That(mode.teacherMotion, Is.SameAs(teacher));
            }
            finally
            {
                mode.teacherIdle.cooldown = originalCooldown;
                mode.traineeIdle.cooldown = originalPlayerCooldown;
                for (int i = 0; i < originalWeights.Length; i++)
                {
                    mode.teacherIdle.variations[i].weight = originalWeights[i];
                }
                mode.CancelMotion();
            }
        }

        private static IEnumerator WaitForStart(Actions actions)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (actions.Starts == 0)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline),
                    "The authored approach must finish and start selection.");
                yield return null;
            }
        }

        private static IEnumerator WaitForModeMotion(DojoModeView mode)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 4;
            while (mode.IsMoving || mode.IsArriving)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
        }

        private static Vector3[] ReadVertices(Graphic graphic)
        {
            Canvas.ForceUpdateCanvases();
            var mesh = graphic.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null);
            return mesh.vertices;
        }

        private static void AssertGroundedMotion(Graphic graphic, Vector3[] vertices)
        {
            var current = ReadVertices(graphic);
            Assert.That(current.Length, Is.EqualTo(vertices.Length));
            Assert.That(current.Length, Is.GreaterThan(4));
            var rect = graphic.rectTransform.rect;
            float movement = 0f;
            int planted = 0;
            for (int i = 0; i < current.Length; i++)
            {
                if (vertices[i].y <= rect.yMin + rect.height * .22f)
                {
                    Assert.That(Vector3.Distance(vertices[i], current[i]), Is.LessThan(.0001f),
                        "Standing motion must preserve the rendered soles, not just the root transform.");
                    planted++;
                }
                movement = Mathf.Max(movement, Vector3.Distance(vertices[i], current[i]));
            }
            Assert.That(planted, Is.GreaterThan(0));
            var figure = graphic as DojoFigureGraphic;
            if (figure && figure.spriteAnimation)
            {
                Assert.That(movement, Is.LessThan(.0001f), "Breathing is drawn in sprite frames, not mesh distortion.");
            }
            else
            {
                Assert.That(movement, Is.GreaterThan(.03f), "Preserve the sage and cloth's existing motion.");
            }
        }

        private void Capture(string path, int width = 1280, int height = 720)
        {
            if (System.Environment.GetEnvironmentVariable("RHYTHM_DOJO_SKIP_CAPTURE") == "1")
            {
                return;
            }

            var camera = Camera.main;
            var canvas = view.GetComponent<Canvas>();
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            var scaler = canvas.GetComponent<CanvasScaler>();
            var priorScale = canvas.scaleFactor;
            var priorScaler = scaler.enabled;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            try
            {
                scaler.enabled = false;
                canvas.scaleFactor = Mathf.Sqrt(width / 1280f * height / 720f);
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                if (view.startPrompt && view.startPrompt.gameObject.activeInHierarchy)
                {
                    AssertVisibleInCamera(view.logoArtwork, camera);
                    AssertVisibleInCamera(view.startPrompt.rectTransform, camera);
                }
                if (view.modeSelection && !view.modeSelection.IsMoving)
                {
                    AssertVisibleInCamera(view.modeSelection.heading.rectTransform, camera);
                    foreach (var text in new[] { view.modeSelection.description, view.modeSelection.detail })
                    {
                        AssertVisibleInCamera(text.rectTransform, camera);
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f),
                            text.name + " must remain readable without truncation.");
                    }
                }
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                camera.targetTexture = null;
                RenderTexture.active = previous;
                canvas.scaleFactor = priorScale;
                scaler.enabled = priorScaler;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(texture);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void AssertVisibleInCamera(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = camera.WorldToViewportPoint(corner);
                Assert.That(point.x, Is.InRange(-.001f, 1.001f), rect.name + " clipped horizontally");
                Assert.That(point.y, Is.InRange(-.001f, 1.001f), rect.name + " clipped vertically");
            }
        }
    }
}
