using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class SpeedTestLaneGraphic : Image
    {
        public const float FarWidth = 88;
        public const float NearWidth = 284;

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var bounds = GetPixelAdjustedRect();
            float center = bounds.center.x;
            float far = FarWidth * .5f;
            float near = NearWidth * .5f;
            var rail = new Color(.49f, .36f, .23f);

            Quad(vertices, center - near, center + near, center - far, center + far, bounds, rail);
            Quad(vertices, center - near + 3, center + near - 3, center - far + 2, center + far - 2, bounds, color);
        }

        private static void Quad(
            VertexHelper vertices,
            float bottomLeft,
            float bottomRight,
            float topLeft,
            float topRight,
            Rect bounds,
            Color color
        )
        {
            int start = vertices.currentVertCount;
            var uv = new Vector2(.5f, .5f);
            vertices.AddVert(new Vector3(bottomLeft, bounds.yMin), color, uv);
            vertices.AddVert(new Vector3(topLeft, bounds.yMax), color, uv);
            vertices.AddVert(new Vector3(topRight, bounds.yMax), color, uv);
            vertices.AddVert(new Vector3(bottomRight, bounds.yMin), color, uv);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }
    }
}
