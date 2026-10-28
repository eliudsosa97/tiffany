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
        public static string materialFolder = MaterialFolder;

        // Un modelo con tomas en su json lleva el controller que arma este registro.
        static readonly Dictionary<string, Func<RuntimeAnimatorController>> Controllers = new Dictionary<string, Func<RuntimeAnimatorController>>
        {
            ["witch"] = WitchAnimator.Controller,
        };

        [Serializable]
        public class ModelSpec
        {
            public MaterialSpec[] materials;
            public ClipSpec[] clips;

            public bool Rigged => clips != null && clips.Length > 0;
        }

        [Serializable]
        public class ClipSpec
        {
            public string name;
            public int frames;
            public bool loop;
        }

        [Serializable]
        public class MaterialSpec
        {
            public string name;
            public string color;
            public float smoothness;
            public float metallic;
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
                    // Sin _Color la segunda corrida reescribe el .mat: URP lo iguala a _BaseColor.
                    var material = GeneratedMaterials.Get(entry.name, color, false, entry.smoothness);
                    material.SetColor("_Color", color);
                    material.SetFloat("_Metallic", entry.metallic);
                }
            }
            finally
            {
                GeneratedMaterials.folder = previous;
            }
            AssetDatabase.SaveAssets();
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
                for (int i = 0; i < materials.Length; i++)
                {
                    string name = materials[i] != null ? materials[i].name : "(vacío)";
                    materials[i] = AssetDatabase.LoadAssetAtPath<Material>($"{materialFolder}/{name}.mat");
                    if (materials[i] == null)
                        throw new InvalidOperationException($"{GameInfo.LogPrefix} {renderer.name} usa el material '{name}', que no está en {materialFolder}.");
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
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
        }
    }
}
