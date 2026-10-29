using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static class ModelLibrary
    {
        public const string ModelFolder = "Assets/Art/Models";
        public const string MaterialFolder = "Assets/Art/Models/Materials";
        public const string BlenderFolder = "Art/Blender";
        public const string KenneyFolder = "Assets/Art/Kenney";
        public const string SpiritShader = "TiffanyGame/Spirit";
        public static string materialFolder = MaterialFolder;
        public static string kenneyMaterialRoot = KenneyFolder;

        static readonly HashSet<string> Occluders = new HashSet<string>(StringComparer.Ordinal)
        {
            "oak", "bougainvillea",
            "Nature/tree_pineTallA_detailed", "Nature/tree_pineTallC_detailed", "Nature/tree_pineRoundC",
            "Nature/rock_largeA", "Nature/stone_tallA"
        };

        // Un modelo con tomas en su json lleva el controller que arma este registro.
        static readonly Dictionary<string, Func<RuntimeAnimatorController>> Controllers = new Dictionary<string, Func<RuntimeAnimatorController>>
        {
            ["witch"] = WitchAnimator.Controller,
            ["tyler"] = () => TylerAnimatorBuilder.Build(),
            ["tiffany-provisional"] = () => ModelAnimatorBuilder.Build("tiffany-provisional"),
        };

        [Serializable]
        public class ModelSpec
        {
            public string modelPath;
            public string controllerPath;
            public string rootMotionBone;
            public bool bakeAxisConversion;
            public bool preserveAnimationKeys;
            public int fps;
            public MaterialSpec[] materials;
            public ClipSpec[] clips;
            public CapsuleSpec trunkCollider;
            public BoxSpec hideVolume;
            public BoneSpec[] skeleton;
            public ControllerSpec controller;

            public bool Rigged => clips != null && clips.Length > 0;
        }

        [Serializable]
        public class CapsuleSpec
        {
            public Vector3 center;
            public float radius;
            public float height;
        }

        [Serializable]
        public class BoxSpec
        {
            public Vector3 center;
            public Vector3 size;
        }

        [Serializable]
        public class BoneSpec
        {
            public string name;
            public string parent;
            public float[] head;
            public float[] tail;
        }

        [Serializable]
        public class ControllerSpec
        {
            public string speedParameter;
            public LayerSpec[] layers;
        }

        [Serializable]
        public class LayerSpec
        {
            public string name;
            public string defaultState;
            public float weight = 1f;
            public bool writeDefaultValues = true;
            public MaskSpec mask;
            public StateSpec[] states;
        }

        [Serializable]
        public class MaskSpec
        {
            public string path;
            public string[] bones;
        }

        [Serializable]
        public class StateSpec
        {
            public string name;
            public string clip;
            public float blend;
            public BlendTreeSpec blendTree;
        }

        [Serializable]
        public class BlendTreeSpec
        {
            public string name;
            public bool normalizeSpeed;
            public BlendChildSpec[] children;
        }

        [Serializable]
        public class BlendChildSpec
        {
            public string clip;
            public float threshold;
        }

        [Serializable]
        public class ClipSpec
        {
            public string name;
            public int frames;
            public bool loop;
            public float speed;
            public float turnSpeed;
            public ContactSpec contacts;
        }

        [Serializable]
        public class ContactSpec
        {
            public float left = -1f;
            public float right = -1f;
        }

        [Serializable]
        public class MaterialSpec
        {
            public string name;
            public string color;
            public float smoothness;
            public float metallic;
            public string surface;
            public bool unlit;
            public bool spirit;
            public string rimColor = "#BFE3FF";
            public float rimPower = 3f;
            public float rimIntensity = 1.5f;
        }

        public static string SpecPath(string id) => Path.Combine(Path.GetDirectoryName(Application.dataPath), BlenderFolder, id + ".json");

        public static ModelSpec LoadSpec(string id)
        {
            string path = SpecPath(id);
            if (!File.Exists(path))
                throw new FileNotFoundException($"{GameInfo.LogPrefix} No existe la fuente del modelo '{id}': {path}");
            var spec = JsonUtility.FromJson<ModelSpec>(File.ReadAllText(path));
            CheckSpec(id, spec);
            return spec;
        }

        public static void CheckSpec(string id, ModelSpec spec)
        {
            if (spec?.materials == null || spec.materials.Length == 0)
                throw new InvalidDataException($"{GameInfo.LogPrefix} El modelo '{id}' no define materiales.");
            // Los .mat de todos los modelos comparten carpeta: el prefijo evita que uno pise al de otro.
            int dash = id.IndexOf('-');
            string family = dash < 0 ? id : id.Substring(0, dash);
            string prefix = char.ToUpperInvariant(family[0]) + family.Substring(1);
            foreach (var entry in spec.materials)
                if (entry.name == null || !entry.name.StartsWith(prefix, StringComparison.Ordinal))
                    throw new InvalidDataException($"{GameInfo.LogPrefix} El material '{entry.name}' de '{id}' debe empezar con '{prefix}'.");
        }

        public static void SyncMaterials(string id)
        {
            var spec = LoadSpec(id);
            string previous = GeneratedMaterials.folder;
            GeneratedMaterials.folder = materialFolder;
            try
            {
                foreach (var entry in spec.materials)
                {
                    if (!ColorUtility.TryParseHtmlString(entry.color, out var color))
                        throw new InvalidDataException($"{GameInfo.LogPrefix} Color inválido '{entry.color}' en {entry.name}.");
                    var material = GeneratedMaterials.Get(entry.name, color, entry.unlit, entry.smoothness,
                        occluder: Occluders.Contains(id));
                    material.SetFloat("_Metallic", entry.metallic);
                    bool transparent = entry.surface == "transparent";
                    if (transparent || material.GetFloat("_Surface") != 0f)
                    {
                        material.SetFloat("_Surface", transparent ? 1f : 0f);
                        material.SetFloat("_Blend", 0f);
                        BaseShaderGUI.SetupMaterialBlendMode(material);
                        material.SetShaderPassEnabled("ShadowCaster", !transparent);
                    }
                    if (entry.spirit)
                    {
                        var shader = Shader.Find(SpiritShader);
                        if (shader == null)
                            throw new InvalidOperationException($"{GameInfo.LogPrefix} No se encontró el shader '{SpiritShader}'.");
                        if (!ColorUtility.TryParseHtmlString(entry.rimColor, out var rimColor))
                            throw new InvalidDataException($"{GameInfo.LogPrefix} Color inválido '{entry.rimColor}' en {entry.name}.");
                        material.shader = shader;
                        material.shaderKeywords = Array.Empty<string>();
                        material.SetFloat("_TiffanyStencil", 0f);
                        material.renderQueue = -1;
                        material.SetColor("_RimColor", rimColor);
                        material.SetFloat("_RimPower", entry.rimPower);
                        material.SetFloat("_RimIntensity", entry.rimIntensity);
                    }
                    EditorUtility.SetDirty(material);
                }
            }
            finally
            {
                GeneratedMaterials.folder = previous;
            }
            AssetDatabase.SaveAssets();
        }

        public static GameObject InstantiateKenney(string kitAndModel, Transform parent, float height)
        {
            int slash = kitAndModel.IndexOf('/');
            if (slash < 1 || slash == kitAndModel.Length - 1)
                throw new ArgumentException("Se esperaba kit/modelo.", nameof(kitAndModel));
            if (height <= 0f || float.IsNaN(height) || float.IsInfinity(height))
                throw new ArgumentOutOfRangeException(nameof(height));
            string kit = kitAndModel.Substring(0, slash);
            string id = kitAndModel.Substring(slash + 1);
            string path = $"{KenneyFolder}/{kit}/Models/{id}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new FileNotFoundException($"{GameInfo.LogPrefix} No existe el modelo {path}.");
            string palettePath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Art/Kenney", kit.ToLowerInvariant() + ".json");
            var palette = File.Exists(palettePath) ? JsonUtility.FromJson<ModelSpec>(File.ReadAllText(palettePath)) : null;
            var root = new GameObject(id);
            root.transform.SetParent(parent, false);
            var instance = Object.Instantiate(asset, root.transform, false);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            string previous = GeneratedMaterials.folder;
            GeneratedMaterials.folder = $"{kenneyMaterialRoot}/{kit}/Materials";
            try
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                        slots[i] = KenneyMaterial(kit, slots[i], palette, Occluders.Contains(kitAndModel));
                    renderer.sharedMaterials = slots;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                var bounds = LocalBounds(root.transform);
                if (bounds.size.y <= 0f) throw new InvalidDataException($"{GameInfo.LogPrefix} {path} no tiene altura.");
                float scale = height / bounds.size.y;
                instance.transform.localScale *= scale;
                instance.transform.localPosition = (instance.transform.localPosition -
                    new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)) * scale;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
            finally
            {
                GeneratedMaterials.folder = previous;
            }
            AssetDatabase.SaveAssets();
            return root;
        }

        static Material KenneyMaterial(string kit, Material source, ModelSpec palette, bool occluder)
        {
            string name = source != null ? source.name : "default";
            var color = source != null && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                : source != null && source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            var entry = palette?.materials == null ? null : Array.Find(palette.materials, candidate => candidate.name == name);
            if (entry != null && !ColorUtility.TryParseHtmlString(entry.color, out color))
                throw new InvalidDataException($"{GameInfo.LogPrefix} Color inválido '{entry.color}' en {name}.");
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.Replace('/', '_').Replace('\\', '_');
            var material = GeneratedMaterials.Get("Kenney" + kit + name + (occluder ? "" : "Lit"), color, occluder: occluder);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.SetOverrideTag("RenderType", occluder ? "" : "Opaque");
            material.renderQueue = -1;
            material.shaderKeywords = Array.Empty<string>();
            foreach (string property in material.GetTexturePropertyNames()) material.SetTexture(property, null);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static Bounds LocalBounds(Transform root)
        {
            Bounds bounds = default;
            bool initialized = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var local = filter.sharedMesh.bounds;
                var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var point = matrix.MultiplyPoint3x4(corner);
                    if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        public static GameObject Instantiate(string id, Transform parent)
        {
            SyncMaterials(id);
            string path = $"{ModelFolder}/{id}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                throw new FileNotFoundException($"{GameInfo.LogPrefix} No existe el modelo {path}.");

            var root = Object.Instantiate(model, parent, false);
            root.name = id;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool unlit = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    string name = materials[i] != null ? materials[i].name : "(vacío)";
                    materials[i] = AssetDatabase.LoadAssetAtPath<Material>($"{materialFolder}/{name}.mat");
                    if (materials[i] == null)
                        throw new InvalidOperationException($"{GameInfo.LogPrefix} {renderer.name} usa el material '{name}', que no está en {materialFolder}.");
                    unlit |= materials[i].shader.name == "Universal Render Pipeline/Unlit";
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = unlit ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = !unlit;
                // Las poses salen mucho del reposo: sin margen el culling la apaga estando a la vista.
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    var bounds = skinned.localBounds;
                    bounds.Expand(Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
                    skinned.localBounds = bounds;
                }
            }
            if (LoadSpec(id).Rigged) Animate(id, root);
            return root;
        }

        static void Animate(string id, GameObject root)
        {
            if (!Controllers.TryGetValue(id, out var controller))
                throw new InvalidOperationException($"{GameInfo.LogPrefix} El modelo '{id}' tiene tomas pero no tiene controller registrado.");
            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                renderer.quality = SkinQuality.Bone4;
        }
    }
}
