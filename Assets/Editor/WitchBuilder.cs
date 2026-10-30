using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.EditorTools
{
    public static class WitchBuilder
    {
        public static GameObject BuildFigure(Transform parent)
        {
            var figure = ModelLibrary.Instantiate("witch", parent);
            figure.name = "WitchFigure";
            return figure;
        }

        internal static Material FireMaterial() =>
            GeneratedMaterials.Get("WitchFire", new Color(1f, 0.65f, 0.08f), true, 0.1f);

        public static void BuildSightRing(WitchController witch)
        {
            var material = GeneratedMaterials.Get("WitchSightRing", new Color(0.5f, 0.08f, 0.06f, 0.18f), true);
            material.shader = Shader.Find("Universal Render Pipeline/Tiffany Sight Ring")
                ?? throw new System.InvalidOperationException("No se encontró el shader del anillo de visión.");
            EditorUtility.SetDirty(material);
            string path = $"{System.IO.Path.GetDirectoryName(GeneratedMaterials.folder)}/WitchSightRing.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                var vertices = new Vector3[128];
                var uv = new Vector2[128];
                var triangles = new int[384];
                for (int i = 0; i < 64; i++)
                {
                    float angle = i * Mathf.PI * 2f / 64;
                    var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    int v = i * 2, t = i * 6, next = (i + 1) % 64 * 2;
                    vertices[v] = direction;
                    vertices[v + 1] = direction * 0.65f;
                    uv[v + 1] = Vector2.up;
                    triangles[t] = v; triangles[t + 1] = next; triangles[t + 2] = v + 1;
                    triangles[t + 3] = next; triangles[t + 4] = next + 1; triangles[t + 5] = v + 1;
                }
                mesh = new Mesh { name = "WitchSightRing", vertices = vertices, uv = uv, triangles = triangles };
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            var root = new GameObject("WitchSightRing");
            root.transform.SetParent(witch.transform.parent, false);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            var view = root.AddComponent<WitchSightRingView>();
            view.witch = witch;
            view.ring = renderer;
        }

        public static Light BuildFireBall(Transform parent, out GameObject ball)
        {
            var fire = FireMaterial();
            ball = CreateRoot("WitchFireBall", parent, Vector3.zero);
            var sphere = CreatePart("FireSphere", PrimitiveType.Sphere, ball.transform, Vector3.zero,
                Vector3.one * 0.9f, fire);
            var renderer = sphere.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var light = CreateRoot("FireLight", ball.transform, Vector3.zero).AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.range = 30f;
            light.intensity = 150f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            AssetDatabase.SaveAssets();
            return light;
        }

        static GameObject CreateRoot(string name, Transform parent, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            return root;
        }

        static GameObject CreatePart(string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, Material material, Vector3 rotation = default)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = Quaternion.Euler(rotation);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return part;
        }
    }
}
