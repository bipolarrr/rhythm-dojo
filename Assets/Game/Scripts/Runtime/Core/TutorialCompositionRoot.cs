using System;
using RhythmDojo.Audio;
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
        [SerializeField] private TitleMusicPlayer music;
        [SerializeField] private TitleMusicSettings titleMusic;
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
            if (!music || !titleMusic)
            {
                throw new InvalidOperationException("Tutorial music composition missing.");
            }
            screen.Validate();
            titleMusic.Validate();
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
        }

        private void Start()
        {
            Validate();
            flow = AppFlowController.Create(gameSettings);
            flow.CancelSelectionPreparation();
            music.Initialize(titleMusic);
            screen.ConfigureLessons(lessons, SelectLesson, flow.TrainingProgress);
            var selected = lesson;
            if (Array.IndexOf(lessons, flow.CurrentTrainingLesson) >= 0)
            {
                selected = flow.CurrentTrainingLesson;
            }
            SelectLesson(selected);
        }

        private void SelectLesson(TrainingLesson selected)
        {
            screen.Initialize(Return, selected, flow.PrepareTraining(selected), () => flow.PlayTraining(selected));
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
