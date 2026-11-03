using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RhythmDojo.Core;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RhythmDojo.Tests
{
    public sealed class InputStateTestParticipant : MonoBehaviour, IModalInputParticipant
    {
        public int resumed;
        public bool throws;

        public void ResumeAfterModal()
        {
            resumed++;
            if (throws)
            {
                throw new InvalidOperationException("Injected resume failure.");
            }
        }
    }

    public sealed class ScenePreparationFlowTests : GameplayTestContext
    {
        private static readonly FieldInfo preparationField = typeof(AppFlowController).GetField(
            "selectionPreparation", BindingFlags.Instance | BindingFlags.NonPublic);
        private AppFlowController flow;
        private PreparedSelectionScene preparation;
        private bool ready;

        private IEnumerator PrepareTitle(double timeout = 20)
        {
            flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.ShowTitle();
            yield return Await(() => SceneManager.GetActiveScene().path == "Assets/Game/Scenes/Title.unity");
            yield return null;
            var previous = (PreparedSelectionScene)preparationField.GetValue(flow);
            flow.CancelSelectionPreparation();
            yield return Wait(() => !previous.IsBusy);
            previous.Dispose();
            ready = false;
            preparation = new PreparedSelectionScene(flow, () => ready, readyTimeout: timeout);
            preparationField.SetValue(flow, preparation);
            flow.PrepareSelection();
        }

        private static IEnumerator Wait(Func<bool> predicate, double seconds = 15)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!predicate())
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator InvalidatedLoadNeverInitializesAfterEditorExit()
        {
            yield return PrepareTitle();
            var title = SceneManager.GetActiveScene();
            new MainMenuActions(flow.Settings).ShowEditor();
            yield return Wait(() => SceneManager.GetActiveScene().path == ChartEditorMenu.ScenePath);
            yield return Wait(() => !preparation.IsBusy);
            Assert.That(preparation.IsSelectionPrepared, Is.False);
            Assert.That(SceneManager.GetSceneByPath(AppFlowController.SelectionPath).isLoaded, Is.False);
            Assert.That(flow.CurrentRequest, Is.Null);
            Assert.That(title.isLoaded, Is.False);
            Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)
                .Count(value => value.isActiveAndEnabled), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PopupKeepsFocusWhilePreparationFinishesThenEntranceRestoresSelectionFocus()
        {
            yield return PrepareTitle();
            var view = UnityEngine.Object.FindFirstObjectByType<MainMenuView>();
            var titleFocus = view.logo.gameObject;
            EventSystem.current.SetSelectedGameObject(titleFocus);
            flow.ShowTitleSettings();
            yield return null;
            var popup = UnityEngine.Object.FindFirstObjectByType<SettingsScreen>();
            Assert.That(popup, Is.Not.Null);
            var popupFocus = EventSystem.current.currentSelectedGameObject;
            Assert.That(popupFocus, Is.Not.Null);
            ready = true;
            yield return Wait(() => preparation.IsSelectionPrepared);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(popupFocus));
            flow.ReturnFromSettings();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(titleFocus));
            new MainMenuActions(flow.Settings, view).StartGame();
            flow.ShowSelection();
            yield return Wait(() => !DojoEntranceTransition.Active);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppFlowController.SelectionPath));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>().enabled, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.scene.path,
                Is.EqualTo(AppFlowController.SelectionPath));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)
                .Count(value => value.isActiveAndEnabled), Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                .Count(value => value.isActiveAndEnabled), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DelayedPreparationPreservesAnEmptyTitleFocus()
        {
            yield return PrepareTitle();
            EventSystem.current.SetSelectedGameObject(null);
            ready = true;
            yield return Wait(() => preparation.IsSelectionPrepared);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
            flow.CancelSelectionPreparation();
            yield return Wait(() => !preparation.IsBusy);
        }

        [Test]
        public void ClosingModalRestoresAnEmptyFocusAfterSelectionResume()
        {
            var screen = UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>();
            EventSystem.current.SetSelectedGameObject(null);
            using (var scope = new ModalInputScope(SceneManager.GetActiveScene()))
            {
                Assert.That(screen.enabled, Is.False);
            }
            Assert.That(screen.enabled, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
        }

        [UnityTest]
        public IEnumerator DisposingOwnerDrainsOutstandingLoadWithoutInitializingIt()
        {
            yield return PrepareTitle();
            Assert.That(preparation.IsBusy, Is.True);
            preparation.Dispose();
            ready = true;
            yield return Wait(() => !preparation.IsBusy);
            Assert.That(preparation.IsSelectionPrepared, Is.False);
            Assert.That(SceneManager.GetSceneByPath(AppFlowController.SelectionPath).isLoaded, Is.False);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo("Assets/Game/Scenes/Title.unity"));
            Assert.That(flow.CurrentRequest, Is.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)
                .Count(value => value.isActiveAndEnabled), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReentryQueuesNewGenerationUntilInvalidatedLoadIsDrained()
        {
            yield return PrepareTitle();
            flow.CancelSelectionPreparation();
            flow.PrepareSelection();
            ready = true;
            yield return Wait(() => preparation.IsSelectionPrepared);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(2));
            Assert.That(UnityEngine.Object.FindObjectsByType<SongSelectionScreen>(FindObjectsSortMode.None)
                .Length, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>().enabled, Is.False);
            flow.CancelSelectionPreparation();
            yield return Wait(() => !preparation.IsBusy);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TimeoutDiscardsUninitializedSceneAndAllowsRetry()
        {
            yield return PrepareTitle(.25);
            yield return Wait(() => preparation.LastError != null);
            Assert.That(preparation.LastError, Is.TypeOf<TimeoutException>());
            yield return Wait(() => !preparation.IsBusy);
            Assert.That(SceneManager.GetSceneByPath(AppFlowController.SelectionPath).isLoaded, Is.False);
            preparation.Dispose();
            preparation = new PreparedSelectionScene(flow);
            preparationField.SetValue(flow, preparation);
            flow.PrepareSelection();
            yield return Wait(() => preparation.IsSelectionPrepared);
            Assert.That(preparation.LastError, Is.Null);
        }

        [UnityTest]
        public IEnumerator LoadFailureAllowsColdEntranceRetry()
        {
            yield return PrepareTitle();
            flow.CancelSelectionPreparation();
            yield return Wait(() => !preparation.IsBusy);
            preparation.Dispose();
            preparation = new PreparedSelectionScene(flow, load: () => null);
            preparationField.SetValue(flow, preparation);
            flow.PrepareSelection();
            Assert.That(preparation.LastError, Is.TypeOf<InvalidOperationException>());
            preparation.Dispose();
            preparation = new PreparedSelectionScene(flow);
            preparationField.SetValue(flow, preparation);
            var view = UnityEngine.Object.FindFirstObjectByType<MainMenuView>();
            new MainMenuActions(flow.Settings, view).StartGame();
            yield return Wait(() => !DojoEntranceTransition.Active);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppFlowController.SelectionPath));
            Assert.That(flow.Transitioning, Is.False);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
        }

        [Test]
        public void FinalOwnerRestoresOriginalStatesAndNotifiesOnceEvenAfterAnotherParticipantThrows()
        {
            var go = new GameObject("Scope test");
            var first = go.AddComponent<InputStateTestParticipant>();
            var second = go.AddComponent<InputStateTestParticipant>();
            var disabled = go.AddComponent<InputStateTestParticipant>();
            disabled.enabled = false;
            first.throws = true;
            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = true;
            var focus = EventSystem.current.currentSelectedGameObject;
            var outer = new InputStateScope(SceneManager.GetActiveScene());
            var inner = new InputStateScope(SceneManager.GetActiveScene());
            try
            {
                foreach (var participant in new[] { first, second, disabled })
                {
                    outer.Suspend(participant);
                    inner.Suspend(participant);
                }
                outer.Block(group);
                inner.Block(group);
                outer.Dispose();
                Assert.That(first.enabled, Is.False);
                Assert.That(first.resumed, Is.Zero);
                Assert.That(group.blocksRaycasts, Is.False);
                EventSystem.current.SetSelectedGameObject(null);
                Assert.Throws<AggregateException>(() => inner.Dispose());
                Assert.That(first.enabled, Is.True);
                Assert.That(second.enabled, Is.True);
                Assert.That(disabled.enabled, Is.False);
                Assert.That(first.resumed, Is.EqualTo(1));
                Assert.That(second.resumed, Is.EqualTo(1));
                Assert.That(disabled.resumed, Is.Zero);
                Assert.That(group.interactable, Is.False);
                Assert.That(group.blocksRaycasts, Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(focus));
                Assert.DoesNotThrow(() => inner.Dispose());
            }
            finally
            {
                outer.Dispose();
                inner.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
