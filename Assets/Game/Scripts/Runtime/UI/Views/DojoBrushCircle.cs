using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // The same separated dry-bristle approach as DojoInkGraphic, wrapped around the visible content.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DojoBrushCircle : MaskableGraphic
    {
        private float reveal;
        private float opacity;
        private float width;

        public float Reveal => reveal;
        public float Opacity => opacity;

        public void Present(float reveal, float opacity, float width)
        {
            if (this.reveal == reveal && this.opacity == opacity && this.width == width)
            {
                return;
            }
            this.reveal = reveal;
            this.opacity = opacity;
            this.width = width;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (reveal <= 0 || opacity <= 0)
            {
                return;
            }

            const int tracks = 9;
            const int steps = 120;
            var rect = rectTransform.rect;
            for (int track = 0; track < tracks; track++)
            {
                float across = (track - 4) / 4f;
                for (int step = 0; step < steps; step++)
                {
                    float t = step / (float)steps;
                    if (t >= reveal)
                    {
                        break;
                    }
                    // Sparse paper gaps follow each bristle; they do not flicker between frames.
                    if (Mathf.Abs(across) > .6f && (step * 7 + track * 11) % 19 < 4)
                    {
                        continue;
                    }
                    float next = Mathf.Min((step + 1f) / steps, reveal);
                    AddPair(vh, rect, t, across, track);
                    AddPair(vh, rect, next, across, track);
                    int index = vh.currentVertCount - 4;
                    vh.AddTriangle(index, index + 1, index + 2);
                    vh.AddTriangle(index + 1, index + 3, index + 2);
                }
            }
        }

        private void AddPair(VertexHelper vh, Rect rect, float t, float across, int track)
        {
            float angle = (.30f - t * 2.06f) * Mathf.PI;
            float x = Mathf.Cos(angle);
            float y = -Mathf.Sin(angle);
            var radial = new Vector2(x, y);
            float wobble = 1 + .018f * Mathf.Sin(t * 19 + .4f) + .011f * Mathf.Sin(t * 43);
            var point = rect.center + Vector2.Scale(radial, rect.size * .5f) * wobble;
            var normal = new Vector2(radial.x / rect.width, radial.y / rect.height).normalized;
            // Press the brush down at entry, then lift it continuously toward a fine dry tip.
            float pressure = Mathf.Lerp(1f, .10f, Mathf.SmoothStep(0, 1, t));
            pressure *= .94f + .06f * Mathf.Sin(t * 9 + .7f);
            pressure *= Mathf.SmoothStep(.03f, 1, (1 - t) / .14f);
            float grain = .65f + .35f * Mathf.PerlinNoise(t * 52, track * .83f);
            float thickness = width * .075f * grain * pressure;
            // Let the heavier brush spread outward so its thick head does not cover the caption.
            point += normal * ((.40f + across * .45f) * width * pressure);
            var tint = color;
            tint.a *= opacity * (.60f + .4f * grain);
            if (Mathf.Abs(across) > .7f)
            {
                tint.a *= .55f;
            }
            vh.AddVert(point - normal * thickness, tint, Vector2.zero);
            vh.AddVert(point + normal * thickness, tint, Vector2.zero);
        }
    }
}
