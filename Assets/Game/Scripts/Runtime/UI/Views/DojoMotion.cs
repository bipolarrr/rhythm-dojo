using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Presentation-only curves. Never used by the input or music clocks.
    public static class DojoMotion
    {
        public static float Travel(float progress)
        {
            float t = Mathf.Clamp01(progress);
            if (t < .24f)
            {
                return .32f * t * t / (.24f * .24f);
            }

            float remaining = (1f - t) / .76f;
            return 1f - .68f * remaining * remaining * remaining;
        }

        public static float Push(float progress)
        {
            float t = Mathf.Clamp01(progress);
            // Slow approach, fast passage, then a brief landing at the exact framing.
            return t * t * t * (4f - 3f * t);
        }

        public static void Stroke(VertexHelper helper, Vector2 start, Vector2 end,
            float width, Color tint, int seed)
        {
            Vector2 along = end - start;
            Vector2 normal = new Vector2(-along.y, along.x).normalized;
            const int tracks = 5;
            const int steps = 18;
            for (int track = 0; track < tracks; track++)
            {
                for (int step = 0; step < steps; step++)
                {
                    // Dry bristles lift independently, leaving paper visible through the tip.
                    if (step > 9 && (step + track * 3 + seed) % 7 < 2)
                    {
                        continue;
                    }

                    float t = step / (float)steps;
                    float next = (step + 1f) / steps;
                    float pressure = Mathf.Sin((.12f + t * .88f) * Mathf.PI);
                    float nextPressure = Mathf.Sin((.12f + next * .88f) * Mathf.PI);
                    float grain = .72f + .28f * Mathf.Sin(step * 2.3f + track * 4.7f + seed);
                    float offset = (track - 2) * width * .19f;
                    float thickness = width * .12f * grain;
                    Vector2 a = start + along * t + normal * (offset * (.6f + t * .7f));
                    Vector2 b = start + along * next + normal * (offset * (.6f + next * .7f));
                    int index = helper.currentVertCount;
                    helper.AddVert(Pixel(a - normal * thickness * pressure), tint, Vector2.zero);
                    helper.AddVert(Pixel(a + normal * thickness * pressure), tint, Vector2.zero);
                    helper.AddVert(Pixel(b + normal * thickness * nextPressure), tint, Vector2.zero);
                    helper.AddVert(Pixel(b - normal * thickness * nextPressure), tint, Vector2.zero);
                    helper.AddTriangle(index, index + 1, index + 2);
                    helper.AddTriangle(index, index + 2, index + 3);
                }
            }
        }

        private static Vector2 Pixel(Vector2 point)
        {
            return new Vector2(Mathf.Round(point.x), Mathf.Round(point.y));
        }
    }
}
