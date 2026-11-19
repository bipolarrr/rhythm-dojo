using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using RhythmDojo.Dialogue;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.Content
{
    [CreateAssetMenu(menuName = "Rhythm Dojo/Training Lesson")]
    public sealed class TrainingLesson : ScriptableObject
    {
        [SerializeField] private string lessonId;
        [SerializeField] private string title;
        [SerializeField] private string patternDescription;
        [SerializeField] private bool guided;
        [SerializeField] private int definitionVersion = 1;
        [SerializeField] private TrainingStage stage;
        [SerializeField] private SongDefinition song;
        [SerializeField] private int maximumMisses;
        [SerializeField] private double minimumAccuracy;
        [SerializeField] private HoldEndMode holdEnd = HoldEndMode.Automatic;
        [SerializeField] private double maximumChordSpreadSeconds;
        [SerializeField] private DialogueSequenceAsset introductionDialogue;
        [SerializeField] private DialogueSequenceAsset successDialogue;
        [SerializeField] private DialogueSequenceAsset failureDialogue;
        [SerializeField] private DialogueSequenceAsset abortedDialogue;
        [SerializeField] private SongDefinition imitationSong;
        [SerializeField] private SongDefinition applicationSong;
        [SerializeField] private DialogueSequenceAsset imitationDialogue;
        [SerializeField] private DialogueSequenceAsset applicationDialogue;
        [SerializeField] private DialogueSequenceAsset assessmentDialogue;
        [SerializeField] private DialogueSequenceAsset demonstrationFinishedDialogue;
        [SerializeField] private DialogueSequenceAsset practiceSucceededDialogue;
        [SerializeField] private DialogueSequenceAsset missingDialogue;
        [SerializeField] private DialogueSequenceAsset timingDialogue;
        [SerializeField] private DialogueSequenceAsset extraInputDialogue;

        public string LessonId => lessonId;
        public string Title => title;
        public string PatternDescription => patternDescription;
        public int DefinitionVersion => definitionVersion;
        public TrainingStage Stage => stage;
        public SongDefinition Song => song;
        public TrainingCriteria Criteria => new TrainingCriteria(maximumMisses, minimumAccuracy);
        public DialogueSequenceAsset IntroductionDialogue => introductionDialogue;
        public bool Guided => guided;
        public double MaximumChordSpreadSeconds => maximumChordSpreadSeconds;

        public SongDefinition SongForStep(TrainingStep step)
        {
            switch (step)
            {
                case TrainingStep.Demonstration:
                case TrainingStep.Imitation:
                    return imitationSong;
                case TrainingStep.Application:
                    return applicationSong;
                default:
                    return song;
            }
        }

        public DialogueSequenceAsset IntroductionForStep(TrainingStep step)
        {
            switch (step)
            {
                case TrainingStep.Imitation:
                    return imitationDialogue;
                case TrainingStep.Application:
                    return applicationDialogue;
                case TrainingStep.Assessment:
                    return assessmentDialogue;
                default:
                    return introductionDialogue;
            }
        }

        public static string StepTitle(TrainingStep step)
        {
            switch (step)
            {
                case TrainingStep.Demonstration:
                    return "1 / 4 · 사형의 시범";
                case TrainingStep.Imitation:
                    return "2 / 4 · 따라 하기";
                case TrainingStep.Application:
                    return "3 / 4 · 응용";
                case TrainingStep.Assessment:
                    return "4 / 4 · 짧은 검정";
                default:
                    return "수련";
            }
        }

        public DialogueLine[] CreateFeedback(TrainingSession session)
        {
            var lines = new List<DialogueLine>();
            var feedback = FeedbackDialogue(session.Outcome);
            if (Guided && session.Outcome == TrainingOutcome.Succeeded)
            {
                if (session.Step == TrainingStep.Demonstration)
                {
                    feedback = demonstrationFinishedDialogue;
                }
                else if (session.Step != TrainingStep.Assessment)
                {
                    feedback = practiceSucceededDialogue;
                }
            }
            if (feedback)
            {
                lines.AddRange(feedback.CreateLines());
            }
            if (session.Outcome == TrainingOutcome.Failed && session.Result.HasValue)
            {
                var result = session.Result.Value;
                if (result.Miss > 0 && missingDialogue)
                {
                    lines.AddRange(missingDialogue.CreateLines());
                }
                bool timingFailed = result.Good > 0 || session.ChordTimingErrors > 0;
                if (timingFailed && timingDialogue)
                {
                    lines.AddRange(timingDialogue.CreateLines());
                }
                if (session.ExtraInputs > 0 && extraInputDialogue)
                {
                    lines.AddRange(extraInputDialogue.CreateLines());
                }
            }
            return lines.ToArray();
        }

        public DialogueSequenceAsset FeedbackDialogue(TrainingOutcome outcome)
        {
            switch (outcome)
            {
                case TrainingOutcome.Succeeded:
                    return successDialogue;
                case TrainingOutcome.Failed:
                    return failureDialogue;
                case TrainingOutcome.Aborted:
                    return abortedDialogue;
                default:
                    return null;
            }
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(lessonId) || string.IsNullOrWhiteSpace(title))
            {
                throw new InvalidOperationException("Training ID and title are required.");
            }
            if (definitionVersion < 1 || !Enum.IsDefined(typeof(TrainingStage), stage))
            {
                throw new InvalidOperationException("Training version or stage is invalid.");
            }
            if (!song)
            {
                throw new InvalidOperationException("Training song reference is missing.");
            }

            song.Validate();
            if (song.Chart.ToChartData().Count == 0)
            {
                throw new InvalidOperationException("Training needs a nonempty chart.");
            }
            _ = Criteria;
            if (!Enum.IsDefined(typeof(HoldEndMode), holdEnd))
            {
                throw new InvalidOperationException("Training hold release rule is invalid.");
            }
            if (!double.IsFinite(maximumChordSpreadSeconds) || maximumChordSpreadSeconds < 0)
            {
                throw new InvalidOperationException("Training chord timing limit is invalid.");
            }
            if (Guided)
            {
                ValidateGuidedSong(imitationSong);
                ValidateGuidedSong(applicationSong);
                ValidateGuidedSong(song);
                var dialogues = new[] { introductionDialogue, imitationDialogue, applicationDialogue,
                    assessmentDialogue, demonstrationFinishedDialogue, practiceSucceededDialogue,
                    successDialogue, failureDialogue, abortedDialogue, missingDialogue, timingDialogue, extraInputDialogue };
                foreach (var dialogue in dialogues)
                {
                    if (!dialogue)
                    {
                        throw new InvalidOperationException("Guided training dialogue missing.");
                    }
                    dialogue.CreateLines();
                }
            }
        }

        private static void ValidateGuidedSong(SongDefinition song)
        {
            if (!song)
            {
                throw new InvalidOperationException("Guided training song missing.");
            }
            song.Validate();
            var chart = song.Chart.ToChartData();
            if (chart.Count == 0 || song.Mode.LaneCount != 4)
            {
                throw new InvalidOperationException("Guided training requires a nonempty four-lane chart.");
            }
            _ = new TrainingPlayback(chart, false);
        }

        public PlayRules CreateRules()
        {
            // Stable lesson rules; user timing/key/scroll preferences still apply.
            // Bump DefinitionVersion when content, criteria or these rules change.
            return new PlayRules
            {
                Profile = JudgmentProfile.Warmup,
                Mode = RhythmDojo.Gameplay.PlayMode.Gauge,
                GaugeFailure = GaugeFailureMode.Continue,
                HoldEnd = holdEnd,
            };
        }
    }
}
