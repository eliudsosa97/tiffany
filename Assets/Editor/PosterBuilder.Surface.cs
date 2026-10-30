using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static partial class PosterBuilder
    {
        sealed class Surface
        {
            readonly Vector3[] vertices;
            readonly int[] triangles;
            readonly Vector3[] first = new Vector3[8];
            readonly Vector3[] second = new Vector3[8];

            public Surface(Mesh mesh)
            {
                vertices = mesh.vertices;
                triangles = mesh.triangles;
            }

            public bool TryAttach(Matrix4x4 matrix, PosterLayout.Anchor anchor, out Vector3 position)
            {
                position = anchor.Position;
                if (!TryContact(matrix, anchor, out var contact)) return false;
                var toPaper = Matrix4x4.TRS(contact, anchor.Rotation, Vector3.one).inverse * matrix;
                float surface = float.NegativeInfinity, center = float.NegativeInfinity;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = toPaper.MultiplyPoint3x4(vertices[triangles[i]]);
                    Vector3 b = toPaper.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                    Vector3 c = toPaper.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                    a.z = -a.z; b.z = -b.z; c.z = -c.z;
                    float denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                    if (Mathf.Abs(denominator) > 0.000001f)
                    {
                        float u = ((b.y - c.y) * -c.x + (c.x - b.x) * -c.y) / denominator;
                        float v = ((c.y - a.y) * -c.x + (a.x - c.x) * -c.y) / denominator;
                        if (u >= -0.00001f && v >= -0.00001f && u + v <= 1.00001f)
                            center = Mathf.Max(center, u * a.z + v * b.z + (1f - u - v) * c.z);
                    }
                    first[0] = a; first[1] = b; first[2] = c;
                    int count = Clip(first, 3, second, 0, -PosterLayout.Width * 0.5f, 1f);
                    count = Clip(second, count, first, 0, PosterLayout.Width * 0.5f, -1f);
                    count = Clip(first, count, second, 1, -PosterLayout.Height * 0.5f, 1f);
                    count = Clip(second, count, first, 1, PosterLayout.Height * 0.5f, -1f);
                    for (int p = 0; p < count; p++) surface = Mathf.Max(surface, first[p].z);
                }
                if (float.IsNegativeInfinity(center) || float.IsNegativeInfinity(surface) || surface - center > 0.025f) return false;
                position = contact + anchor.Rotation * Vector3.back * (surface + 0.005f);
                return true;
            }

            bool TryContact(Matrix4x4 matrix, PosterLayout.Anchor anchor, out Vector3 contact)
            {
                float nearest = float.NegativeInfinity;
                contact = Vector3.zero;
                int hits = 0;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 previous = matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                    for (int j = 0; j < 3; j++)
                    {
                        Vector3 current = matrix.MultiplyPoint3x4(vertices[triangles[i + j]]);
                        float height = anchor.Position.y;
                        if ((previous.y <= height && current.y > height) || (current.y <= height && previous.y > height))
                        {
                            Vector3 point = Vector3.LerpUnclamped(previous, current, (height - previous.y) / (current.y - previous.y));
                            float distance = Vector3.Dot(point - anchor.Position, anchor.Facing);
                            if (distance > nearest + 0.0001f)
                            {
                                nearest = distance;
                                contact = point;
                                hits = 1;
                            }
                            else if (Mathf.Abs(distance - nearest) <= 0.0001f)
                            {
                                contact += point;
                                hits++;
                            }
                        }
                        previous = current;
                    }
                }
                if (hits == 0) return false;
                contact /= hits;
                return true;
            }

            static int Clip(Vector3[] input, int count, Vector3[] output, int axis, float edge, float sign)
            {
                if (count == 0) return 0;
                int written = 0;
                Vector3 previous = input[count - 1];
                float before = (previous[axis] - edge) * sign;
                for (int i = 0; i < count; i++)
                {
                    Vector3 current = input[i];
                    float after = (current[axis] - edge) * sign;
                    if ((before >= 0f) != (after >= 0f))
                        output[written++] = Vector3.LerpUnclamped(previous, current, before / (before - after));
                    if (after >= 0f) output[written++] = current;
                    previous = current;
                    before = after;
                }
                return written;
            }
        }
    }
}
