using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkspaceToolIcon : MaskableGraphic
    {
        public enum IconKind
        {
            Select,
            Pencil,
            Eraser,
            Grab
        }

        public IconKind kind;

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            switch (kind)
            {
                case IconKind.Select:
                    Polygon(helper, new Vector2(.2f, .9f), new Vector2(.2f, .25f), new Vector2(.7f, .48f));
                    Line(helper, new Vector2(.43f, .5f), new Vector2(.67f, .12f), .12f);
                    break;
                case IconKind.Pencil:
                    Polygon(helper, new Vector2(.19f, .28f), new Vector2(.35f, .12f),
                        new Vector2(.87f, .64f), new Vector2(.71f, .8f));
                    Polygon(helper, new Vector2(.1f, .05f), new Vector2(.16f, .23f), new Vector2(.3f, .09f));
                    Line(helper, new Vector2(.76f, .85f), new Vector2(.92f, .69f), .1f);
                    break;
                case IconKind.Eraser:
                    Polygon(helper, new Vector2(.12f, .35f), new Vector2(.48f, .8f),
                        new Vector2(.87f, .49f), new Vector2(.51f, .04f));
                    Line(helper, new Vector2(.14f, .03f), new Vector2(.89f, .03f), .06f);
                    break;
                case IconKind.Grab:
                    Line(helper, new Vector2(.27f, .12f), new Vector2(.15f, .45f), .1f);
                    Line(helper, new Vector2(.15f, .45f), new Vector2(.3f, .35f), .1f);
                    Line(helper, new Vector2(.3f, .35f), new Vector2(.3f, .81f), .1f);
                    Line(helper, new Vector2(.45f, .46f), new Vector2(.45f, .94f), .1f);
                    Line(helper, new Vector2(.6f, .46f), new Vector2(.6f, .87f), .1f);
                    Line(helper, new Vector2(.75f, .12f), new Vector2(.75f, .72f), .1f);
                    Polygon(helper, new Vector2(.27f, .12f), new Vector2(.3f, .49f),
                        new Vector2(.75f, .49f), new Vector2(.75f, .12f));
                    break;
            }
        }

        private void Line(VertexHelper helper, Vector2 a, Vector2 b, float thickness)
        {
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized * thickness * .5f;
            Polygon(helper, a - normal, a + normal, b + normal, b - normal);
        }

        private void Polygon(VertexHelper helper, params Vector2[] points)
        {
            int start = helper.currentVertCount;
            var rect = rectTransform.rect;
            foreach (var point in points)
            {
                helper.AddVert(new Vector2(rect.xMin + point.x * rect.width,
                    rect.yMin + point.y * rect.height), color, Vector2.zero);
            }
            for (int i = 1; i < points.Length - 1; i++)
            {
                helper.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
