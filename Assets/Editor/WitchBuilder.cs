using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.EditorTools
{
    public static class WitchBuilder
    {
        public static GameObject BuildFigure(Transform parent)
        {
            var skin = GeneratedMaterials.Get("WitchSkin", new Color(0.14f, 0.14f, 0.13f), false, 0.1f);
            var cloth = GeneratedMaterials.Get("WitchCloth", new Color(0.055f, 0.04f, 0.03f), false, 0.1f);
            var hair = GeneratedMaterials.Get("WitchHair", new Color(0.012f, 0.012f, 0.012f), false, 0.1f);
            var figure = CreateRoot("WitchFigure", parent, Vector3.zero);
            var root = figure.transform;

            CreatePart("LegL", PrimitiveType.Cylinder, root, new Vector3(-0.08f, 0.475f, 0f),
                new Vector3(0.07f, 0.475f, 0.07f), skin);
            CreatePart("LegR", PrimitiveType.Cylinder, root, new Vector3(0.08f, 0.475f, 0f),
                new Vector3(0.07f, 0.475f, 0.07f), skin);
            CreatePart("FootL", PrimitiveType.Sphere, root, new Vector3(-0.08f, 0.02f, 0.045f),
                new Vector3(0.075f, 0.04f, 0.17f), skin);
            CreatePart("FootR", PrimitiveType.Sphere, root, new Vector3(0.08f, 0.02f, 0.045f),
                new Vector3(0.075f, 0.04f, 0.17f), skin);
            CreatePart("Torso", PrimitiveType.Capsule, root, new Vector3(0f, 1.425f, 0f),
                new Vector3(0.28f, 0.525f, 0.2f), cloth, new Vector3(6f, 0f, 0f));

            var shoulderL = CreateRoot("ShoulderL", root, new Vector3(-0.135f, 1.9f, 0.05f)).transform;
            var shoulderR = CreateRoot("ShoulderR", root, new Vector3(0.135f, 2.05f, 0.05f)).transform;
            CreateSegment("CollarL", root, new Vector3(0f, 1.9f, 0.05f), shoulderL.localPosition, 0.055f, skin);
            CreateSegment("CollarR", root, new Vector3(0f, 1.91f, 0.05f), shoulderR.localPosition, 0.055f, skin);
            BuildArm(shoulderL, "L", new Vector3(-0.04f, -0.84f, 0f), new Vector3(-0.08f, -1.74f, 0.02f), skin);
            BuildArm(shoulderR, "R", new Vector3(0.1f, -0.55f, 0.07f), new Vector3(-0.01f, -0.85f, 0.38f), skin);

            CreateSegment("Neck", root, new Vector3(0f, 1.94f, 0.06f), new Vector3(-0.045f, 2.15f, 0.13f), 0.07f, skin);
            CreatePart("Head", PrimitiveType.Sphere, root, new Vector3(-0.045f, 2.23f, 0.13f),
                new Vector3(0.22f, 0.24f, 0.18f), skin, new Vector3(0f, 0f, 25f));
            CreatePart("HairL", PrimitiveType.Cube, root, new Vector3(-0.1f, 1.97f, -0.02f),
                new Vector3(0.1f, 0.6f, 0.025f), hair, new Vector3(16f, 0f, -8f));
            CreatePart("HairBack", PrimitiveType.Cube, root, new Vector3(0f, 2f, 0f),
                new Vector3(0.12f, 0.6f, 0.025f), hair, new Vector3(12f, 0f, 2f));
            CreatePart("HairR", PrimitiveType.Cube, root, new Vector3(0.1f, 1.99f, -0.025f),
                new Vector3(0.1f, 0.62f, 0.025f), hair, new Vector3(20f, 0f, 10f));
            CreatePart("HairFront", PrimitiveType.Cube, root, new Vector3(-0.055f, 1.96f, 0.255f),
                new Vector3(0.075f, 0.7f, 0.035f), hair, new Vector3(-4f, 0f, 12f));

            CreatePart("ShawlL", PrimitiveType.Cube, root, new Vector3(-0.145f, 1.66f, -0.055f),
                new Vector3(0.11f, 0.55f, 0.035f), cloth, new Vector3(6f, 0f, -8f));
            CreatePart("ShawlR", PrimitiveType.Cube, root, new Vector3(0.16f, 1.77f, -0.07f),
                new Vector3(0.11f, 0.57f, 0.035f), cloth, new Vector3(-9f, 0f, 12f));
            CreatePart("ShawlFront", PrimitiveType.Cube, root, new Vector3(0.145f, 1.82f, 0.17f),
                new Vector3(0.13f, 0.54f, 0.04f), cloth, new Vector3(-22f, 0f, 8f));
            CreatePart("ShawlTornEnd", PrimitiveType.Cube, root, new Vector3(-0.19f, 1.27f, -0.08f),
                new Vector3(0.075f, 0.3f, 0.025f), cloth, new Vector3(10f, 0f, 6f));

            AssetDatabase.SaveAssets();
            return figure;
        }

        public static Light BuildFireBall(Transform parent, out GameObject ball)
        {
            var fire = GeneratedMaterials.Get("WitchFire", new Color(1f, 0.65f, 0.08f), true, 0.1f);
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

        static void BuildArm(Transform shoulder, string side, Vector3 elbow, Vector3 handPosition, Material skin)
        {
            var arm = CreateRoot($"Arm{side}", shoulder, Vector3.zero).transform;
            CreateSegment($"UpperArm{side}", arm, Vector3.zero, elbow, 0.06f, skin);
            CreateSegment($"Forearm{side}", arm, elbow, handPosition, 0.06f, skin);
            var hand = CreateRoot($"Hand{side}", arm, handPosition).transform;
            CreatePart($"Palm{side}", PrimitiveType.Sphere, hand, Vector3.zero, new Vector3(0.055f, 0.08f, 0.055f), skin);
            for (int i = 0; i < 4; i++)
            {
                var rotation = new Vector3(20f, (i - 1.5f) * 14f, 0f);
                var start = new Vector3((i - 1.5f) * 0.017f, -0.025f, 0.02f);
                var center = start + Quaternion.Euler(rotation) * Vector3.forward * 0.12f;
                CreatePart($"Finger{side}{i + 1}", PrimitiveType.Cube, hand, center,
                    new Vector3(0.015f, 0.015f, 0.24f), skin, rotation);
            }
        }

        static void CreateSegment(string name, Transform parent, Vector3 start, Vector3 end, float diameter, Material material)
        {
            var direction = end - start;
            var part = CreatePart(name, PrimitiveType.Cylinder, parent, (start + end) * 0.5f,
                new Vector3(diameter, direction.magnitude * 0.5f, diameter), material);
            part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
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
