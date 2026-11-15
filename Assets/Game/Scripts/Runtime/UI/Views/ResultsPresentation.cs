using System;
using System.Globalization;
using RhythmDojo.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class ResultsPresentation : MonoBehaviour
    {
        public const float CountStart = .55f;
        public const float StampTime = 1.58f;
        public const float Duration = 2.02f;

        [Serializable]
        public sealed class Track
        {
            public CanvasGroup group;
            public RectTransform rect;
            public ResultsBrushReveal brush;
            public float delay;
            public float duration = .28f;
            public Vector2 offset;
            public Vector2 scale = Vector2.one;
            public bool overshoot;
            private Vector2 home;
            private Vector3 homeScale;
            private bool captured;

            public void Apply(float elapsed)
            {
                if (!captured)
                {
                    home = rect.anchoredPosition;
                    homeScale = rect.localScale;
                    captured = true;
                }
                float t = Mathf.Clamp01((elapsed - delay) / duration);
                float eased = 1 - Mathf.Pow(1 - t, 3);
                if (overshoot)
                {
                    float shifted = t - 1;
                    eased = 1 + 2.2f * shifted * shifted * shifted + 1.2f * shifted * shifted;
                }
                group.alpha = Mathf.Clamp01(t * 3);
                rect.anchoredPosition = home + offset * (1 - eased);
                rect.localScale = Vector3.Scale(homeScale,
                    new Vector3(Mathf.LerpUnclamped(scale.x, 1, eased),
                        Mathf.LerpUnclamped(scale.y, 1, eased), 1));
                if (brush)
                {
                    brush.SetProgress(DojoMotion.Travel(t));
                }
            }
        }

        public Track[] tracks;
        public CanvasGroup inputGate;
        public Text scoreValue;
        public Text navigationHelp;
        public RectTransform seal;
        public ResultsSoundPlayer sounds;
        public bool IsRevealing { get; private set; }
        public float Elapsed { get; private set; }
        private int score;
        private bool failure;
        private int startedFrame;
        private bool hasResult;
        private int cue;
        private static readonly float[] CueTimes = { .20f, .46f, .60f, .78f, .90f, 1.04f, 1.18f, 1.32f, 1.45f, StampTime };

        public void Validate()
        {
            if (!inputGate || !scoreValue || !navigationHelp)
            {
                throw new InvalidOperationException("Results presentation controls missing.");
            }
            if (!seal || !sounds || tracks == null)
            {
                throw new InvalidOperationException("Results presentation composition missing.");
            }
            foreach (var track in tracks)
            {
                if (!track.rect || !track.group || track.duration <= 0)
                {
                    throw new InvalidOperationException("Results presentation track missing.");
                }
            }
            sounds.Validate();
        }

        public void Begin(int value, bool failed, bool available)
        {
            Validate();
            sounds.StopAll();
            score = value;
            failure = failed;
            hasResult = available;
            Elapsed = 0;
            cue = 0;
            startedFrame = Time.frameCount;
            IsRevealing = available;
            inputGate.interactable = false;
            inputGate.blocksRaycasts = false;
            if (!available)
            {
                Finish();
                return;
            }
            navigationHelp.text = "Enter / Space 결과 펼치기   ·   Esc 돌아가기";
            Apply();
            sounds.Begin();
        }

        private void Update()
        {
            if (!IsRevealing || Time.frameCount == startedFrame)
            {
                return;
            }
            if (!RhythmDojo.Input.ApplicationFocusState.IsFocused)
            {
                return;
            }
            // Scene activation and first glyph uploads can cost a long frame. Keep
            // that loading hitch from consuming the brush reveal before it is seen.
            Elapsed = Mathf.Min(Duration, Elapsed + Mathf.Min(Time.unscaledDeltaTime, .05f));
            Apply();
            // Consume overdue cues without a burst after a hitch. Audio follows the
            // currently visible gesture, never a separate coroutine or DSP clock.
            int latest = -1;
            while (cue < CueTimes.Length && Elapsed >= CueTimes[cue])
            {
                latest = cue++;
            }
            if (latest >= 0 && Elapsed - CueTimes[latest] < .12f)
            {
                PlayCue(latest);
            }
            if (Elapsed >= Duration)
            {
                Finish();
            }
        }

        private void PlayCue(int index)
        {
            if (index < 2)
            {
                sounds.Brush();
                return;
            }
            if (index == CueTimes.Length - 1)
            {
                sounds.Stamp(failure);
                return;
            }
            if (index >= 4 && index <= 6)
            {
                sounds.Statistic();
                return;
            }
            sounds.Count();
        }

        private void Apply()
        {
            foreach (var track in tracks)
            {
                track.Apply(Elapsed);
            }
            if (!hasResult)
            {
                return;
            }
            float t = Mathf.Clamp01((Elapsed - CountStart) / (StampTime - CountStart));
            int displayed = Mathf.RoundToInt(score * (1 - Mathf.Pow(1 - t, 3)));
            if (t >= 1)
            {
                displayed = score;
            }
            scoreValue.text = displayed.ToString("N0", CultureInfo.CurrentCulture);
            float stamp = Mathf.Clamp01((Elapsed - StampTime) / .24f);
            scoreValue.rectTransform.localScale *= 1 + .035f * Mathf.Sin(stamp * Mathf.PI);
        }

        public void Finish()
        {
            sounds.StopAll();
            Elapsed = Duration;
            Apply();
            IsRevealing = false;
            navigationHelp.text = "↑↓ / ←→ 이동   ·   Enter / Space 선택   ·   Esc 돌아가기";
        }

        private void OnDisable()
        {
            if (sounds)
            {
                sounds.StopAll();
            }
            IsRevealing = false;
        }
    }
}
