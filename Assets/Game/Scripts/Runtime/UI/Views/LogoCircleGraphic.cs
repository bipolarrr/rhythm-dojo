using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LogoCircleGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var rect = rectTransform.rect;
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            const int segments = 96;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                var da = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int index = helper.currentVertCount;
                var fill = new Color(.025f, .035f, .045f, color.a);
                helper.AddVert(center, fill, Vector2.zero);
                helper.AddVert(center + da * radius * .86f, fill, Vector2.zero);
                helper.AddVert(center + db * radius * .86f, fill, Vector2.zero);
                helper.AddTriangle(index, index + 1, index + 2);
                Ring(helper, center, da, db, radius * .86f, radius * .88f, color, color);
                if (i % 8 < 5)
                    Ring(helper, center, da, db, radius * .78f, radius * .784f,
                        new Color(.38f,.53f,.54f,.45f), new Color(.38f,.53f,.54f,.45f));
                var glow = color; glow.a *= .18f;
                var clear = color; clear.a = 0f;
                Ring(helper, center, da, db, radius * .88f, radius, glow, clear);
            }
        }

        private static void Ring(VertexHelper helper, Vector2 center, Vector2 a, Vector2 b,
            float inner, float outer, Color innerColor, Color outerColor)
        {
            int index = helper.currentVertCount;
            helper.AddVert(center + a * inner, innerColor, Vector2.zero);
            helper.AddVert(center + b * inner, innerColor, Vector2.zero);
            helper.AddVert(center + b * outer, outerColor, Vector2.zero);
            helper.AddVert(center + a * outer, outerColor, Vector2.zero);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }

        public bool IsRaycastLocationValid(Vector2 point, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, point, eventCamera, out var local)) return false;
            var rect = rectTransform.rect;
            return (local - rect.center).sqrMagnitude <= Mathf.Pow(Mathf.Min(rect.width, rect.height) * .44f, 2f);
        }
    }
}
