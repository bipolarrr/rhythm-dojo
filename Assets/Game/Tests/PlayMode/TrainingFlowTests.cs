using RhythmDojo.Presentation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RhythmDojo.Tests
{
    [Category("Training")]
    [Category("Gameplay")]
    public sealed class TrainingFlowTests : GameplayTestContext
    {
        private string previousTraining;
        private bool hadTraining;
        private string previousDialogue;
        private bool hadDialogue;
        private RhythmGameController game;
        private readonly List<(double time, Key key, bool press)> events = new List<(double, Key, bool)>();
        private readonly HashSet<Key> held = new HashSet<Key>();
        private int nextEvent;

        [SetUp]
        public void PreserveTraining()
        {
            hadTraining = PlayerPrefs.HasKey(TrainingProgressStore.StorageKey);
            previousTraining = PlayerPrefs.GetString(TrainingProgressStore.StorageKey, "");
            PlayerPrefs.DeleteKey(TrainingProgressStore.StorageKey);
            hadDialogue = PlayerPrefs.HasKey(DialogueReadStore.StorageKey);
            previousDialogue = PlayerPrefs.GetString(DialogueReadStore.StorageKey, "");
            PlayerPrefs.DeleteKey(DialogueReadStore.StorageKey);
        }

        [UnityTearDown]
        public IEnumerator RestoreTraining()
        {
            InputSystem.onBeforeUpdate -= FeedInput;
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            if (flow)
            {
                flow.Training?.Abort();
            }
            if (hadTraining)
            {
                PlayerPrefs.SetString(TrainingProgressStore.StorageKey, previousTraining);
            }
            else
            {
                PlayerPrefs.DeleteKey(TrainingProgressStore.StorageKey);
            }
            PlayerPrefs.Save();
            if (hadDialogue)
            {
                PlayerPrefs.SetString(DialogueReadStore.StorageKey, previousDialogue);
            }
            else
            {
                PlayerPrefs.DeleteKey(DialogueReadStore.StorageKey);
            }
            PlayerPrefs.Save();
            events.Clear();
            held.Clear();
            yield return null;
        }

        private IEnumerator OpenTraining()
        {
            yield return SceneManager.LoadSceneAsync(MainMenuActions.TutorialScenePath);
            yield return Await(() => Object.FindFirstObjectByType<TutorialScreen>());
            yield return null;
        }

        private IEnumerator StartTraining(bool perfect, double error = 0, bool extra = false, string fault = null)
        {
            yield return Click(Object.FindFirstObjectByType<TutorialView>().startButton);
            yield return FinishDialogue();
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            game = Object.FindFirstObjectByType<RhythmGameController>();
            events.Clear();
            held.Clear();
            nextEvent = 0;
            if (perfect)
            {
                var song = Object.FindFirstObjectByType<AppFlowController>().CurrentRequest.Song;
                for (int i = 0; i < game.Chart.Count; i++)
                {
                    var note = game.Chart[i];
                    var key = PlayKeyBindings.GetKey(song.Mode, song.ModeId, note.Lane);
                    double press = note.StartTime + error;
                    double release = note.EndTime + error + .025;
                    if (fault == "chord")
                    {
                        if (note.Lane < 2)
                        {
                            press -= .04;
                        }
                        else
                        {
                            press += .04;
                        }
                        release = press + .02;
                    }
                    if (i == 0)
                    {
                        if (fault == "early")
                        {
                            release = note.EndTime - .6;
                        }
                        else if (fault == "jack")
                        {
                            release = game.Chart[1].StartTime + .2;
                        }
                    }
                    if (fault == "late" && note.Kind == NoteKind.Hold)
                    {
                        release = note.EndTime + .3;
                    }
                    events.Add((press, key, true));
                    events.Add((release, key, false));
                }
                if (extra)
                {
                    var key = PlayKeyBindings.GetKey(song.Mode, song.ModeId, 1);
                    events.Add((5.5, key, true));
                    events.Add((5.55, key, false));
                }
                events.Sort((a, b) => a.time.CompareTo(b.time));
                InputSystem.onBeforeUpdate += FeedInput;
            }
            yield return WaitForGameplayIntroduction();
            yield return null;
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Playing));
            Assert.That(flow.Training.State, Is.EqualTo(TrainingState.Playing));
            Assert.That(flow.IsEditorPlaytest, Is.False);
        }

        private void FeedInput()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || !game)
            {
                return;
            }
            if (game.Session == null || game.Session.State != SessionState.Playing)
            {
                return;
            }
            double time = game.Clock.SongTime;
            double realtime = Time.realtimeSinceStartupAsDouble;
            while (nextEvent < events.Count && time >= events[nextEvent].time)
            {
                var input = events[nextEvent++];
                if (input.press)
                {
                    held.Add(input.key);
                }
                else
                {
                    held.Remove(input.key);
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(held.ToArray()),
                    realtime - (time - input.time));
            }
        }

        private IEnumerator WaitForFeedback()
        {
            yield return Await(() => Object.FindFirstObjectByType<TutorialScreen>(),
                "Training did not return a result after real playback.", 35);
            InputSystem.onBeforeUpdate -= FeedInput;
            yield return null;
            Assert.That(Object.FindFirstObjectByType<AppFlowController>().CurrentRequest, Is.Null);
            var dialogue = Object.FindFirstObjectByType<DialogueScreen>();
            Assert.That(dialogue.IsActive, Is.True, "The mentor must explain the completed/aborted attempt.");
            yield return Await(() => dialogue.Session.IsLineComplete);
            CaptureSelection("dialogue-feedback-" + Object.FindFirstObjectByType<AppFlowController>().Training.Outcome);
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            if (flow.Training.Outcome == TrainingOutcome.Failed && flow.CurrentTrainingLesson.LessonId == "trill")
            {
                var lines = flow.CurrentTrainingLesson.CreateFeedback(flow.Training);
                string feedback = string.Join("\n", lines.Select(line => line.Text));
                Assert.That(feedback.Contains("맞히지 못한 노트"), Is.EqualTo(flow.Training.Result.Value.Miss > 0));
                Assert.That(feedback.Contains("Perfect 범위"), Is.EqualTo(flow.Training.Result.Value.Good > 0));
                Assert.That(feedback.Contains("노트에 맞지 않은 입력"), Is.EqualTo(flow.Training.ExtraInputs > 0));
            }
            yield return FinishDialogue();
        }

        private IEnumerator FinishDialogue()
        {
            var dialogue = Object.FindFirstObjectByType<DialogueScreen>();
            var view = Object.FindFirstObjectByType<DialogueView>();
            Assert.That(dialogue.IsActive, Is.True);
            yield return Await(() => view.advance.interactable);
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (dialogue && dialogue.IsActive)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Dialogue did not finish.");
                if (!dialogue.Session.IsComplete)
                {
                    yield return Press(Key.Enter);
                }
                else
                {
                    yield return null;
                }
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator DialogueKeyboardMouseAndHeldInputGateTheActualTrainingTransition()
        {
            yield return OpenTraining();
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var tutorial = Object.FindFirstObjectByType<TutorialView>();
            var dialogue = Object.FindFirstObjectByType<DialogueScreen>();
            var view = Object.FindFirstObjectByType<DialogueView>();
            var objects = dialogue.GetComponentsInChildren<Transform>(true);
            yield return Click(tutorial.startButton);
            yield return Await(() => view.advance.interactable);
            Assert.That(dialogue.Session.IsLineComplete, Is.False);
            yield return Press(Key.Escape);
            yield return Press(Key.D);
            Assert.That(flow.Training.State, Is.EqualTo(TrainingState.Ready));
            Assert.That(flow.CurrentRequest, Is.Null);
            Assert.That(tutorial.startButton.interactable, Is.False);
            Assert.That(tutorial.backButton.interactable, Is.False);
            Assert.That(view.skip.interactable, Is.False);
            yield return Press(Key.Tab);
            Assert.That(dialogue.Session.Index, Is.Zero, "Unread text cannot be skipped.");
            yield return Press(Key.Enter);
            Assert.That(dialogue.Session.Index, Is.Zero);
            Assert.That(dialogue.Session.IsLineComplete, Is.True);
            CaptureSelection("dialogue-calm");
            yield return Click(view.advance);
            Assert.That(dialogue.Session.Index, Is.EqualTo(1));
            yield return Await(() => dialogue.Session.IsLineComplete);
            Assert.That(view.portrait.sprite, Is.EqualTo(view.expressions[(int)RhythmDojo.Dialogue.DialogueExpression.Teaching]));
            CaptureSelection("dialogue-teaching");
            CaptureSelection("dialogue-teaching-16x10", 1280, 800);
            yield return Press(Key.Space);
            yield return Await(() => dialogue.Session.IsLineComplete);
            Assert.That(dialogue.GetComponentsInChildren<Transform>(true), Is.EqualTo(objects));
            CaptureSelection("dialogue-confident");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter, Key.D));
            yield return null;
            yield return Await(() => view.group.alpha == 0);
            yield return null;
            Assert.That(dialogue.IsActive, Is.True, "Held confirm/lane input must not leak into gameplay.");
            Assert.That(flow.CurrentRequest, Is.Null);
            Assert.That(Object.FindFirstObjectByType<RhythmGameController>(), Is.Null);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return WaitForGameplayIntroduction();
            Assert.That(Object.FindFirstObjectByType<DialogueScreen>(), Is.Null);
            Assert.That(flow.Training.Attempt, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<RhythmGameController>().Session.State, Is.EqualTo(SessionState.Playing));
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>().ReadModel.Snapshot.SongTime >= 3.02);
            CapturePlayback("trill-demonstration");

            yield return Press(Key.Escape);
            yield return Click(Object.FindFirstObjectByType<GameplayPauseMenu>().exitButton);
            yield return WaitForFeedback();
            yield return Click(Object.FindFirstObjectByType<TutorialView>().backButton);
            tutorial = Object.FindFirstObjectByType<TutorialView>();
            yield return Click(tutorial.lessonButtons[5]);
            CaptureSelection("pattern-training-selection-final");
            yield return StartTraining(false);
            yield return Await(() => game.ReadModel.Snapshot.SongTime >= 3.1);
            var heading = Object.FindFirstObjectByType<TrainingPlaybackView>().heading;
            Canvas.ForceUpdateCanvases();
            Assert.That(heading.preferredHeight, Is.LessThanOrEqualTo(heading.rectTransform.rect.height),
                "The full lesson name and stage must fit the authored mentor heading.");
            CapturePlayback("shield-release-demonstration-final");
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator DialogueReadHistoryReopenCancellationAndFailedStartRemainUsable()
        {
            yield return OpenTraining();
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var lesson = flow.CurrentTrainingLesson;
            var screen = Object.FindFirstObjectByType<TutorialScreen>();
            var view = Object.FindFirstObjectByType<DialogueView>();
            var dialogue = Object.FindFirstObjectByType<DialogueScreen>();
            yield return Click(Object.FindFirstObjectByType<TutorialView>().startButton);
            yield return Await(() => view.advance.interactable);
            yield return Press(Key.Enter);
            Assert.That(dialogue.Session.IsLineComplete, Is.True);
            screen.enabled = false;
            screen.enabled = true;
            Assert.That(dialogue.IsActive, Is.False);
            Assert.That(flow.CurrentRequest, Is.Null);

            yield return OpenTraining();
            screen = Object.FindFirstObjectByType<TutorialScreen>();
            view = Object.FindFirstObjectByType<DialogueView>();
            dialogue = Object.FindFirstObjectByType<DialogueScreen>();
            int calls = 0;
            screen.Initialize(flow.ShowPracticeModes, lesson, flow.Training, () =>
            {
                calls++;
                return false;
            });
            var tutorial = Object.FindFirstObjectByType<TutorialView>();
            yield return Click(tutorial.startButton);
            yield return Await(() => view.advance.interactable);
            Assert.That(view.skip.interactable, Is.True);
            yield return Click(view.skip);
            Assert.That(dialogue.Session.Index, Is.EqualTo(1));
            Assert.That(view.skip.interactable, Is.False);
            yield return FinishDialogue();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(tutorial.startButton.interactable, Is.True);
            Assert.That(tutorial.storyRoot.gameObject.activeSelf, Is.True);

            yield return Click(tutorial.startButton);
            yield return Await(() => view.advance.interactable);
            yield return Press(Key.Tab);
            yield return Await(() => !dialogue.IsActive);
            Assert.That(calls, Is.EqualTo(2), "All read lines skip to one completion callback.");
            var changed = Object.Instantiate(lesson.IntroductionDialogue);
            try
            {
                changed.lines[0].text += " 새 가르침이다.";
                dialogue.Play(changed, () => calls++);
                Assert.That(dialogue.Session.CanSkipRead, Is.False, "Changed wording must be read again.");
                dialogue.Cancel();
                Assert.That(calls, Is.EqualTo(2), "Cancellation is not story completion.");
            }
            finally
            {
                Object.Destroy(changed);
            }
        }

        [UnityTest]
        [Category("Endurance")]
        [Timeout(300000)]
        public IEnumerator RealInputFailureRetrySuccessAndNewApplicationSessionRestoreCompletion()
        {
            yield return SceneManager.LoadSceneAsync(MainMenuActions.TitleScenePath);
            yield return null;
            yield return Click(Object.FindFirstObjectByType<TitleSkyView>().start);
            yield return Await(() => SceneManager.GetActiveScene().path == MainMenuActions.ModeSelectionScenePath);
            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            yield return Await(() => menu.CanSubmit);
            yield return Click(menu.GetComponent<MainMenuView>().modeSelection.previous);
            yield return Await(() => menu.CanSubmit);
            yield return Press(Key.Enter);
            yield return Await(() => Object.FindFirstObjectByType<TutorialScreen>());
            yield return null;
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            flow.Preferences.TimingOffsetMs = 30;
            var rules = flow.Rules.Snapshot;
            rules.Mode = RhythmDojo.Gameplay.PlayMode.Challenge;
            flow.Rules.Update(rules);
            string ordinary = PlayerPrefs.GetString(SelectionPreferences.StorageKey, "");
            var lesson = flow.CurrentTrainingLesson;
            string lessonId = lesson.LessonId;
            int version = lesson.DefinitionVersion;
            CaptureSelection("trill-ready");

            yield return StartTraining(false);
            Assert.That(flow.CurrentRequest.TimingOffsetMs, Is.Zero);
            Assert.That(flow.Training.Step, Is.EqualTo(TrainingStep.Demonstration));
            yield return Press(Key.D);
            yield return Press(Key.K);
            yield return Await(() => game.ReadModel.Snapshot.SongTime >= 3.02);
            var mentor = Object.FindFirstObjectByType<TrainingPlaybackView>();
            Assert.That(mentor.actor.Current, Is.EqualTo(DojoIdleVariation.Motion.Punch));
            CapturePlayback("trill-demonstration");
            yield return WaitForFeedback();
            Assert.That(flow.Training.Result.Value.Perfect, Is.EqualTo(8));
            Assert.That(flow.Training.ExtraInputs, Is.Zero, "Physical input during demonstration is ignored.");
            Assert.That(flow.Training.Completed, Is.False);
            Assert.That(PlayerPrefs.HasKey(TrainingProgressStore.StorageKey), Is.False);
            foreach (var note in flow.LastResult.Notes)
            {
                Assert.That(note.Start.Value.ErrorSeconds, Is.Zero.Within(1e-8));
            }

            yield return StartTraining(false);
            Assert.That(flow.CurrentRequest.TimingOffsetMs, Is.EqualTo(30));
            Assert.That(flow.Training.Step, Is.EqualTo(TrainingStep.Imitation));
            yield return WaitForFeedback();
            Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.Failed));
            Assert.That(flow.Training.Result.Value.Miss, Is.EqualTo(8));
            Assert.That(flow.Training.Inputs, Is.Empty);
            Assert.That(PlayerPrefs.HasKey(TrainingProgressStore.StorageKey), Is.False);
            CaptureSelection("trill-missing");

            yield return StartTraining(true, .11);
            yield return WaitForFeedback();
            Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.Failed));
            Assert.That(flow.Training.Result.Value.Good, Is.EqualTo(8));
            Assert.That(flow.Training.Result.Value.Miss, Is.Zero);
            Assert.That(flow.Training.ExtraInputs, Is.Zero);
            CaptureSelection("trill-timing");

            yield return StartTraining(true, .03);
            yield return WaitForFeedback();
            Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.Succeeded));
            Assert.That(flow.Training.Completed, Is.False);
            Assert.That(flow.Training.Inputs.Count, Is.EqualTo(8));
            Assert.That(PlayerPrefs.HasKey(TrainingProgressStore.StorageKey), Is.False);

            yield return StartTraining(true, .03);
            Assert.That(flow.Training.Step, Is.EqualTo(TrainingStep.Application));
            yield return WaitForFeedback();
            Assert.That(flow.Training.Result.Value.Perfect, Is.EqualTo(24));
            Assert.That(flow.Training.Completed, Is.False);
            Assert.That(PlayerPrefs.HasKey(TrainingProgressStore.StorageKey), Is.False);

            yield return StartTraining(true, .03, true);
            Assert.That(flow.Training.Step, Is.EqualTo(TrainingStep.Assessment));
            yield return WaitForFeedback();
            Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.Failed));
            Assert.That(flow.Training.Result.Value.Perfect, Is.EqualTo(24));
            Assert.That(flow.Training.Result.Value.Miss, Is.Zero);
            Assert.That(flow.Training.ExtraInputs, Is.EqualTo(1));
            Assert.That(flow.Training.Inputs.Single(input => !input.Matched).Lane, Is.EqualTo(1));
            Assert.That(PlayerPrefs.HasKey(TrainingProgressStore.StorageKey), Is.False);
            CaptureSelection("trill-extra-input");

            yield return StartTraining(true, .03);
            var song = flow.CurrentRequest.Song;
            yield return Await(() => game.ReadModel.Snapshot.SongTime >= 12.1);
            CapturePlayback("trill-assessment");
            yield return WaitForFeedback();
            Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.Succeeded));
            Assert.That(flow.Training.Result.Value.Perfect, Is.EqualTo(24));
            Assert.That(flow.LastResult.CanCompareScore, Is.False);
            Assert.That(song.Mode == null, Is.True, "Completed requests must release their owned mode.");
            Assert.That(new TrainingProgressStore().IsCompleted(lessonId, version), Is.True);
            CaptureSelection("trill-completed");

            yield return StartTraining(false);
            yield return WaitForFeedback();
            Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.Failed));
            Assert.That(flow.Training.Completed, Is.True);
            Assert.That(flow.Rules.Snapshot.Mode, Is.EqualTo(RhythmDojo.Gameplay.PlayMode.Challenge));
            Assert.That(PlayerPrefs.GetString(SelectionPreferences.StorageKey, ""), Is.EqualTo(ordinary));
            yield return Click(Object.FindFirstObjectByType<TutorialView>().backButton);
            Assert.That(flow.Training.State, Is.EqualTo(TrainingState.Ready));
            Assert.That(Object.FindFirstObjectByType<TutorialView>().progress.text, Is.EqualTo("이수 완료"));
            CaptureSelection("trill-selection-completed");
            yield return Click(Object.FindFirstObjectByType<TutorialView>().backButton);
            yield return Await(() => SceneManager.GetActiveScene().path == MainMenuActions.ModeSelectionScenePath);
            Assert.That(flow.Training, Is.Null);

            Object.Destroy(flow.gameObject);
            yield return null;
            yield return OpenTraining();
            flow = Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(flow.Training.Completed, Is.True);
            Assert.That(flow.Training.State, Is.EqualTo(TrainingState.Ready));
            Assert.That(flow.Training.Result, Is.Null);
            Assert.That(Object.FindFirstObjectByType<TutorialView>().progress.text, Is.EqualTo("이수 완료"));
            CaptureSelection("trill-restored");
        }

        private static void CapturePlayback(string name)
        {
            var capture = GameplayHudFlowTests.CaptureGameplayDisplay();
            try
            {
                System.IO.File.WriteAllBytes("Logs/" + name + ".png", capture.EncodeToPNG());
            }
            finally
            {
                Object.Destroy(capture);
            }
        }

        [UnityTest]
        [Category("Endurance")]
        [Timeout(300000)]
        public IEnumerator StairsDenimCanRetryCompleteAndRestoreIndependently() => CompleteAdditionalLesson(1, "stairs-denim", null);

        [UnityTest]
        [Category("Endurance")]
        [Timeout(300000)]
        public IEnumerator JackRequiresRepressAndCanCompleteIndependently() => CompleteAdditionalLesson(2, "jack", "jack");

        [UnityTest]
        [Category("Endurance")]
        [Timeout(300000)]
        public IEnumerator BurstCanRetryCompleteAndRestoreIndependently() => CompleteAdditionalLesson(3, "burst", null);

        [UnityTest]
        [Category("Endurance")]
        [Timeout(300000)]
        public IEnumerator HoldTapRejectsEarlyReleaseAndCanCompleteIndependently() => CompleteAdditionalLesson(4, "hold-tap", "early");

        [UnityTest]
        [Category("Endurance")]
        [Timeout(300000)]
        public IEnumerator ShieldRequiresTimedReleaseAndCanCompleteIndependently() => CompleteAdditionalLesson(5, "shield-release", "late");

        [UnityTest]
        [Category("Endurance")]
        [Timeout(300000)]
        public IEnumerator ChordTrillRejectsSplitPerfectsAndCanCompleteIndependently() => CompleteAdditionalLesson(6, "chord-trill", "chord");

        private IEnumerator CompleteAdditionalLesson(int index, string id, string fault)
        {
            yield return OpenTraining();
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var view = Object.FindFirstObjectByType<TutorialView>();
            var objects = view.GetComponentsInChildren<Transform>(true);
            yield return Click(view.lessonButtons[index]);
            Assert.That(view.GetComponentsInChildren<Transform>(true), Is.EqualTo(objects));
            Assert.That(flow.Training.LessonId, Is.EqualTo(id));
            var lesson = flow.CurrentTrainingLesson;
            int version = lesson.DefinitionVersion;
            CaptureSelection(id + "-ready");
            CaptureSelection(id + "-ready-16x10", 1280, 800);

            yield return StartTraining(false);
            yield return Await(() => game.ReadModel.Snapshot.SongTime >= 3.1);
            CapturePlayback(id + "-demonstration");
            yield return WaitForFeedback();
            Assert.That(flow.CurrentTrainingLesson, Is.SameAs(lesson));
            Assert.That(flow.Training.Step, Is.EqualTo(TrainingStep.Demonstration));
            Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.Succeeded));
            Assert.That(flow.Training.Result.Value.AccuracyRate, Is.EqualTo(100));
            Assert.That(flow.Training.Completed, Is.False);

            yield return StartTraining(fault != null, fault: fault);
            yield return WaitForFeedback();
            Assert.That(flow.Training.Step, Is.EqualTo(TrainingStep.Imitation));
            Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.Failed), id + " must reject its technique error.");
            Assert.That(flow.Training.Completed, Is.False);
            Assert.That(PlayerPrefs.HasKey(TrainingProgressStore.StorageKey), Is.False);
            if (fault == "chord")
            {
                Assert.That(flow.Training.Result.Value.AccuracyRate, Is.EqualTo(100));
                Assert.That(flow.Training.ChordTimingErrors, Is.GreaterThan(0));
                Assert.That(flow.Training.ExtraInputs, Is.Zero);
            }
            else
            {
                Assert.That(flow.Training.Result.Value.Miss, Is.GreaterThan(0));
            }
            CaptureSelection(id + "-technique-feedback");

            foreach (var step in new[] { TrainingStep.Imitation, TrainingStep.Application, TrainingStep.Assessment })
            {
                yield return StartTraining(true);
                Assert.That(flow.Training.Step, Is.EqualTo(step));
                yield return WaitForFeedback();
                Assert.That(flow.CurrentTrainingLesson, Is.SameAs(lesson));
                Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.Succeeded), id + " / " + step);
                Assert.That(flow.Training.ExtraInputs, Is.Zero);
                Assert.That(flow.Training.ChordTimingErrors, Is.Zero);
                Assert.That(flow.Training.Completed, Is.EqualTo(step == TrainingStep.Assessment));
                Assert.That(new TrainingProgressStore().IsCompleted(id, version),
                    Is.EqualTo(step == TrainingStep.Assessment));
                Assert.That(new TrainingProgressStore().IsCompleted("trill", 1), Is.False);
            }
            Assert.That(Object.FindFirstObjectByType<TutorialView>().feedback.text, Does.Contain(lesson.Title + " 이수 완료"));
            CaptureSelection(id + "-completed");

            yield return StartTraining(false);
            Assert.That(flow.Training.Step, Is.EqualTo(TrainingStep.Assessment));
            yield return WaitForFeedback();
            Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.Failed));
            Assert.That(flow.Training.Completed, Is.True);
            yield return Click(Object.FindFirstObjectByType<TutorialView>().backButton);
            view = Object.FindFirstObjectByType<TutorialView>();
            yield return Click(view.lessonButtons[0]);
            Assert.That(flow.Training.Completed, Is.False);
            yield return Click(view.lessonButtons[index]);
            Assert.That(flow.Training.Completed, Is.True);
            Assert.That(flow.Training.NextStep, Is.EqualTo(TrainingStep.Demonstration));
            Assert.That(flow.Training.Result, Is.Null);
            Assert.That(view.lessonLabels[index].text, Does.Contain("이수"));
            yield return Click(view.backButton);
            yield return Await(() => SceneManager.GetActiveScene().path == MainMenuActions.ModeSelectionScenePath);
            Object.Destroy(flow.gameObject);
            yield return null;
            yield return OpenTraining();
            yield return Click(Object.FindFirstObjectByType<TutorialView>().lessonButtons[index]);
            flow = Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(flow.Training.Completed, Is.True);
            Assert.That(flow.Training.State, Is.EqualTo(TrainingState.Ready));
            Assert.That(flow.Training.LessonId, Is.EqualTo(id));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator InvalidLoadDuplicateStartPauseRestartAndExitKeepTrainingIsolated()
        {
            yield return OpenTraining();
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var lesson = flow.CurrentTrainingLesson;
            var invalid = ScriptableObject.CreateInstance<TrainingLesson>();
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => flow.PlayTraining(invalid));
                Assert.That(flow.Transitioning, Is.False);
                Assert.That(flow.CurrentRequest, Is.Null);
                Assert.That(flow.Training.State, Is.EqualTo(TrainingState.Ready));
            }
            finally
            {
                Object.Destroy(invalid);
            }

            var view = Object.FindFirstObjectByType<TutorialView>();
            var objects = view.GetComponentsInChildren<Transform>(true);
            var screen = view.GetComponent<TutorialScreen>();
            screen.Initialize(flow.ShowPracticeModes, lesson, flow.Training, () => flow.PlayTraining(lesson));
            screen.Initialize(flow.ShowPracticeModes, lesson, flow.Training, () => flow.PlayTraining(lesson));
            view.gameObject.SetActive(false);
            view.gameObject.SetActive(true);
            Assert.That(view.GetComponentsInChildren<Transform>(true), Is.EqualTo(objects));

            yield return StartTraining(false);
            var director = UnityEngine.Object.FindFirstObjectByType<GameplayBackgroundDirector>();
            Assert.That(director.room.scenery[0].gameObject.activeInHierarchy, Is.True);
            Assert.That(director.Performance, Is.Not.Null);
            Assert.That(flow.PlayTraining(lesson), Is.False);
            Assert.That(flow.Training.Attempt, Is.EqualTo(1));
            var song = flow.CurrentRequest.Song;
            var mentor = Object.FindFirstObjectByType<TrainingPlaybackView>();
            var playbackObjects = mentor.GetComponentsInChildren<Transform>(true);
            yield return Press(Key.Escape);
            Assert.That(game.IsPaused, Is.True);
            Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.None));
            yield return Click(Object.FindFirstObjectByType<GameplayPauseMenu>().restartButton);
            yield return null;
            Assert.That(flow.Training.Attempt, Is.EqualTo(2));
            Assert.That(flow.CurrentRequest.Song, Is.SameAs(song));
            Assert.That(mentor.GetComponentsInChildren<Transform>(true), Is.EqualTo(playbackObjects));
            yield return Press(Key.Escape);
            yield return Click(Object.FindFirstObjectByType<GameplayPauseMenu>().exitButton);
            yield return WaitForFeedback();
            Assert.That(flow.Training.Outcome, Is.EqualTo(TrainingOutcome.Aborted));
            Assert.That(flow.Training.Result, Is.Null);
            Assert.That(PlayerPrefs.HasKey(TrainingProgressStore.StorageKey), Is.False);

            yield return Click(Object.FindFirstObjectByType<TutorialView>().backButton);
            Assert.That(flow.Training.State, Is.EqualTo(TrainingState.Ready));
            yield return Click(Object.FindFirstObjectByType<TutorialView>().backButton);
            yield return Await(() => SceneManager.GetActiveScene().path == MainMenuActions.ModeSelectionScenePath);
            Assert.That(flow.Training, Is.Null);
            flow.ShowSelection();
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
            yield return null;
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            Assert.That(flow.IsTrainingPlay, Is.False);
            Assert.That(flow.CurrentRequest.Song.SongId, Is.Not.EqualTo("training-basic-pulse"));
        }
    }
}
