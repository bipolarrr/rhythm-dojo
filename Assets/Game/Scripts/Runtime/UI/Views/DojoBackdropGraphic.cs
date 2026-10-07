using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Architectural light strips and a perspective training floor, drawn at any resolution.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DojoBackdropGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var rect = rectTransform.rect;
            var structure = new Color(.27f, .32f, .35f, .27f);
            var light = new Color(.41f, .78f, .76f, .52f);
            var accent = new Color(.94f, .22f, .24f, .65f);
            Line(helper, rect, new Vector2(.12f,.22f), new Vector2(.12f,.80f), 2, structure);
            Line(helper, rect, new Vector2(.88f,.22f), new Vector2(.88f,.80f), 2, structure);
            Line(helper, rect, new Vector2(.12f,.80f), new Vector2(.88f,.80f), 2, structure);
            Line(helper, rect, new Vector2(.17f,.27f), new Vector2(.17f,.75f), 3, light);
            Line(helper, rect, new Vector2(.83f,.27f), new Vector2(.83f,.75f), 3, light);
            Line(helper, rect, new Vector2(.32f,.75f), new Vector2(.68f,.75f), 3, accent);
            Line(helper, rect, new Vector2(.12f,.22f), new Vector2(.88f,.22f), 1, structure);
            for (int i=0; i<9; i++)
                Line(helper, rect, new Vector2(.12f+i*.095f,.22f), new Vector2(-.25f+i*.1875f,0), 1, structure);
            foreach (float y in new[] { .04f, .10f, .16f })
                Line(helper, rect, new Vector2(0,y), new Vector2(1,y), 1, structure);
        }
        private static void Line(VertexHelper helper, Rect rect, Vector2 start, Vector2 end, float width, Color tint)
        {
            start = rect.min + Vector2.Scale(start, rect.size);
            end = rect.min + Vector2.Scale(end, rect.size);
            var direction = (end-start).normalized;
            var normal = new Vector2(-direction.y,direction.x) * width*.5f;
            int index=helper.currentVertCount;
            helper.AddVert(start-normal,tint,Vector2.zero);
            helper.AddVert(start+normal,tint,Vector2.zero);
            helper.AddVert(end+normal,tint,Vector2.zero);
            helper.AddVert(end-normal,tint,Vector2.zero);
            helper.AddTriangle(index,index+1,index+2);
            helper.AddTriangle(index,index+2,index+3);
        }
    }
}
