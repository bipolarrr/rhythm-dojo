using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DojoClickRippleGraphic : MaskableGraphic
    {
        private const float Duration = .38f;
        private float elapsed;

        public void Play()
        {
            elapsed = 0; gameObject.SetActive(true); SetVerticesDirty();
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= Duration) { gameObject.SetActive(false); return; }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            float progress = Mathf.Clamp01(elapsed/Duration);
            float eased = 1-(1-progress)*(1-progress);
            float radius = Mathf.Lerp(6,62,eased);
            float opacity = (1-progress)*(1-progress);
            Ring(helper,radius,2.2f,new Color(.96f,.25f,.26f,opacity*.85f));
            Ring(helper,radius+2,6,new Color(.96f,.25f,.26f,opacity*.10f));
            Ring(helper,radius*.64f,1.1f,new Color(.46f,.78f,.76f,opacity*.50f));
        }

        private static void Ring(VertexHelper helper,float radius,float width,Color tint)
        {
            const int segments = 64;
            float inner = Mathf.Max(0,radius-width*.5f), outer = radius+width*.5f;
            for (int i=0; i<segments; i++)
            {
                float a=i*Mathf.PI*2/segments, b=(i+1)*Mathf.PI*2/segments;
                var start=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                var end=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                int index=helper.currentVertCount;
                helper.AddVert(start*inner,tint,Vector2.zero); helper.AddVert(end*inner,tint,Vector2.zero);
                helper.AddVert(end*outer,tint,Vector2.zero); helper.AddVert(start*outer,tint,Vector2.zero);
                helper.AddTriangle(index,index+1,index+2); helper.AddTriangle(index,index+2,index+3);
            }
        }
    }
}
