using UnityEditor;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class TylerBuilder
    {
        public static Light BuildShareLight(Transform parent)
        {
            var light = CreateRoot("Tyler Share Light", parent, Vector3.up * 0.8f).AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.65f, 0.8f, 1f);
            light.range = 4f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
            // En WebGL, Tyler tiene menor prioridad que veladoras y fuego de la bruja al exceder cuatro luces por objeto.
            light.renderMode = LightRenderMode.ForceVertex;
            light.enabled = false;
            return light;
        }

        // Figura provisional hasta disponer del modelo animado.
        public static GameObject BuildFigure(Transform parent, out Transform head, out GameObject stand, out GameObject crouch)
        {
            var fur = GeneratedMaterials.Get("TylerFur", new Color(0.92f, 0.92f, 0.9f));
            fur.EnableKeyword("_EMISSION");
            fur.SetColor("_EmissionColor", new Color(0.85f, 0.9f, 1f) * 0.12f);
            fur.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(fur);
            var figure = CreateRoot("TylerFigure", parent, Vector3.zero);
            stand = BuildBody("TylerStand", figure.transform, fur, false, out head);
            crouch = BuildBody("TylerCrouch", figure.transform, fur, true, out _);
            crouch.SetActive(false);
            return figure;
        }

        static GameObject BuildBody(string name, Transform parent, Material fur, bool crouched, out Transform head)
        {
            var root = CreateRoot(name, parent, Vector3.zero);
            float bodyHeight = crouched ? 0.25f : 0.34f;
            float legHeight = crouched ? 0.16f : 0.25f;
            CreatePart("Body", PrimitiveType.Sphere, root.transform, new Vector3(0f, bodyHeight, 0f),
                new Vector3(0.22f, 0.22f, 0.5f), fur);
            for (int i = 0; i < 4; i++)
                CreatePart($"Leg {i + 1}", PrimitiveType.Capsule, root.transform,
                    new Vector3(i % 2 == 0 ? -0.08f : 0.08f, legHeight * 0.5f, i < 2 ? 0.17f : -0.17f),
                    new Vector3(0.055f, legHeight * 0.5f, 0.055f), fur);
            CreatePart("Neck", PrimitiveType.Capsule, root.transform,
                new Vector3(0f, bodyHeight + 0.04f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), fur,
                new Vector3(crouched ? 55f : 25f, 0f, 0f));
            head = CreateRoot("Head", root.transform,
                crouched ? new Vector3(0f, 0.3f, 0.32f) : new Vector3(0f, 0.5f, 0.25f)).transform;
            CreatePart("Skull", PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.14f, 0.16f, 0.18f), fur);
            CreatePart("Muzzle", PrimitiveType.Sphere, head, new Vector3(0f, -0.025f, 0.1f),
                new Vector3(0.09f, 0.08f, 0.14f), fur);
            for (int i = 0; i < 2; i++)
                CreatePart($"Ear {i + 1}", PrimitiveType.Capsule, head,
                    new Vector3(i == 0 ? -0.055f : 0.055f, 0.13f, -0.025f),
                    new Vector3(0.045f, 0.08f, 0.055f), fur, new Vector3(0f, 0f, i == 0 ? 12f : -12f));
            CreatePart("Tail", PrimitiveType.Capsule, root.transform, new Vector3(0f, bodyHeight - 0.02f, -0.3f),
                new Vector3(0.035f, 0.11f, 0.035f), fur, new Vector3(-65f, 0f, 0f));
            return root;
        }

        static GameObject CreateRoot(string name, Transform parent, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            return root;
        }

        static void CreatePart(string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, Material material, Vector3 rotation = default)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = Quaternion.Euler(rotation);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
