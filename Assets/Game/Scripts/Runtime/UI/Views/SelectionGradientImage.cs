using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class SelectionGradientImage : Image
    {
        public bool useGradient;
        public Color leftColor = Color.white, rightColor = Color.white;
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            base.OnPopulateMesh(vertices);
            if (!useGradient) return;
            var bounds = rectTransform.rect;
            if (bounds.width <= 0) return;
            var vertex = new UIVertex();
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                float progress = Mathf.Clamp01((vertex.position.x - bounds.xMin) / bounds.width);
                vertex.color = Color.Lerp(leftColor, rightColor, progress) * color;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
