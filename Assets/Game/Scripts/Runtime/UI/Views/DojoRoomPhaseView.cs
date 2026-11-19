using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class DojoRoomPhaseView : MonoBehaviour
    {
        public Camera stageCamera;
        public DojoTrainingStage[] scenery;
        public Sprite[] poses;
        public Sprite[] effects;

        private double strikeTime;
        private double mistakeTime;
        private double finishStart;
        private int reactionPhase;
        public float VisualTime { get; private set; }
        public float RecentFlow { get; private set; }
        public float Strength { get; private set; }
        public int Strikes { get; private set; }
        public bool Finishing { get; private set; }
        public bool Destroyed { get; private set; }
        public float FinishAge { get; private set; }

        public void Validate()
        {
            if (!stageCamera || scenery == null || scenery.Length != 4)
            {
                throw new InvalidOperationException("The stage camera and four training stages are required.");
            }
            if (poses == null || poses.Length != 24 || Array.Exists(poses, sprite => !sprite))
            {
                throw new InvalidOperationException("The trainee action drawings are incomplete.");
            }
            if (effects == null || effects.Length != 42 || Array.Exists(effects, sprite => !sprite))
            {
                throw new InvalidOperationException("The stage effect drawings are incomplete.");
            }
            foreach (var stage in scenery)
            {
                if (!stage)
                {
                    throw new InvalidOperationException("A training stage is missing.");
                }
                stage.Validate();
            }
        }

        public void ResetPresentation()
        {
            strikeTime = mistakeTime = double.NegativeInfinity;
            RecentFlow = 0;
            Strikes = 0;
            Strength = 0;
            Finishing = Destroyed = false;
            FinishAge = 0;
            Step(0, 0, 0, 0, 1, 0);
        }

        public void Hit(int phase, double time, float strength)
        {
            RecentFlow = Mathf.Min(1, RecentFlow + .085f * strength);
            if (reactionPhase == phase && time - strikeTime < .16)
            {
                return;
            }
            reactionPhase = phase;
            strikeTime = time;
            Strength = strength;
            Strikes++;
        }

        public void Miss(double time)
        {
            RecentFlow *= .64f;
            if (time - mistakeTime > .50)
            {
                mistakeTime = time;
            }
        }

        public void BeginFinish(bool flawless)
        {
            if (!Finishing)
            {
                finishStart = Time.unscaledTimeAsDouble;
                Finishing = true;
                Destroyed = flawless;
            }
            FinishAge = (float)(Time.unscaledTimeAsDouble - finishStart);
        }

        public void Step(int phase, double time, float age, float beat, float overlap, float duration)
        {
            VisualTime = (float)time;
            float width = stageCamera.orthographicSize * 2 * stageCamera.aspect;
            float travel = Mathf.SmoothStep(0, 1, overlap);
            float cameraStage = phase;
            if (phase > 0)
            {
                cameraStage = phase - 1 + travel;
            }
            stageCamera.transform.position = new Vector3(cameraStage * width, 135, -24);

            for (int i = 0; i < scenery.Length; i++)
            {
                bool leaving = i == phase - 1 && travel < 1;
                var stage = scenery[i];
                bool visible = i == phase || leaving;
                if (stage.gameObject.activeSelf != visible)
                {
                    stage.gameObject.SetActive(visible);
                }
                if (!visible)
                {
                    continue;
                }

                float localTime = age;
                if (leaving)
                {
                    localTime += duration;
                }
                stage.Frame(width, i * width);
                float hit = float.PositiveInfinity;
                if (reactionPhase == i)
                {
                    hit = (float)(time - strikeTime);
                }
                stage.Step(this, localTime, beat, hit, (float)(time - mistakeTime));
            }
        }

        public void Effect(SpriteRenderer item, int row, int frame)
        {
            item.sprite = effects[row * 6 + Mathf.Clamp(frame, 0, 5)];
        }
    }
}
