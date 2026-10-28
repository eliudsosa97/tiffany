using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.EditorTools
{
    public static class WitchBuilder
    {
        public static GameObject BuildFigure(Transform parent, out GameObject stand, out GameObject crouch)
        {
            var figure = ModelLibrary.Instantiate("witch", parent);
            figure.name = "WitchFigure";
            var standTransform = figure.transform.Find("WitchStand");
            var crouchTransform = figure.transform.Find("WitchCrouch");
            if (standTransform == null)
                throw new System.InvalidOperationException($"{GameInfo.LogPrefix} Falta la hija directa 'WitchStand' en el modelo 'witch'.");
            if (crouchTransform == null)
                throw new System.InvalidOperationException($"{GameInfo.LogPrefix} Falta la hija directa 'WitchCrouch' en el modelo 'witch'.");
            stand = standTransform.gameObject;
            crouch = crouchTransform.gameObject;
            stand.SetActive(true);
            crouch.SetActive(false);
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
