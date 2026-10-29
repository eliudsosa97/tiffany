using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.EditorTools
{
    public static class StreetLamps
    {
        public const float Spacing = 22f;

        public static GameObject Place(Transform parent, Vector3 localPosition, float yaw, int seed)
        {
            var spec = ModelLibrary.LoadSpec("street-lamp");
            var root = ModelLibrary.Instantiate("street-lamp", parent);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var streetLamp = root.AddComponent<StreetLamp>();
            streetLamp.seed = seed;
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(node.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(node.gameObject) | StaticEditorFlags.BatchingStatic);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bool isBulb = renderer.sharedMaterial.name == "StreetLampBulb";
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = !isBulb;
                if (isBulb) streetLamp.bulb = renderer;
            }
            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.center = spec.trunkCollider.center;
            collider.radius = spec.trunkCollider.radius;
            collider.height = spec.trunkCollider.height;
            var lightObject = new GameObject("Lamp Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = spec.lightOffset;
            var lamp = lightObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 196f / 255f, 107f / 255f);
            lamp.range = 7f;
            lamp.shadows = LightShadows.None;
            lamp.intensity = streetLamp.baseIntensity;
            streetLamp.lamp = lamp;
            return root;
        }
    }
}
