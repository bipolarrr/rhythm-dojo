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
            color = tint; strength = intensity; missed = miss; sustained = false; elapsed = 0;
            gameObject.SetActive(true); SetVerticesDirty();
        }
        public void Sustain(Color tint)
        {
            elapsed = 0; color = tint; strength = .3f; sustained = true; missed = false;
            gameObject.SetActive(true); SetVerticesDirty();
        }
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (!sustained && elapsed >= Duration) { gameObject.SetActive(false); return; }
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            float progress = Mathf.Clamp01(elapsed / Duration);
            float fade = sustained ? .6f + .12f * Mathf.Sin(elapsed * 12) : (1 - progress) * (1 - progress);
            float radius = Mathf.Min(rectTransform.rect.width * .42f, 48);
            float expanded = sustained ? radius * .75f : Mathf.Lerp(radius * .28f, radius, 1 - (1-progress)*(1-progress));
            var tint = color; tint.a = fade * strength;
            Ring(helper, expanded, 2.3f, tint);
            var glow = tint; glow.a *= .24f;
            Ring(helper, expanded, 8, glow);
            if (sustained) { Quad(helper, -radius*.55f, -3, radius*.55f, 3, glow); return; }
            if (missed) return;
            var core = new Color(.96f, .98f, .90f, fade * strength * .85f);
            Quad(helper, -radius*.7f, -2.2f, radius*.7f, 2.2f, core);
            Quad(helper, -2, 0, 2, Mathf.Lerp(16,60,progress), glow);
            for (int i = 0; i < 6; i++)
            {
                float direction = (i - 2.5f) / 2.5f;
                float x = direction * radius * (.15f + progress * .75f);
                float y = (1 - Mathf.Abs(direction) * .55f) * (8 + progress * 50);
                Quad(helper, x-1.3f, y-2.5f, x+1.3f, y+2.5f, tint);
            }
        }
        private static void Quad(VertexHelper helper,float x0,float y0,float x1,float y1,Color tint)
        {
            int index = helper.currentVertCount;
            helper.AddVert(new Vector2(x0,y0),tint,Vector2.zero); helper.AddVert(new Vector2(x0,y1),tint,Vector2.zero);
            helper.AddVert(new Vector2(x1,y1),tint,Vector2.zero); helper.AddVert(new Vector2(x1,y0),tint,Vector2.zero);
            helper.AddTriangle(index,index+1,index+2); helper.AddTriangle(index,index+2,index+3);
        }
        private static void Ring(VertexHelper helper,float radius,float width,Color tint)
        {
            const int segments = 48;
            for (int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                var start=new Vector2(Mathf.Cos(a),Mathf.Sin(a)*.32f);
                var end=new Vector2(Mathf.Cos(b),Mathf.Sin(b)*.32f);
                int index=helper.currentVertCount;
                helper.AddVert(start*Mathf.Max(0,radius-width*.5f),tint,Vector2.zero);
                helper.AddVert(end*Mathf.Max(0,radius-width*.5f),tint,Vector2.zero);
                helper.AddVert(end*(radius+width*.5f),tint,Vector2.zero);
                helper.AddVert(start*(radius+width*.5f),tint,Vector2.zero);
                helper.AddTriangle(index,index+1,index+2);helper.AddTriangle(index,index+2,index+3);
            }
        }
    }
}
