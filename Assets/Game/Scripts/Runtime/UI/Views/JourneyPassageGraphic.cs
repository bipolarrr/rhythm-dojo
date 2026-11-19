using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Wind carries ink-wash ribbons across adjacent projections. This is scenery
    // only, underneath the unchanged lane viewport and HUD.
    public sealed class JourneyPassageGraphic : MaskableGraphic
    {
        private float passage;

        public void SetPassage(float amount)
        {
            passage = Mathf.Clamp01(amount);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            float envelope = Mathf.Sin(passage * Mathf.PI);
            if (envelope <= .001f)
            {
                return;
            }
            var rect = rectTransform.rect;
            for (int band = 0; band < 9; band++)
            {
                float y = .04f + band * .115f;
                float center = -1.1f + passage * 3.2f + Mathf.Sin(band * 2.4f) * .18f;
                for (int segment = 0; segment < 24; segment++)
                {
                    float u = segment / 24f;
                    float x = center + u - .5f;
                    float next = x + 1f / 24;
                    float thickness = Mathf.Sin(u * Mathf.PI) * .052f;
                    float bend = Mathf.Sin(u * 5 + band) * .025f;
                    var tint = new Color(.55f, .65f, .61f, envelope * .28f * Mathf.Sin(u * Mathf.PI));
                    int index = helper.currentVertCount;
                    helper.AddVert(Point(rect, x, y + bend - thickness), tint, Vector2.zero);
                    helper.AddVert(Point(rect, x, y + bend + thickness), tint, Vector2.zero);
                    helper.AddVert(Point(rect, next, y + bend + thickness), tint, Vector2.zero);
                    helper.AddVert(Point(rect, next, y + bend - thickness), tint, Vector2.zero);
                    helper.AddTriangle(index, index + 1, index + 2);
                    helper.AddTriangle(index + 2, index + 3, index);
                }
            }
        }

        private static Vector2 Point(Rect rect, float x, float y)
        {
            return new Vector2(rect.xMin + rect.width * x, rect.yMin + rect.height * y);
        }
    }
}
