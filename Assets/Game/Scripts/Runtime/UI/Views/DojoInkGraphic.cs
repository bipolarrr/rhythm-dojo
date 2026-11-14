using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Authored vector accents: tapered dry strokes or a feathered pool of local contrast.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DojoInkGraphic : MaskableGraphic
    {
        public bool wash;
        public bool contrastStroke;

        // Optional authored binding for labels whose contents change with selection.
        public Text fittedText;
        public Vector2 textPadding = new Vector2(8, 4);
        private TextGenerator generator;
        private string fittedValue;
        private TextGenerationSettings fittedSettings;
        private bool hasFit;

        private void LateUpdate()
        {
            if (!UnityEngine.Application.isPlaying || !fittedText)
            {
                return;
            }

            var settings = fittedText.GetGenerationSettings(fittedText.rectTransform.rect.size);
            if (hasFit && fittedValue == fittedText.text && fittedSettings.Equals(settings))
            {
                return;
            }

            if (generator == null)
            {
                generator = new TextGenerator();
            }
            fittedValue = fittedText.text;
            fittedSettings = settings;
            hasFit = true;
            FitInk(rectTransform, fittedText, -1, textPadding.x, textPadding.y, generator);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            hasFit = false;
        }

        protected override void OnDestroy()
        {
            ((System.IDisposable)generator)?.Dispose();
            base.OnDestroy();
        }

        public static void FitInk(RectTransform ink, Text text, int line, float horizontal, float vertical,
            TextGenerator generator = null)
        {
            // Measure rendered glyph quads, not the much wider layout rectangle.
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            if (generator == null)
            {
                generator = new TextGenerator();
            }
            generator.Populate(text.text, settings);
            var vertices = generator.verts;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            float splitY = float.MinValue;
            if (line >= 0 && generator.lineCount > 1 && text.text.Contains("\n"))
            {
                var lines = generator.lines;
                splitY = (lines[1].topY + lines[0].topY - lines[0].height) * .5f
                    / text.pixelsPerUnit;
            }
            // Unity may omit whitespace quads. Never index geometry by string character index.
            for (int vertex = 0; vertex + 3 < vertices.Count; vertex += 4)
            {
                Vector2 low = vertices[vertex].position / text.pixelsPerUnit;
                Vector2 high = low;
                for (int corner = 1; corner < 4; corner++)
                {
                    Vector2 point = vertices[vertex + corner].position / text.pixelsPerUnit;
                    low = Vector2.Min(low, point);
                    high = Vector2.Max(high, point);
                }
                if (high.x <= low.x || high.y <= low.y)
                {
                    continue;
                }
                if (splitY != float.MinValue)
                {
                    float center = (low.y + high.y) * .5f;
                    if (line == 0 && center < splitY)
                    {
                        continue;
                    }
                    if (line == 1 && center >= splitY)
                    {
                        continue;
                    }
                }
                min = Vector2.Min(min, low);
                max = Vector2.Max(max, high);
            }
            if (min.x == float.MaxValue)
            {
                ink.sizeDelta = Vector2.zero;
                return;
            }
            ink.SetParent(text.transform, false);
            ink.localRotation = Quaternion.identity;
            ink.localScale = Vector3.one;
            ink.anchorMin = ink.anchorMax = text.rectTransform.pivot;
            ink.pivot = new Vector2(.5f, .5f);
            ink.anchoredPosition = (min + max) * .5f;
            // Reserve the outer bristle/fade region in addition to readable padding.
            var coverage = max - min + new Vector2(horizontal * 2, vertical * 2);
            ink.sizeDelta = new Vector2(coverage.x / .76f, coverage.y / .64f);
            // Return to the motion group, retaining the text's rotation and measured placement.
            ink.SetParent(text.transform.parent, true);
            ink.SetAsFirstSibling();
        }

        private void PopulateContrast(VertexHelper vh, Rect rect)
        {
            // Parallel bristle tracks have independent contact and lift-off points.
            // Their overlap forms the pigment body without a shared polygon silhouette.
            const int tracks = 38;
            const int steps = 48;
            for (int track = 0; track < tracks; track++)
            {
                float across = track / (float)(tracks - 1);
                float edge = Mathf.Abs(across * 2f - 1f);
                float start = .015f + .045f * Mathf.PerlinNoise(track * .71f, 3.2f);
                float end = .94f + .055f * Mathf.PerlinNoise(track * .93f, 8.7f);
                int first = vh.currentVertCount;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float x = Mathf.Lerp(start, end, t);
                    float pressure = Mathf.SmoothStep(0f, 1f, t / .08f)
                        * Mathf.SmoothStep(0f, 1f, (1f - t) / .15f);
                    float bend = Mathf.Sin(x * 4.8f + .4f) * .025f;
                    float spread = .86f + .10f * Mathf.Sin(x * 5.1f);
                    float center = rect.center.y + rect.height
                        * ((across - .5f) * spread + bend);
                    float grain = Mathf.PerlinNoise(x * 37f, track * .83f);
                    float width = rect.height / tracks * .68f;
                    var tint = color;
                    tint.a *= pressure;
                    if (edge > .72f)
                    {
                        width *= .35f + grain;
                        tint.a *= Mathf.Clamp01((1f - edge) * 4f) * grain;
                    }
                    vh.AddVert(new Vector2(rect.xMin + x * rect.width, center - width),
                        tint, Vector2.zero);
                    vh.AddVert(new Vector2(rect.xMin + x * rect.width, center + width),
                        tint, Vector2.zero);
                    if (i > 0)
                    {
                        int current = first + i * 2;
                        vh.AddTriangle(current - 2, current - 1, current);
                        vh.AddTriangle(current - 1, current + 1, current);
                    }
                }
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (contrastStroke)
            {
                PopulateContrast(vh, rect);
                return;
            }
            if (wash)
            {
                const int segments = 48;
                vh.AddVert(rect.center, color, Vector2.zero);
                for (int ring = 1; ring <= 5; ring++)
                {
                    float radius = ring / 5f;
                    var tint = color;
                    tint.a *= 1f - Mathf.SmoothStep(0, 1, radius);
                    for (int i = 0; i <= segments; i++)
                    {
                        float angle = i * Mathf.PI * 2f / segments;
                        var point = new Vector2(Mathf.Cos(angle) * rect.width, Mathf.Sin(angle) * rect.height);
                        vh.AddVert(rect.center + point * (.5f * radius), tint, Vector2.zero);
                        if (i == segments)
                        {
                            continue;
                        }
                        int current = 1 + (ring - 1) * (segments + 1) + i;
                        if (ring == 1)
                        {
                            vh.AddTriangle(0, current, current + 1);
                            continue;
                        }
                        int inner = current - segments - 1;
                        vh.AddTriangle(inner, current, current + 1);
                        vh.AddTriangle(inner, current + 1, inner + 1);
                    }
                }
                return;
            }

            DojoMotion.Stroke(vh, new Vector2(rect.xMin, rect.center.y - rect.height * .1f),
                new Vector2(rect.xMax, rect.center.y + rect.height * .1f), rect.height, color, 3);
        }
    }
}
