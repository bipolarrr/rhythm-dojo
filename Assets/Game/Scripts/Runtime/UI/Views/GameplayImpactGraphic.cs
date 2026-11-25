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

        [SerializeField] private Texture2D bristles;

        public override Texture mainTexture => bristles;
        public bool HasArtwork => bristles;

        private double struckAt;
        private float elapsed;
        private float strength;
        private bool sustained;
        private bool missed;
        private Judgment grade;
        private const float Duration = .21f;

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
            struckAt = Time.unscaledTimeAsDouble;
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
            elapsed = (float)(Time.unscaledTimeAsDouble - struckAt);

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

            float radius = rectTransform.rect.width * .42f;
            var contact = new Vector2(0, -2);
            if (sustained)
            {
                var tint = color;
                tint.a *= .85f;
                Sweep(helper, new Vector2(-radius * .74f, -4), new Vector2(radius * .74f, -4),
                    radius * .18f, tint);
                return;
            }

            float progress = Mathf.Clamp01(elapsed / Duration);
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.035f, Duration, elapsed));
            var pigment = color;
            pigment.a *= fade * strength;
            float direction = 1;
            if ((Lane + Variation) % 2 != 0)
            {
                direction = -1;
            }
            if (missed)
            {
                var drop = contact + Vector2.down * radius * (.15f + .43f * progress);
                Sweep(helper, drop + Vector2.left * radius * .44f, drop, radius * .23f, pigment);
                Sweep(helper, drop + Vector2.right * radius * .44f,
                    drop + Vector2.down * radius * .18f, radius * .15f, pigment);
                return;
            }

            float attack = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / .084f), 3);
            radius *= Mathf.Lerp(.55f, 1, strength);
            PaintImpact(helper, attack, radius, direction);
            float reach = radius * (.13f + .87f * attack);
            if (grade == Judgment.Good)
            {
                reach *= .79f;
            }

            // Two descending cuts split from the same point. Late bristles peel away
            // into the spent-note area while the next note approaches from above.
            var lead = new Vector2(direction * reach * .91f, -reach * (1.22f + .16f * Variation));
            var counter = new Vector2(-direction * reach * .83f, -reach * .63f);
            var ink = new Color(.10f, .14f, .17f, pigment.a);
            Sweep(helper, contact, contact + lead * .98f, radius * .43f, ink);
            Sweep(helper, contact, contact + lead, radius * (.18f + .17f * fade), pigment);
            Sweep(helper, contact, contact + counter, radius * .21f, pigment);

            var core = new Color(1, .97f, .84f, strength * (1 - Mathf.Clamp01(elapsed / .083f)));
            var span = Vector2.right * radius * (.28f + .44f * attack);
            Sweep(helper, contact - span, contact + span, radius * .27f * (1 - progress), core);

            float flight = Mathf.Clamp01((elapsed - .024f) / .19f);
            int fragments = 3 + Variation;
            for (int i = 0; i < fragments; i++)
            {
                float angle = (.18f + .66f * (i + .5f) / fragments) * Mathf.PI;
                var ray = new Vector2(direction * Mathf.Cos(angle), -Mathf.Sin(angle));
                float distance = radius * (.16f + flight * (1 + .12f * (i % 2)));
                var tip = contact + ray * distance;
                tip.y -= radius * flight * .55f;
                var fleck = pigment;
                fleck.a *= (1 - flight) * attack;
                Sweep(helper, tip - ray * radius * (.11f + .18f * attack), tip,
                    radius * (.09f + .025f * (i % 2)), fleck);
            }
        }

        private void PaintImpact(VertexHelper helper, float attack, float radius, float direction)
        {
            float opacity = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.054f, .155f, elapsed));
            if (opacity <= 0)
            {
                return;
            }

            float width = radius * (.55f + .58f * attack);
            float height = radius * (.22f + 1.24f * attack);
            if (grade == Judgment.Good)
            {
                width *= .80f;
                height *= .72f;
            }

            // The lower three quarters of the atlas hold the impact fan. Its top
            // edge is pinned to the receptor as the pigment opens downwards.
            float x = direction * width;
            var tint = Color.Lerp(Color.white, color, .25f);
            tint.a = opacity * strength;
            int index = helper.currentVertCount;
            helper.AddVert(new Vector2(-x, -height), tint, Vector2.zero);
            helper.AddVert(new Vector2(-x, 0), tint, new Vector2(0, .75f));
            helper.AddVert(new Vector2(x, 0), tint, new Vector2(1, .75f));
            helper.AddVert(new Vector2(x, -height), tint, Vector2.right);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }

        private static void Sweep(VertexHelper helper, Vector2 start, Vector2 end, float width, Color tint)
        {
            Vector2 along = (end - start).normalized;
            var side = new Vector2(-along.y, along.x) * (width * .5f);
            // Let the uppermost bristle graze the line at contact.
            float overhang = Mathf.Max(start.y, end.y) + Mathf.Abs(side.y);
            if (overhang > 0)
            {
                start.y -= overhang;
                end.y -= overhang;
            }

            int index = helper.currentVertCount;
            helper.AddVert(start - side, tint, new Vector2(0, .75f));
            helper.AddVert(start + side, tint, Vector2.up);
            helper.AddVert(end + side, tint, Vector2.one);
            helper.AddVert(end - side, tint, new Vector2(1, .75f));
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }
    }
}
