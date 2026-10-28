using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TiffanyGame.EditorTools
{
    public static class MemoryBuilder
    {
        public static void Build(Transform zone, PlayerController player, GameInterface gameInterface,
            TylerController tyler, Camera viewCamera, Transform[] positions,
            string profilePath = "Assets/Scenes/Generated/EchoVolumeProfile.asset")
        {
            if (positions == null || positions.Length != 5)
                throw new System.ArgumentException($"{GameInfo.LogPrefix} La ruta necesita cinco posiciones de recuerdos.", nameof(positions));
            var spots = new MemorySpot[positions.Length];
            for (int i = 0; i < spots.Length; i++)
                spots[i] = BuildSpot(positions[i], i + 1, positions[i].position);
            var collector = player.gameObject.AddComponent<MemoryCollector>();
            collector.soul = player.GetComponent<PlayerSoul>();
            collector.hiding = player.GetComponent<PlayerHiding>();
            collector.gameInterface = gameInterface;
            collector.tyler = tyler;
            collector.spots = spots;
            collector.echoVolume = BuildEchoVolume(zone, profilePath);
            collector.viewCamera = viewCamera;

            Vector3 focus = spots[0].transform.position;
            Vector3 cameraPosition = focus + new Vector3(4f, 3f, -3f);
            Quaternion cameraRotation = Quaternion.LookRotation(focus - cameraPosition);
            for (int i = 0; i < 2; i++)
            {
                var stop = new GameObject(i == 0 ? "Memory 0 Spot" : "Memory 1 Echo");
                stop.transform.SetParent(zone, false);
                stop.transform.position = focus - Vector3.forward * 1.5f;
                var capture = stop.AddComponent<CaptureStop>();
                capture.wait = i == 0 ? 1.5f : MemoryEcho.Fade + 1.5f;
                capture.overrideCamera = true;
                capture.cameraPosition = cameraPosition;
                capture.cameraRotation = cameraRotation;
                stop.AddComponent<LightCaptureStage>().energy = SoulEnergy.Max;
                var stage = stop.AddComponent<MemoryCaptureStage>();
                stage.collector = collector;
                stage.spot = spots[0];
                stage.collect = i == 1;
            }
        }

        static Volume BuildEchoVolume(Transform zone, string path)
        {
            GeneratedMaterials.EnsureFolder(System.IO.Path.GetDirectoryName(path));
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "EchoVolumeProfile";
                AssetDatabase.CreateAsset(profile, path);
            }
            if (!profile.TryGet<ColorAdjustments>(out var color))
            {
                color = profile.Add<ColorAdjustments>();
                AssetDatabase.AddObjectToAsset(color, profile);
            }
            color.active = true;
            color.saturation.Override(-100f);
            EditorUtility.SetDirty(color);
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("Memory Echo").AddComponent<Volume>();
            volume.transform.SetParent(zone, false);
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            volume.weight = 0f;
            volume.priority = 100f;
            return volume;
        }

        static MemorySpot BuildSpot(Transform zone, int id, Vector3 position)
        {
            var root = new GameObject($"Memory {id}");
            root.transform.SetParent(zone, false);
            root.transform.position = position;
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 1.2f;
            sphere.isTrigger = true;
            var spot = root.AddComponent<MemorySpot>();
            spot.memoryId = id;
            // Veladora provisional hasta recibir el objeto de Clau.
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Memory Candle";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * 0.18f;
            visual.transform.localScale = new Vector3(0.22f, 0.18f, 0.22f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            var material = GeneratedMaterials.Get("MemoryCandle", new Color(0.95f, 0.9f, 0.75f));
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f) * 0.2f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(visual, StaticEditorFlags.BatchingStatic);
            spot.visual = visual;
            return spot;
        }
    }
}
