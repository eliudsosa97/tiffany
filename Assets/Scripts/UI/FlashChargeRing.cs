using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace TiffanyGame
{
    public sealed class FlashChargeRing : Image
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (fillAmount <= 0f || sprite == null) return;
            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            Vector4 uv = DataUtility.GetOuterUV(sprite);
            int segments = Mathf.CeilToInt(fillAmount * 64f);
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * 0.5f - fillAmount * Mathf.PI * 2f * i / segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                for (int edge = 0; edge < 2; edge++)
                {
                    Vector2 point = direction * (edge == 0 ? 1f : 0.8f);
                    mesh.AddVert(rect.center + point * radius, color,
                        new Vector2(Mathf.Lerp(uv.x, uv.z, point.x * 0.5f + 0.5f),
                            Mathf.Lerp(uv.y, uv.w, point.y * 0.5f + 0.5f)));
                }
                if (i == 0) continue;
                int start = (i - 1) * 2;
                mesh.AddTriangle(start, start + 2, start + 1);
                mesh.AddTriangle(start + 1, start + 2, start + 3);
            }
        }
    }
}
