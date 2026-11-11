using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    [Serializable]
    public sealed class DojoLessonSequence
    {
        public enum Step
        {
            // Keep Signal's enum value for compatibility; lessons no longer enter it.
            Waiting, Signal, Demonstration, Delay, Imitation, Approval
        }

        [Range(.2f, .5f)] public float imitationDelay = .35f;
        public Step Phase { get; private set; }
        public DojoIdleVariation.Motion Motion { get; private set; }
        public int CompletedSets { get; private set; }
        private float elapsed;

        public void Cancel()
        {
            Phase = Step.Waiting;
            Motion = DojoIdleVariation.Motion.None;
            elapsed = 0;
        }

        public void Advance(float seconds, float age, DojoIdleVariation choices,
            DojoActorAnimation teacher, DojoActorAnimation trainee)
        {
            teacher.Sample(DojoIdleVariation.Motion.None, age);
            trainee.Sample(DojoIdleVariation.Motion.None, age + .37f);
            if (Phase == Step.Waiting)
            {
                choices.Advance(seconds, 0, true);
                if (choices.IsPlaying)
                {
                    Motion = choices.Current.motion;
                    Enter(Step.Demonstration);
                    teacher.Sample(Motion, 0);
                }
                return;
            }

            elapsed += seconds;
            switch (Phase)
            {
                case Step.Demonstration:
                    teacher.Sample(Motion, elapsed);
                    if (elapsed >= teacher.Duration(Motion))
                    {
                        Enter(Step.Delay);
                    }
                    break;
                case Step.Delay:
                    if (elapsed >= imitationDelay)
                    {
                        Enter(Step.Imitation);
                        trainee.Sample(Motion, 0);
                    }
                    break;
                case Step.Imitation:
                    trainee.Sample(Motion, elapsed);
                    if (elapsed >= trainee.Duration(Motion))
                    {
                        Enter(Step.Approval);
                    }
                    break;
                case Step.Approval:
                    teacher.Sample(DojoIdleVariation.Motion.Approve, elapsed);
                    if (elapsed >= teacher.Duration(DojoIdleVariation.Motion.Approve))
                    {
                        CompletedSets++;
                        choices.Cancel();
                        Cancel();
                    }
                    break;
            }
        }

        private void Enter(Step step)
        {
            Phase = step;
            elapsed = 0;
        }
    }
}
