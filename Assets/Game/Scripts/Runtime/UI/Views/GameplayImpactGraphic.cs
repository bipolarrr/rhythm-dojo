using RhythmDojo.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameplayImpactGraphic : MaskableGraphic
    {
        public int Lane { get; set; }
        public int Variation { get; set; }

        private float elapsed;
        private float strength;
        private bool sustained;
        private bool missed;
        private Judgment grade;
        private const float Duration = .24f;

        public void Play(Color tint, float intensity, bool miss = false)
        {
            var result = Judgment.Perfect;
            if (miss)
            {
                result = Judgment.Miss;
            }
            Play(tint, intensity, result);
        }

        public void Play(Color tint, float intensity, Judgment grade)
        {
            color = tint;
            strength = intensity;
            this.grade = grade;
            missed = grade == Judgment.Miss;
            sustained = false;
            elapsed = 0;
            gameObject.SetActive(true);
            SetVerticesDirty();
        }

        public void Sustain(Color tint)
        {
            elapsed = 0;
            color = tint;
            strength = .3f;
            sustained = true;
            missed = false;
            gameObject.SetActive(true);
            SetVerticesDirty();
        }

        private void Update()
        {
            if (sustained)
            {
                return;
            }
            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= Duration)
            {
                gameObject.SetActive(false);

                return;
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();

            float progress = Mathf.Clamp01(elapsed / Duration);
            float radius = Mathf.Min(rectTransform.rect.width * .42f, 46);
            var tint = color;
            if (sustained)
            {
                // A quiet, steady contact mark; no continuous expanding rings.
                tint.a *= strength * .65f;
                DojoMotion.Stroke(helper, new Vector2(-radius * .65f, -5),
                    new Vector2(radius * .65f, -3), 5, tint, Lane);
                return;
            }

            float attack = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / .055f), 3);
            float fade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.065f, Duration, elapsed));
            tint.a *= fade * strength;
            float direction = 1;
            if ((Lane + Variation) % 2 != 0)
            {
                direction = -1;
            }
            if (missed)
            {
                // Failed contact sinks below the rail instead of celebrating with a burst.
                DojoMotion.Stroke(helper, new Vector2(-radius * .5f, -8 - progress * 10),
                    new Vector2(-radius * .08f, -10 - progress * 10), 6, tint, Lane);
                DojoMotion.Stroke(helper, new Vector2(radius * .10f, -10 - progress * 12),
                    new Vector2(radius * .36f, -13 - progress * 12), 4, tint, Lane + 2);
                return;
            }

            float width = 17;
            int fragments = 5;
            if (grade == Judgment.Good)
            {
                radius *= .78f;
                width = 11;
                fragments = 3;
            }

            // Dry pigment under the contact, then a shorter ivory edge. All geometry stays
            // below the judgment rail and inside one lane even during six-note chords.
            var start = new Vector2(-direction * radius * .72f, -9);
            var end = new Vector2(direction * radius * .88f * attack, -15 - Variation * 3);
            var ink = new Color(.035f, .065f, .065f, fade * strength * .85f);
            DojoMotion.Stroke(helper, start + Vector2.down * 4,
                end + Vector2.down * (4 + progress * 7), width * 1.55f, ink, Lane + Variation);
            DojoMotion.Stroke(helper, start, end, width * (.65f + .35f * fade), tint, Lane + Variation);
            var core = new Color(.98f, .91f, .72f, fade * strength * .85f);
            DojoMotion.Stroke(helper, start * .6f + Vector2.down * 3,
                new Vector2(direction * radius * .65f * attack, -10), 5, core, Lane + 2);
            if (grade == Judgment.Perfect)
            {
                var echo = tint;
                echo.a *= .24f * (1 - progress);
                DojoMotion.Stroke(helper, start + Vector2.down * 13,
                    end + Vector2.down * (12 + progress * 10), 7, echo, Lane + 1);
            }

            for (int i = 0; i < fragments; i++)
            {
                float delay = .016f + i * .008f;
                float flight = Mathf.Clamp01((elapsed - delay) / .16f);
                if (flight <= 0)
                {
                    continue;
                }
                float x = direction * radius * (-.55f + i * .27f) * (.4f + flight * .6f);
                float y = -17 - flight * (10 + i * 4);
                var fragment = tint;
                fragment.a *= 1 - flight;
                PixelFragment(helper, x, y, 2 + i % 2, fragment);
            }
        }

        private static void PixelFragment(VertexHelper helper, float x, float y, float size, Color tint)
        {
            x = Mathf.Round(x);
            y = Mathf.Round(y);
            int index = helper.currentVertCount;
            helper.AddVert(new Vector2(x, y), tint, Vector2.zero);
            helper.AddVert(new Vector2(x, y + size), tint, Vector2.zero);
            helper.AddVert(new Vector2(x + size, y + size), tint, Vector2.zero);
            helper.AddVert(new Vector2(x + size, y), tint, Vector2.zero);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }
    }
}
