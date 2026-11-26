using System;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class TrainingPlaybackView : MonoBehaviour
    {
        public GameObject trainingPanel;
        public Text lessonTitle;
        public Text instructions;
        public Text progress;
        public Text feedback;
        public Button startButton;
        public Text startLabel;
        public Button backButton;
        public Text backLabel;
        public DialogueScreen dialogue;

        private TrainingLesson currentLesson;
        private TrainingSession currentSession;
        private Action returnRequested;
        private Func<bool> startRequested;
        private int briefingFrame;
        private bool waitingForSubmitRelease;

        public void Validate()
        {
            if (!trainingPanel || !lessonTitle || !instructions)
            {
                throw new InvalidOperationException("Training briefing is incomplete.");
            }
            if (!progress || !feedback || !dialogue)
            {
                throw new InvalidOperationException("Training feedback is incomplete.");
            }
            if (!startButton || !startLabel)
            {
                throw new InvalidOperationException("Training start control is missing.");
            }
            if (!backButton || !backLabel)
            {
                throw new InvalidOperationException("Training return control is missing.");
            }
            dialogue.Validate();
        }

        public void Initialize(Action onReturn, TrainingLesson lesson, TrainingSession session, Func<bool> onStart)
        {
            Validate();
            Unbind();
            currentLesson = lesson;
            currentSession = session;
            returnRequested = onReturn ?? throw new ArgumentNullException(nameof(onReturn));
            startRequested = onStart ?? throw new ArgumentNullException(nameof(onStart));
            briefingFrame = Time.frameCount;
            waitingForSubmitRelease = SubmitHeld();
            gameObject.SetActive(true);
            startButton.onClick.AddListener(StartTraining);
            backButton.onClick.AddListener(Return);
            lessonTitle.text = lesson.Title;
            string stage = "수련";
            if (lesson.Guided)
            {
                stage = TrainingLesson.StepTitle(session.NextStep);
            }
            else if (session.Stage == TrainingStage.Assessment)
            {
                stage = "검정";
            }
            var keys = new string[lesson.Song.Mode.LaneCount];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = PlayKeyBindings.GetLabel(lesson.Song.Mode, lesson.Song.Mode.name, i);
            }
            instructions.text = $"{stage} · {lesson.PatternDescription}\n"
                + "1 / 2 / 3 / 4 레인 = " + string.Join(" / ", keys)
                + $"\n성공 조건: 끝까지 연주 · Miss {session.Criteria.MaximumMisses}개 이하"
                + $" · 정확도 {session.Criteria.MinimumAccuracy:0.#}% 이상";
            if (lesson.Guided)
            {
                instructions.text += "\n추가 입력 0회";
            }
            if (lesson.MaximumChordSpreadSeconds > 0)
            {
                instructions.text += $" · 동시 입력 차이 {lesson.MaximumChordSpreadSeconds * 1000:0}ms 이내";
            }

            progress.text = "미완료";
            if (session.Completed)
            {
                progress.text = "이수 완료";
            }
            startLabel.text = "수련 시작";
            if (session.State == TrainingState.Feedback)
            {
                startLabel.text = "재도전";
            }
            backLabel.text = "〈  수련 선택";
            switch (session.Outcome)
            {
                case TrainingOutcome.Succeeded:
                    feedback.text = "수련 성공";
                    startLabel.text = "다음 단계";
                    if (session.Step == TrainingStep.Assessment)
                    {
                        feedback.text = lesson.Title + " 이수 완료 · 완료 상태가 저장되었습니다.";
                        startLabel.text = "검정 다시 하기";
                    }
                    else if (session.Step == TrainingStep.Demonstration)
                    {
                        feedback.text = "시범이 끝났습니다. 이제 직접 따라 해보세요.";
                    }
                    break;
                case TrainingOutcome.Failed:
                    feedback.text = "수련 실패 · 성공 조건에 도달하지 못했습니다.";
                    break;
                case TrainingOutcome.Aborted:
                    feedback.text = "수련 중단 · 다시 도전할 수 있습니다.";
                    break;
                default:
                    feedback.text = "준비되면 수련을 시작하세요.";
                    if (lesson.Guided)
                    {
                        feedback.text = "시범 → 따라 하기 → 응용 → 검정\n준비되면 수련을 시작하세요.";
                    }
                    break;
            }
            if (session.Result is { } result && session.Step != TrainingStep.Demonstration)
            {
                feedback.text += $"\n정확도 {result.AccuracyRate:0.#}% · Miss {result.Miss} · 추가 입력 {session.ExtraInputs}회";
                if (lesson.MaximumChordSpreadSeconds > 0)
                {
                    feedback.text += $" · 동시 입력 어긋남 {session.ChordTimingErrors}회";
                }
            }
            if (lesson.Guided && session.State == TrainingState.Feedback)
            {
                lessonTitle.text += " · " + TrainingLesson.StepTitle(session.NextStep);
            }
            var feedbackDialogue = lesson.CreateFeedback(session);
            if (feedbackDialogue.Length > 0)
            {
                SetDialogueControls(true);
                dialogue.Play(feedbackDialogue, () => SetDialogueControls(false));
            }
            else
            {
                SetDialogueControls(false);
            }
        }

        private void StartTraining()
        {
            if (startRequested == null || !startButton.interactable)
            {
                return;
            }
            if (Time.frameCount == briefingFrame || waitingForSubmitRelease)
            {
                return;
            }

            startButton.interactable = false;
            backButton.interactable = false;
            var introduction = currentLesson.IntroductionForStep(currentSession.NextStep);
            if (introduction)
            {
                SetDialogueControls(true);
                dialogue.Play(introduction, LaunchTraining);
                return;
            }
            LaunchTraining();
        }

        private void LaunchTraining()
        {
            try
            {
                if (startRequested())
                {
                    gameObject.SetActive(false);
                    return;
                }
            }
            catch (Exception exception)
            {
                feedback.text = "수련을 시작할 수 없습니다.\n" + exception.Message;
            }
            SetDialogueControls(false);
        }

        private void SetDialogueControls(bool active)
        {
            startButton.interactable = !active;
            backButton.interactable = !active;
            trainingPanel.SetActive(!active);
            if (!active && EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
            }
        }

        private void Update()
        {
            if (waitingForSubmitRelease && !SubmitHeld())
            {
                waitingForSubmitRelease = false;
            }
            if (dialogue.IsActive || Time.frameCount == briefingFrame)
            {
                return;
            }
            bool cancel = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
            cancel |= Gamepad.current?.buttonEast.wasPressedThisFrame == true;
            if (cancel)
            {
                Return();
            }
        }

        private static bool SubmitHeld()
        {
            var module = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            if (module && module.submit.action.IsPressed())
            {
                return true;
            }
            return Gamepad.current?.buttonSouth.isPressed == true
                || Keyboard.current?.enterKey.isPressed == true || Keyboard.current?.spaceKey.isPressed == true;
        }

        private void Return()
        {
            if (backButton.interactable && !dialogue.IsActive)
            {
                returnRequested?.Invoke();
            }
        }

        public void Unbind()
        {
            dialogue.Cancel();
            startButton.onClick.RemoveListener(StartTraining);
            backButton.onClick.RemoveListener(Return);
            startRequested = null;
            returnRequested = null;
        }

        private void OnDisable()
        {
            dialogue.Cancel();
        }

        private void OnEnable()
        {
            if (startRequested != null)
            {
                SetDialogueControls(false);
            }
        }
    }
}
