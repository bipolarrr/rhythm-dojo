using System;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;

namespace RhythmDojo.Core
{
    public sealed class TutorialCompositionRoot : MonoBehaviour
    {
        [SerializeField] private TutorialScreen screen;
        [SerializeField] private GameSettings gameSettings;
        [SerializeField] private TrainingLesson lesson;
        [SerializeField] private TrainingLesson[] lessons;
        private AppFlowController flow;
        private bool returning;

        public void Validate()
        {
            if (!screen || !gameSettings)
            {
                throw new InvalidOperationException("Tutorial scene dependencies missing.");
            }
            screen.Validate();
            if (!lesson)
            {
                throw new InvalidOperationException("Tutorial training lesson missing. Rebuild Tutorial.");
            }
            lesson.Validate();
            if (lessons == null || lessons.Length == 0)
            {
                throw new InvalidOperationException("Tutorial lessons missing. Rebuild Tutorial.");
            }
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var selected in lessons)
            {
                if (!selected || !ids.Add(selected.LessonId))
                {
                    throw new InvalidOperationException("Tutorial lesson is missing or duplicated.");
                }
                selected.Validate();
            }
            screen.ValidateLessonIds(ids);
        }

        private void Start()
        {
            Validate();
            flow = AppFlowController.Create(gameSettings);
            flow.CancelSelectionPreparation();
            screen.ConfigureLessons(lessons, SelectLesson, flow.TrainingProgress);
            screen.Initialize(Return, flow.CurrentTrainingLesson ?? lesson);
        }

        private void SelectLesson(TrainingLesson selected)
        {
            flow.PlayTraining(selected);
        }

        private void Return()
        {
            if (returning || flow.Transitioning)
            {
                return;
            }
            returning = true;
            flow.ShowPracticeModes();
        }
    }
}
