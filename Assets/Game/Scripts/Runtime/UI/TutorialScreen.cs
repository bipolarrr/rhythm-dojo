using System;
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
        private Action returnRequested;
        private float age;
        private TrainingLesson[] lessons;
        private UnityEngine.Events.UnityAction[] lessonActions;
        private TrainingProgressStore trainingProgress;

        public void ValidateLessonIds(System.Collections.Generic.HashSet<string> ids)
        {
            if (ids.Count != view.lessonIds.Length || !ids.SetEquals(view.lessonIds))
            {
                throw new InvalidOperationException("Tutorial lessons do not match the authored tablet IDs.");
            }
        }

        public void ConfigureLessons(TrainingLesson[] lessons, Action<TrainingLesson> onSelect,
            TrainingProgressStore progress)
        {
            Validate();
            ClearLessonActions();
            if (lessons.Length != view.lessonButtons.Length)
            {
                throw new InvalidOperationException("Tutorial lessons do not match authored buttons.");
            }
            this.lessons = new TrainingLesson[lessons.Length];
            trainingProgress = progress;
            lessonActions = new UnityEngine.Events.UnityAction[lessons.Length];
            for (int i = 0; i < lessons.Length; i++)
            {
                var id = view.lessonIds[i];
                var selected = Array.Find(lessons, entry => entry && entry.LessonId == id);
                if (!selected)
                {
                    throw new InvalidOperationException("Missing tutorial lesson for tablet " + id);
                }
                this.lessons[i] = selected;
                view.lessonLabels[i].text = selected.Title;
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
        }

        public void Initialize(Action onReturn, TrainingLesson selected = null)
        {
            Validate();
            returnRequested = onReturn ?? throw new ArgumentNullException(nameof(onReturn));
            view.backButton.onClick.RemoveListener(Return);
            view.backButton.onClick.AddListener(Return);
            int selectedTablet = 0;
            for (int i = 0; i < lessons.Length; i++)
            {
                var entry = lessons[i];
                bool completed = trainingProgress.IsCompleted(entry.LessonId, entry.DefinitionVersion);
                view.completionSeals[i].SetActive(completed);
                if (entry == selected)
                {
                    selectedTablet = i;
                }
            }
            if (EventSystem.current)
            {
                view.lessonButtons[selectedTablet].Select();
            }
            age = 0;
            view.Reveal(0);
        }

        private void Update()
        {
            if (returnRequested == null)
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
            returnRequested?.Invoke();
        }

        private void OnDestroy()
        {
            ClearLessonActions();
            if (view && view.backButton)
            {
                view.backButton.onClick.RemoveListener(Return);
            }
        }
    }
}
