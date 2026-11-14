using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameplayImpactGraphic : MaskableGraphic
    {
        public int Lane { get; set; }

        private float elapsed;
        private float strength;
        private bool sustained;
        private bool missed;
        private const float Duration = .24f;

        public void Play(Color tint, float intensity, bool miss = false)
        {
            color = tint;
            strength = intensity;
            missed = miss;
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
            float radius = Mathf.Min(rectTransform.rect.width * .40f, 44);
            var tint = color;
            if (sustained)
            {
                // A quiet, steady contact mark; no continuous expanding rings.
                tint.a *= strength * .65f;
                DojoMotion.Stroke(helper, new Vector2(-radius * .65f, -5),
                    new Vector2(radius * .65f, -3), 5, tint, Lane);
                return;
            }

            float attack = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / .065f), 3);
            float fade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.065f, Duration, elapsed));
            tint.a *= fade * strength;
            float direction = 1;
            if (Lane % 2 != 0)
            {
                direction = -1;
            }
            if (missed)
            {
                // Failed contact sinks below the rail instead of celebrating with a burst.
                DojoMotion.Stroke(helper, new Vector2(-radius * .45f, -6 - progress * 8),
                    new Vector2(radius * .4f, -9 - progress * 8), 4, tint, Lane);
                return;
            }

            // Confined to this lane, below incoming notes, with a split dry tip.
            DojoMotion.Stroke(helper, new Vector2(-direction * radius * .7f, -5),
                new Vector2(direction * radius * attack, -9 - 9 * attack), 11 * fade, tint, Lane);
            var core = new Color(.94f, .85f, .65f, fade * strength * .72f);
            DojoMotion.Stroke(helper, new Vector2(-direction * radius * .5f, -4),
                new Vector2(direction * radius * .7f * attack, -7), 4, core, Lane + 2);
            for (int i = 0; i < 3; i++)
            {
                float delay = .018f + i * .009f;
                float flight = Mathf.Clamp01((elapsed - delay) / .12f);
                if (flight <= 0)
                {
                    continue;
                }
                float x = direction * radius * (.2f + i * .2f) * attack;
                float y = -14 - flight * (8 + i * 5);
                DojoMotion.Stroke(helper, new Vector2(x, y),
                    new Vector2(x + direction * 4, y - 3), 3, tint, i);
            }
        }

    }
}
