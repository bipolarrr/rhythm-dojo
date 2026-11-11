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
            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity");
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
            yield return null;
            Assert.That(view.alwaysVisibleMenu, Is.False);
            yield return Click(view.logo);
            yield return new WaitForSecondsRealtime(MainMenuScreen.MenuExpansionSeconds + .1f);
            Assert.That(view.menuPanel.alpha, Is.EqualTo(1f));
            Assert.That(view.menuPanel.blocksRaycasts, Is.True);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator SlidingDoorsRevealDummiesAndReverseWithoutStartingEarly()
        {
            var actions = new Actions();
            screen.Initialize(actions);
            var leftDoor = view.entrance.leftDoor;
            var rightDoor = view.entrance.rightDoor;
            var doorwayApproach = view.entrance.doorwayApproach;
            yield return Click(view.entranceTrigger);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(view.entrance.Opening, Is.InRange(.01f, .99f));
            Assert.That(view.entrance.leftDoor.anchoredPosition.x, Is.LessThan(0));
            Assert.That(view.entrance.rightDoor.anchoredPosition.x, Is.GreaterThan(0));
            Assert.That(view.entrance.interiorBackdrop.alpha, Is.Zero);
            Assert.That(view.menuPanel.alpha, Is.EqualTo(1), "The actual room remains opaque behind the doors.");
            Assert.That(view.entrance.frame.alpha, Is.EqualTo(1), "The doorway must not dissolve.");
            Capture("Logs/renewal-door-opening.png");
            view.start.onClick.Invoke();
            view.logo.onClick.Invoke();
            Assert.That(actions.Starts, Is.Zero);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(doorwayApproach.localScale.x, Is.GreaterThan(1.2f));
            Assert.That(view.entrance.frame.alpha, Is.EqualTo(1));
            Capture("Logs/hanok-door-approach-test.png");
            yield return new WaitForSecondsRealtime(MainMenuScreen.MenuExpansionSeconds);
            Assert.That(view.entrance.Opening, Is.EqualTo(1));
            Assert.That(view.menuPanel.blocksRaycasts, Is.True);
            view.logo.onClick.Invoke();
            yield return new WaitForSecondsRealtime(MainMenuScreen.MenuExpansionSeconds + .1f);
            Assert.That(view.entrance.Opening, Is.Zero);
            Assert.That(view.menuPanel.gameObject.activeSelf, Is.False);
            Assert.That(view.entrance.leftDoor, Is.SameAs(leftDoor));
            Assert.That(view.entrance.rightDoor, Is.SameAs(rightDoor));
            Assert.That(view.entrance.doorwayApproach, Is.SameAs(doorwayApproach));
            Assert.That(doorwayApproach.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator EscapeCancelsEntryAndTravelWithoutReplacingTheStage()
        {
            var actions = new Actions();
            screen.Initialize(actions);
            var character = view.modeSelection.character;
            var doorwayApproach = view.entrance.doorwayApproach;
            var typography = view.modeSelection.typography[1];
            var titlePosition = ((RectTransform)typography.titleGroup.transform).anchoredPosition;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return Click(view.entranceTrigger);
                yield return new WaitForSecondsRealtime(.35f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                double deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (view.menuPanel.gameObject.activeSelf)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                Assert.That(view.entrance.Opening, Is.Zero);
                yield return OpenMenu();
                yield return Click(view.modeSelection.next);
                yield return new WaitForSecondsRealtime(.20f);
                Assert.That(view.modeSelection.IsMoving, Is.True);
                view.modeSelection.next.onClick.Invoke();
                view.start.onClick.Invoke();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(view.modeSelection.IsMoving, Is.False);
                Assert.That(view.modeSelection.SelectedIndex, Is.EqualTo(1));
                deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (view.menuPanel.gameObject.activeSelf)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                yield return OpenMenu();
                Assert.That(view.modeSelection.character, Is.SameAs(character));
                Assert.That(view.entrance.doorwayApproach, Is.SameAs(doorwayApproach));
                Assert.That(view.modeSelection.travelCover.gameObject.activeSelf, Is.False);
                Assert.That(view.modeSelection.typography[1], Is.SameAs(typography));
                yield return new WaitForSecondsRealtime(typography.enterSeconds + typography.copyDelay);
                Assert.That(typography.titleGroup.alpha, Is.EqualTo(1f));
                Assert.That(typography.copyGroup.alpha, Is.EqualTo(1f));
                Assert.That(((RectTransform)typography.titleGroup.transform).anchoredPosition,
                    Is.EqualTo(titlePosition), "Reopening must restore the authored typography pose.");
                Assert.That(actions.Starts, Is.Zero);
                yield return Click(view.modeSelection.previous);
                deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (view.modeSelection.IsMoving)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                Assert.That(view.modeSelection.SelectedIndex, Is.Zero);
                view.logo.onClick.Invoke();
                deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (view.menuPanel.gameObject.activeSelf)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                    yield return null;
                }
                yield return OpenMenu();
                yield return Click(view.start);
                Assert.That(view.modeSelection.actionLabel.text, Is.EqualTo("두드리기"));
                Assert.That(view.modeSelection.typography[0].titleGroup.alpha, Is.EqualTo(1f));
                Assert.That(view.modeSelection.typography[0].copyGroup.alpha, Is.EqualTo(1f),
                    "Starting a lesson during its entrance must finish revealing the instructions.");
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
                view.start.onClick.Invoke();
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
            Assert.That(view.editor.gameObject.activeInHierarchy, Is.False);
            view.editor.onClick.Invoke();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo("Assets/Game/Scenes/Title.unity"));
            yield return OpenMenu();
            Assert.That(view.editor.transform.IsChildOf(view.menuPanel.transform), Is.True,
                "The authored composition must stay under the door-controlled menu.");
            Assert.That(view.editor.gameObject.activeInHierarchy, Is.True);
            var editorCorners = new Vector3[4];
            ((RectTransform)view.editor.transform).GetWorldCorners(editorCorners);
            foreach (var corner in editorCorners)
            {
                var local = view.GetComponent<RectTransform>().InverseTransformPoint(corner);
                Assert.That(view.GetComponent<RectTransform>().rect.Contains(local), Is.True,
                    "Editor control must remain inside the screen.");
            }

            view.logo.onClick.Invoke();
            yield return new WaitForSecondsRealtime(MainMenuScreen.MenuExpansionSeconds + .1f);
            Assert.That(view.editor.gameObject.activeInHierarchy, Is.False);
            yield return OpenMenu();
            yield return Click(view.editor);
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
            Assert.That(SceneManager.GetActiveScene().path, Does.EndWith("/Title.unity"));
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
                    view.start.interactable || view.settings.interactable || view.quit.interactable,
                    Is.False
                );
                yield return ClickAt(new Vector2(Screen.width * .2f, Screen.height * .8f));
                Assert.That(actions.Starts, Is.Zero);
                Assert.That(view.quitPopup.activeSelf, Is.True);
                yield return Click(view.cancelQuit);
                Assert.That(view.quitPopup.activeSelf, Is.False);
                Assert.That(
                    view.start.interactable && view.settings.interactable && view.quit.interactable,
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
            yield return Click(view.start);
            yield return new WaitForSecondsRealtime(.45f);
            view.start.onClick.Invoke();
            Assert.That(actions.Starts, Is.EqualTo(1));
            Assert.That(oldActions.Starts, Is.Zero);
            Assert.That(view.start.interactable, Is.False);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator PlayDismissesMenuImmediatelyWithoutWaitingForBoardDelay()
        {
            var actions = new Actions();
            screen.Initialize(actions);
            yield return OpenMenu();
            Assert.That(view.start.GetComponent<DojoBoardBreak>(), Is.Null);
            Assert.That(view.menuPanel.alpha, Is.EqualTo(1f));
            view.start.onClick.Invoke();
            Assert.That(view.menuPanel.alpha, Is.LessThan(1f),
                "Play must respond immediately instead of holding a static menu for a quarter second.");
            Assert.That(actions.Starts, Is.Zero);
            yield return new WaitForSecondsRealtime(.25f);
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
                Assert.That(SceneManager.GetActiveScene().path, Does.EndWith("/Title.unity"));
                yield return null;
            }

            var selection = UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>();
            Assert.That(selection.enabled, Is.False);
            Assert.That(selection.GetComponent<Canvas>().sortingOrder, Is.LessThan(view.GetComponent<Canvas>().sortingOrder));
            Assert.That(selection.GetComponent<CanvasGroup>().interactable, Is.False);
            Assert.That(selection.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.gameObject.scene.path, Does.EndWith("/Title.unity"));
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.SameAs(view.logo.gameObject));
            Assert.That(Array.FindAll(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None),
                item => item.isActiveAndEnabled), Has.Length.EqualTo(1));
            Assert.That(DojoEntranceTransition.Active, Is.Null);
            yield return OpenMenu();
            Assert.That(view.start.interactable, Is.True);
        }

        [UnityTest]
        [Category("Smoke")]
        [Category("SceneIntegration")]
        public IEnumerator StartLoadsBootstrapThenExistingSongSelection()
        {
            view.entrance.SetApproach(1f);
            Canvas.ForceUpdateCanvases();
            var entranceCorners = new Vector3[4];
            view.entrance.GetComponentInChildren<Image>().rectTransform.GetWorldCorners(entranceCorners);
            view.entrance.SetApproach(0f);
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
                Assert.That(UnityEngine.Object.FindFirstObjectByType<AppFlowController>().IsSelectionPrepared, Is.True);
                int preparedHandle = SceneManager.GetSceneByPath(AppFlowController.SelectionPath).handle;
                selectionLoads = 0;
                yield return Click(view.start);
            yield return new WaitForSecondsRealtime(.45f);

                double deadline = Time.realtimeSinceStartupAsDouble + 20;

                while (!UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>())
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
                var backdrop = Array.Find(selectionView.GetComponentsInChildren<Image>(),
                    image => image.sprite == selectionView.fallbackCover && image.GetComponent<AspectRatioFitter>());
                Assert.That(backdrop, Is.Not.Null);
                Assert.That(selectionView.entrancePanels, Has.Length.EqualTo(6));
                Canvas.ForceUpdateCanvases();
                var selectionCorners = new Vector3[4];
                backdrop.rectTransform.GetWorldCorners(selectionCorners);


                AssertHasDisplayCamera();
                Assert.That(bootstrapLoaded, Is.False,
                    "The title must enter selection directly, avoiding an extra scene activation stall.");
                Assert.That(selectionLoads, Is.Zero, "Play must reuse the warmed scene without loading it again.");
                Assert.That((int)SceneManager.GetActiveScene().handle, Is.EqualTo(preparedHandle));
                Assert.That(SceneManager.GetSceneByPath("Assets/Game/Scenes/Title.unity").isLoaded, Is.False,
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
            screen.enabled = false;
            try
            {
                view.SetExpansion(.36f);
                Capture("Logs/main-menu-door-opening.png");
                view.SetExpansion(.62f);
                Capture("Logs/main-menu-door-approach.png");
                Assert.That(view.entrance.frame.alpha, Is.EqualTo(1f));
                Assert.That(view.menuPanel.alpha, Is.EqualTo(1f));
                Assert.That(view.entrance.doorwayApproach.localScale.x, Is.GreaterThan(1.2f));
                view.SetExpansion(1f);
                Capture("Logs/main-menu-door-entered.png");
            }
            finally
            {
                view.SetExpansion(1f);
                screen.enabled = true;
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
            var actions = new Actions();
            screen.Initialize(actions);
            yield return OpenMenu();
            view.logo.onClick.Invoke();
            yield return new WaitForSecondsRealtime(MainMenuScreen.MenuExpansionSeconds + .1f);
            Assert.That(view.menuPanel.gameObject.activeSelf, Is.False);
            Assert.That(view.logo.targetGraphic.raycastTarget, Is.True);
            view.start.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(actions.Starts, Is.Zero);
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
                Is.EqualTo("Assets/Game/Scenes/Title.unity"));
            Assert.That(SceneManager.GetSceneByPath(AppFlowController.SettingsPath).isLoaded,
                Is.False);
            Assert.That(settingsView.backdrop, Is.Not.Null);
            Assert.That(settingsView.backdrop.activeSelf, Is.False);
            Assert.That(view.modeSelection.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.entrance.frame.alpha, Is.Zero);
            Assert.That(view.entrance.Opening, Is.EqualTo(1));
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

            while (SceneManager.GetActiveScene().path != "Assets/Game/Scenes/Title.unity")
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
            Assert.That(view.start.interactable, Is.True);
            view.logo.onClick.Invoke();
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
            view.start.onClick.Invoke();
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
            Assert.That(view.start.interactable, Is.True);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator EntranceWaitsForReadyAndRestoresSelectionInput()
        {
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
            var actions = new Actions();
            screen.Initialize(actions);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)view.logo.transform;
            var corner = rect.TransformPoint(new Vector3(rect.rect.xMax - 5f, rect.rect.yMax - 5f));
            yield return ClickAt(RectTransformUtility.WorldToScreenPoint(null, corner));
            yield return new WaitForSecondsRealtime(MainMenuScreen.MenuExpansionSeconds + .1f);
            Assert.That(view.menuPanel.gameObject.activeSelf, Is.True);
            view.logo.onClick.Invoke();
            yield return new WaitForSecondsRealtime(MainMenuScreen.MenuExpansionSeconds + .1f);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(view.logo.gameObject);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(MainMenuScreen.MenuExpansionSeconds + .1f);
                Assert.That(view.menuPanel.alpha, Is.EqualTo(1f));
                Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,
                    Is.EqualTo(view.start.gameObject));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(actions.Starts, Is.EqualTo(1));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator EntranceTimeoutOffersRecoveryAndCleansUp()
        {
            bool recovered = false;
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
            yield return Click(view.modeSelection.next);
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (view.modeSelection.IsMoving)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            yield return Click(view.start);
            deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (SceneManager.GetActiveScene().path != AppFlowController.SelectionPath)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            yield return null;
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(flow.Rules.Snapshot.Mode, Is.EqualTo(RhythmDojo.Gameplay.PlayMode.Challenge));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongSelectionView>().modeLabel.text,
                Does.StartWith("Challenge"));
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
                "The target must wait for the player's answer to the master.");
            while (view.modeSelection.IsArriving)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            Assert.That(view.modeSelection.ContactCount, Is.EqualTo(contacts + 1));
            Capture("Logs/renewal-lesson.png");
            yield return Click(view.start);
            double startTime = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < 4; i++)
            {
                while (Time.realtimeSinceStartupAsDouble < startTime + i * .75)
                {
                    yield return null;
                }
                yield return Click(view.start);
            }
            Assert.That(view.modeSelection.LessonHits, Is.EqualTo(4));
            Assert.That(view.modeSelection.detail.text, Does.Contain("기초 수련 완료"));
            Assert.That(view.modeSelection.lessonCue.text, Does.Contain("잘했다"));
            foreach (var mark in view.modeSelection.lessonMarks)
            {
                Assert.That(mark.color.r, Is.EqualTo(1));
            }
            Assert.That(SceneManager.GetActiveScene().path, Does.EndWith("/Title.unity"));
            Capture("Logs/emergency-lesson.png");
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator UndefinedFourthModeCannotStartButNavigationRemainsAvailable()
        {
            var actions = new Actions();
            screen.Initialize(actions);
            yield return OpenMenu();
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
            Assert.That(view.start.interactable, Is.False);
            view.start.onClick.Invoke();
            Assert.That(actions.Starts, Is.Zero);
            yield return Click(view.modeSelection.next);
            double navigationDeadline = Time.realtimeSinceStartupAsDouble + 3;
            while (view.modeSelection.IsMoving)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(navigationDeadline));
                yield return null;
            }
            yield return null;
            Assert.That(view.modeSelection.SelectedIndex, Is.Zero);
            Assert.That(view.start.interactable, Is.True);
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
            yield return Click(view.start);
            deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!view.modeSelection.detail.text.Contains("다시 수련 시작"))
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            Assert.That(view.start.interactable, Is.True);
            ready = true;
            yield return Click(view.start);
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
                    Assert.That(mode.banners[mode.SelectedIndex].localRotation, Is.EqualTo(Quaternion.identity));
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
            Assert.That(mode.character.sprite, Is.SameAs(mode.poses[0]));
            yield return Click(mode.practiceTarget);
            mode.CancelMotion();
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(mode.ContactCount, Is.EqualTo(contacts + 1), "Cancelled contact must never replay later.");
            Assert.That(mode.props[1].localRotation, Is.EqualTo(Quaternion.identity));
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
            Assert.That(movement, Is.GreaterThan(.03f), "Grounding must retain visible upper-body or cloth motion.");
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
                if (view.startPrompt.gameObject.activeInHierarchy && view.entrance.Opening < .01f)
                {
                    AssertVisibleInCamera(view.logoArtwork, camera);
                    AssertVisibleInCamera(view.startPrompt.rectTransform, camera);
                }
                if (!view.entrance.gameObject.activeSelf && !view.modeSelection.IsMoving)
                {
                    AssertVisibleInCamera(view.modeSelection.heading.rectTransform, camera);
                    foreach (var text in new[] { view.modeSelection.description, view.modeSelection.detail })
                    {
                        AssertVisibleInCamera(text.rectTransform, camera);
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f),
                            text.name + " must remain readable without truncation.");
                    }
                    AssertVisibleInCamera((RectTransform)view.start.transform, camera);
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
