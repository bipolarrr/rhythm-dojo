using System;
using System.Collections.Generic;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public enum TrainingStage
    {
        Practice,
        Assessment,
    }

    public enum TrainingState
    {
        Ready,
        Preparing,
        Playing,
        Feedback,
    }

    public enum TrainingOutcome
    {
        None,
        Succeeded,
        Failed,
        Aborted,
    }

    public enum TrainingStep
    {
        Single,
        Demonstration,
        Imitation,
        Application,
        Assessment,
    }

    // Survives scene changes. Story/presentation can observe this small execution contract.
    public sealed class TrainingSession
    {
        public string LessonId { get; }
        public int DefinitionVersion { get; }
        public TrainingStage Stage { get; }
        public TrainingCriteria Criteria { get; }
        public TrainingState State { get; private set; }
        public TrainingOutcome Outcome { get; private set; }
        public int Attempt { get; private set; }
        public bool Completed { get; private set; }
        public SessionSnapshot? Result { get; private set; }
        public TrainingStep Step { get; private set; }
        public bool Guided { get; }
        public int ExtraInputs { get; private set; }
        public int ChordTimingErrors { get; private set; }
        public IReadOnlyList<TrainingInputRecord> Inputs { get; private set; } = Array.Empty<TrainingInputRecord>();

        public TrainingStep NextStep
        {
            get
            {
                if (!Guided || Outcome != TrainingOutcome.Succeeded)
                {
                    return Step;
                }
                switch (Step)
                {
                    case TrainingStep.Demonstration:
                        return TrainingStep.Imitation;
                    case TrainingStep.Imitation:
                        return TrainingStep.Application;
                    case TrainingStep.Application:
                        return TrainingStep.Assessment;
                    default:
                        return Step;
                }
            }
        }

        public TrainingSession(string lessonId, int definitionVersion, TrainingStage stage,
            TrainingCriteria criteria, bool completed, bool guided = false)
        {
            if (string.IsNullOrWhiteSpace(lessonId) || definitionVersion < 1)
            {
                throw new ArgumentException("Training identity and version are required.");
            }
            if (!Enum.IsDefined(typeof(TrainingStage), stage))
            {
                throw new ArgumentOutOfRangeException(nameof(stage));
            }

            LessonId = lessonId;
            DefinitionVersion = definitionVersion;
            Stage = stage;
            Criteria = criteria ?? throw new ArgumentNullException(nameof(criteria));
            Completed = completed;
            Guided = guided;
            if (guided)
            {
                Step = TrainingStep.Demonstration;
            }
        }

        public void BeginAttempt()
        {
            if (State == TrainingState.Preparing || State == TrainingState.Playing)
            {
                throw new InvalidOperationException("A training attempt is already active.");
            }

            Step = NextStep;
            Attempt++;
            State = TrainingState.Preparing;
            Outcome = TrainingOutcome.None;
            Result = null;
            ExtraInputs = 0;
            ChordTimingErrors = 0;
            Inputs = Array.Empty<TrainingInputRecord>();
        }

        public void ResetPlayback()
        {
            if (State == TrainingState.Playing)
            {
                Attempt++;
                State = TrainingState.Preparing;
                Outcome = TrainingOutcome.None;
                Result = null;
            }
        }

        public void MarkPlaying()
        {
            if (State == TrainingState.Preparing)
            {
                State = TrainingState.Playing;
            }
        }

        public bool Finish(int attempt, SessionSnapshot snapshot, IReadOnlyList<TrainingInputRecord> inputs = null)
        {
            if (attempt != Attempt || State != TrainingState.Playing)
            {
                return false;
            }
            if (snapshot.State != SessionState.Completed && snapshot.State != SessionState.GameOver)
            {
                return false;
            }

            Result = snapshot;
            var copy = new List<TrainingInputRecord>();
            ExtraInputs = 0;
            ChordTimingErrors = 0;
            if (inputs != null)
            {
                foreach (var input in inputs)
                {
                    copy.Add(input);
                    if (!input.Synchronized)
                    {
                        ChordTimingErrors++;
                    }
                    if (!input.Matched)
                    {
                        ExtraInputs++;
                    }
                }
            }
            Inputs = copy.AsReadOnly();
            State = TrainingState.Feedback;
            Outcome = TrainingOutcome.Failed;
            if (Criteria.IsSatisfied(snapshot) && ExtraInputs == 0 && ChordTimingErrors == 0)
            {
                Outcome = TrainingOutcome.Succeeded;
                if (Step == TrainingStep.Single || Step == TrainingStep.Assessment)
                {
                    Completed = true;
                }
            }
            return true;
        }

        public void Abort()
        {
            if (State != TrainingState.Preparing && State != TrainingState.Playing)
            {
                return;
            }

            Result = null;
            Outcome = TrainingOutcome.Aborted;
            State = TrainingState.Feedback;
        }

        public void ReturnToSelection()
        {
            if (State == TrainingState.Playing || State == TrainingState.Preparing)
            {
                throw new InvalidOperationException("Finish or abort playback before returning to selection.");
            }
            Step = TrainingStep.Single;
            if (Guided)
            {
                Step = TrainingStep.Demonstration;
            }
            State = TrainingState.Ready;
            Outcome = TrainingOutcome.None;
            Result = null;
            ExtraInputs = 0;
            ChordTimingErrors = 0;
            Inputs = Array.Empty<TrainingInputRecord>();
        }
    }
}
