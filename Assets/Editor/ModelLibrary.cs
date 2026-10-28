using System;
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

        [Serializable]
        public class ModelSpec
        {
            public MaterialSpec[] materials;
        }

        [Serializable]
        public class MaterialSpec
        {
            public string name;
            public string color;
            public float smoothness;
        }

        public static ModelSpec LoadSpec(string id)
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), BlenderFolder, id + ".json");
            if (!File.Exists(path))
                throw new FileNotFoundException($"{GameInfo.LogPrefix} No existe la fuente del modelo '{id}': {path}");
            var spec = JsonUtility.FromJson<ModelSpec>(File.ReadAllText(path));
            if (spec?.materials == null || spec.materials.Length == 0)
                throw new InvalidDataException($"{GameInfo.LogPrefix} {path} no define materiales.");
            return spec;
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
                    GeneratedMaterials.Get(entry.name, color, false, entry.smoothness).SetColor("_Color", color);
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
            }
            return root;
        }
    }
}
