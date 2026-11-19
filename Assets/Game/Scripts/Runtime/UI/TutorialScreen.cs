using System;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace RhythmDojo.UI
{
    public sealed class TutorialScreen : MonoBehaviour
    {
        [SerializeField] private TutorialView view;
        [SerializeField] private DialogueScreen dialogue;
        private TrainingLesson currentLesson;
        private TrainingSession currentSession;
        private Action returnRequested;
        private Func<bool> startRequested;
        private float age;
        private TrainingLesson[] lessons;
        private UnityEngine.Events.UnityAction[] lessonActions;

        public void ConfigureLessons(TrainingLesson[] lessons, Action<TrainingLesson> onSelect,
            TrainingProgressStore progress)
        {
            Validate();
            ClearLessonActions();
            if (lessons.Length != view.lessonButtons.Length)
            {
                throw new InvalidOperationException("Tutorial lessons do not match authored buttons.");
            }
            this.lessons = lessons;
            lessonActions = new UnityEngine.Events.UnityAction[lessons.Length];
            for (int i = 0; i < lessons.Length; i++)
            {
                var selected = lessons[i];
                view.lessonLabels[i].text = selected.Title;
                if (progress.IsCompleted(selected.LessonId, selected.DefinitionVersion))
                {
                    view.lessonLabels[i].text += " · 이수";
                }
                lessonActions[i] = () => onSelect(selected);
                view.lessonButtons[i].onClick.AddListener(lessonActions[i]);
            }
        }

        private void ClearLessonActions()
        {
            if (lessonActions == null)
            {
                return;
            }
            for (int i = 0; i < lessonActions.Length; i++)
            {
                view.lessonButtons[i].onClick.RemoveListener(lessonActions[i]);
            }
            lessonActions = null;
        }

        public void Validate()
        {
            if (!view)
            {
                throw new InvalidOperationException("Tutorial view missing.");
            }
            view.Validate();
            if (!dialogue)
            {
                throw new InvalidOperationException("Tutorial dialogue missing. Rebuild Tutorial.");
            }
            dialogue.Validate();
        }

        public void Initialize(Action onReturn)
        {
            Validate();
            dialogue.Cancel();
            currentLesson = null;
            currentSession = null;
            view.storyRoot.gameObject.SetActive(true);
            view.introduction.gameObject.SetActive(true);
            view.backButton.onClick.RemoveListener(Return);
            view.startButton.onClick.RemoveListener(StartTraining);
            startRequested = null;
            view.startButton.interactable = false;
            returnRequested = onReturn ?? throw new ArgumentNullException(nameof(onReturn));
            view.backButton.onClick.AddListener(Return);
            age = 0;
            view.Reveal(0);
        }

        public void Initialize(Action onReturn, TrainingLesson lesson, TrainingSession session, Func<bool> onStart)
        {
            Initialize(onReturn);
            currentLesson = lesson;
            currentSession = session;
            startRequested = onStart ?? throw new ArgumentNullException(nameof(onStart));
            view.startButton.onClick.AddListener(StartTraining);
            view.startButton.interactable = true;
            view.backButton.interactable = true;
            view.lessonTitle.text = lesson.Title;
            string stage = "수련";
            if (session.Stage == TrainingStage.Assessment)
            {
                stage = "검정";
            }
            var keys = new string[lesson.Song.Mode.LaneCount];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = PlayKeyBindings.GetLabel(lesson.Song.Mode, lesson.Song.Mode.name, i);
            }
            view.instructions.text = $"{stage} · {lesson.PatternDescription}\n"
                + "1 / 2 / 3 / 4 레인 = " + string.Join(" / ", keys)
                + $"\n성공 조건: 끝까지 연주 · Miss {session.Criteria.MaximumMisses}개 이하"
                + $" · 정확도 {session.Criteria.MinimumAccuracy:0.#}% 이상";
            if (lesson.Guided)
            {
                view.instructions.text += " · 추가 입력 0회";
            }
            if (lesson.MaximumChordSpreadSeconds > 0)
            {
                view.instructions.text += $"\n동시 입력 차이 {lesson.MaximumChordSpreadSeconds * 1000:0}ms 이내";
            }

            view.progress.text = "미완료";
            if (session.Completed)
            {
                view.progress.text = "이수 완료";
            }
            view.startLabel.text = "수련 시작";
            if (session.State == TrainingState.Feedback)
            {
                view.startLabel.text = "재도전";
            }
            view.backLabel.text = "〈  모드 선택";
            if (session.State == TrainingState.Feedback)
            {
                view.backLabel.text = "〈  수련 선택";
            }
            switch (session.Outcome)
            {
                case TrainingOutcome.Succeeded:
                    view.feedback.text = "수련 성공";
                    view.startLabel.text = "다음 단계";
                    if (session.Step == TrainingStep.Assessment)
                    {
                        view.feedback.text = lesson.Title + " 이수 완료 · 완료 상태가 저장되었습니다.";
                        view.startLabel.text = "검정 다시 하기";
                    }
                    else if (session.Step == TrainingStep.Demonstration)
                    {
                        view.feedback.text = "시범이 끝났습니다. 이제 직접 따라 해보세요.";
                    }
                    break;
                case TrainingOutcome.Failed:
                    view.feedback.text = "수련 실패 · 성공 조건에 도달하지 못했습니다.";
                    break;
                case TrainingOutcome.Aborted:
                    view.feedback.text = "수련 중단 · 다시 도전할 수 있습니다.";
                    break;
                default:
                    view.feedback.text = "준비되면 수련을 시작하세요.";
                    if (lesson.Guided)
                    {
                        view.feedback.text = "시범 → 따라 하기 → 응용 → 검정\n준비되면 수련을 시작하세요.";
                    }
                    break;
            }
            if (session.Result is { } result && session.Step != TrainingStep.Demonstration)
            {
                view.feedback.text += $"\n정확도 {result.AccuracyRate:0.#}% · Miss {result.Miss} · 추가 입력 {session.ExtraInputs}회";
                if (lesson.MaximumChordSpreadSeconds > 0)
                {
                    view.feedback.text += $" · 동시 입력 어긋남 {session.ChordTimingErrors}회";
                }
            }
            if (lesson.Guided && session.State == TrainingState.Feedback)
            {
                view.lessonTitle.text += " · " + TrainingLesson.StepTitle(session.NextStep);
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
            if (startRequested == null || !view.startButton.interactable)
            {
                return;
            }

            view.startButton.interactable = false;
            view.backButton.interactable = false;
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
                    view.feedback.text = "수련 준비 중…";
                    return;
                }
            }
            catch (Exception exception)
            {
                view.feedback.text = "수련을 시작할 수 없습니다.\n" + exception.Message;
            }
            SetDialogueControls(false);
        }

        private void SetDialogueControls(bool active)
        {
            view.startButton.interactable = !active;
            view.backButton.interactable = !active;
            view.storyRoot.gameObject.SetActive(!active);
            view.introduction.gameObject.SetActive(!active);
            if (lessons != null)
            {
                for (int i = 0; i < lessons.Length; i++)
                {
                    bool canSelect = !active && currentSession?.State == TrainingState.Ready;
                    view.lessonButtons[i].interactable = canSelect && lessons[i] != currentLesson;
                }
            }
            if (!active && EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(view.startButton.gameObject);
            }
        }

        private void Update()
        {
            if (returnRequested == null)
            {
                return;
            }
            if (dialogue.IsActive)
            {
                return;
            }
            if (age < .45f)
            {
                age += Time.unscaledDeltaTime;
                view.Reveal(age / .45f);
            }
            bool cancel = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
            cancel |= Gamepad.current?.buttonEast.wasPressedThisFrame == true;
            if (cancel)
            {
                Return();
            }
        }

        private void Return()
        {
            if (view.backButton.interactable)
            {
                if (currentSession?.State == TrainingState.Feedback)
                {
                    currentSession.ReturnToSelection();
                    Initialize(returnRequested, currentLesson, currentSession, startRequested);
                    return;
                }
                returnRequested?.Invoke();
            }
        }

        private void OnDisable()
        {
            if (dialogue)
            {
                dialogue.Cancel();
            }
            if (view && startRequested != null)
            {
                SetDialogueControls(false);
            }
        }

        private void OnDestroy()
        {
            ClearLessonActions();
            if (view && view.backButton)
            {
                view.backButton.onClick.RemoveListener(Return);
            }
            returnRequested = null;
            if (view && view.startButton)
            {
                view.startButton.onClick.RemoveListener(StartTraining);
            }
            startRequested = null;
        }
    }
}
