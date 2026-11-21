using UnityEngine;
using UnityEngine.UI;

namespace VehicleFundamentals
{
    /// <summary>Tessellates rounded Canvas surfaces with a one-pixel alpha fringe.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VehicleHudSurface : MaskableGraphic
    {
        public override Texture mainTexture => Texture2D.whiteTexture;
        public float radius = 6;
        public bool vertical;
        private float amount = 1;
        public float fillAmount
        {
            get => amount;
            set { float next = Mathf.Clamp01(value); if (next == amount) return; amount = next; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (amount <= 0) return;
            Rect area = GetPixelAdjustedRect();
            if (vertical) area.height *= amount; else area.width *= amount;
            float r = Mathf.Min(radius, Mathf.Min(area.width, area.height) * 0.5f);
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.uv0 = new Vector2(0.5f, 0.5f);
            vertex.position = area.center; helper.AddVert(vertex);
            const int cornerSegments = 12;
            const int perimeter = cornerSegments * 4;
            float fringe = 1f / Mathf.Max(0.01f, canvas != null ? canvas.scaleFactor : 1);
            for (int i = 0; i < perimeter; i++)
            {
                int corner = i / cornerSegments;
                float angle = (-90 + corner * 90 + (i % cornerSegments) * 90f / (cornerSegments - 1)) * Mathf.Deg2Rad;
                float cx = corner < 2 ? area.xMax - r : area.xMin + r;
                float cy = corner == 0 || corner == 3 ? area.yMin + r : area.yMax - r;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertex.color = color;
                vertex.position = new Vector3(cx + direction.x * r, cy + direction.y * r);
                helper.AddVert(vertex);
                Color transparent = color; transparent.a = 0; vertex.color = transparent;
                vertex.position = new Vector3(cx + direction.x * (r + fringe), cy + direction.y * (r + fringe));
                helper.AddVert(vertex);
            }
            for (int i = 0; i < perimeter; i++)
            {
                int inner = 1 + i * 2, outer = inner + 1, next = 1 + ((i + 1) % perimeter) * 2;
                helper.AddTriangle(0, inner, next);
                helper.AddTriangle(inner, outer, next + 1); helper.AddTriangle(inner, next + 1, next);
            }
        }
    }
}
