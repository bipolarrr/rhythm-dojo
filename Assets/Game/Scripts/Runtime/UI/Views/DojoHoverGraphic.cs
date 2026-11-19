using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Authored mesh modifier, driven by the shared pointer focus. RectTransforms and raycasts never move.
    [DisallowMultipleComponent]
    public sealed class DojoHoverGraphic : BaseMeshEffect
    {
        public Selectable control;
        private static readonly List<DojoHoverGraphic> active = new List<DojoHoverGraphic>();
        private Bounds bounds;
        private bool hasBounds;
        private float scale = 1;
        private Vector3 center;

        public bool IsCaption => graphic is Text;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active.Clear();
        }

        public static void Collect(Selectable control, List<DojoHoverGraphic> graphics)
        {
            graphics.Clear();
            foreach (var item in active)
            {
                if (item && item.control == control)
                {
                    graphics.Add(item);
                }
            }
        }

        public bool TryGetBounds(out Bounds result)
        {
            result = default;
            if (!IsActive() || !control || !hasBounds)
            {
                return false;
            }
            if (graphic.canvasRenderer.GetInheritedAlpha() < .01f)
            {
                return false;
            }
            if (!graphic.isActiveAndEnabled || graphic.color.a < .01f)
            {
                return false;
            }
            if (bounds.size.x <= 0 || bounds.size.y <= 0)
            {
                return false;
            }

            var matrix = control.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            result = new Bounds(matrix.MultiplyPoint3x4(bounds.min), Vector3.zero);
            result.Encapsulate(matrix.MultiplyPoint3x4(bounds.max));
            result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.max.y)));
            result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.min.y)));
            return true;
        }

        public void Present(float scale, Vector3 center)
        {
            if (Mathf.Approximately(this.scale, scale) && this.center == center)
            {
                return;
            }
            this.scale = scale;
            this.center = center;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            hasBounds = false;
            if (!IsActive() || !control)
            {
                return;
            }

            var vertex = new UIVertex();
            var toControl = control.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            var fromControl = toControl.inverse;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                if (vertex.color.a > 0)
                {
                    if (!hasBounds)
                    {
                        bounds = new Bounds(vertex.position, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(vertex.position);
                    }
                }
                var point = toControl.MultiplyPoint3x4(vertex.position);
                vertex.position = fromControl.MultiplyPoint3x4(center + (point - center) * scale);
                vh.SetUIVertex(vertex, i);
            }
        }

        protected override void OnEnable()
        {
            scale = 1;
            hasBounds = false;
            base.OnEnable();
            active.Add(this);
        }

        protected override void OnDisable()
        {
            active.Remove(this);
            scale = 1;
            hasBounds = false;
            base.OnDisable();
        }
    }
}
