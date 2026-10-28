using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TiffanyGame.EditorTools
{
    public static class MemoryBuilder
    {
        public static void Build(Transform zone, PlayerController player, GameInterface gameInterface,
            TylerController tyler, Camera viewCamera)
        {
            var spots = new[]
            {
                BuildSpot(zone, 1, SceneBuilder.SurfacePoint(15f, 20f)),
                BuildSpot(zone, 2, SceneBuilder.SurfacePoint(-15f, 22f)),
                BuildSpot(zone, 3, SceneBuilder.SurfacePoint(0f, 22f)),
                BuildSpot(zone, 4, SceneBuilder.SurfacePoint(-22f, 0f)),
                BuildSpot(zone, 5, SceneBuilder.SurfacePoint(22f, 0f))
            };
            var collector = player.gameObject.AddComponent<MemoryCollector>();
            collector.soul = player.GetComponent<PlayerSoul>();
            collector.hiding = player.GetComponent<PlayerHiding>();
            collector.gameInterface = gameInterface;
            collector.tyler = tyler;
            collector.spots = spots;
            collector.echoVolume = BuildEchoVolume(zone);
            collector.viewCamera = viewCamera;
            var blackout = EchoBuilder.BuildBlackout(gameInterface);
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                EchoBuilder.Build(spot, gameInterface, blackout);
                EchoData.TryLoad(spot.echoPlayer.echoAsset.text, out var data, out _);
                var stop = new GameObject($"Echo {spot.memoryId} Memory");
                stop.transform.SetParent(zone, false);
                stop.transform.SetPositionAndRotation(spot.transform.TransformPoint(new Vector3(-3f, 0f, -4f)), spot.transform.rotation);
                var capture = stop.AddComponent<CaptureStop>();
                capture.wait = data.duration * 0.5f;
                capture.overrideCamera = true;
                Vector3 focus = spot.transform.TransformPoint(new Vector3(0f, 0.7f, 0.8f));
                capture.cameraPosition = spot.transform.TransformPoint(new Vector3(8f, 6f, -12f));
                capture.cameraRotation = Quaternion.LookRotation(focus - capture.cameraPosition);
                var stage = stop.AddComponent<EchoCaptureStage>();
                stage.collector = collector;
                stage.spot = spot;
            }
            var preview = new GameObject("Memory 0 Spot");
            preview.transform.SetParent(zone, false);
            preview.transform.position = SceneBuilder.SurfacePoint(15f, 18.5f);
            preview.AddComponent<CaptureStop>();
            preview.AddComponent<LightCaptureStage>().energy = SoulEnergy.Max;
            var memoryStage = preview.AddComponent<MemoryCaptureStage>();
            memoryStage.collector = collector;
            memoryStage.spot = spots[0];
            memoryStage.collect = false;
        }

        static Volume BuildEchoVolume(Transform zone)
        {
            const string path = "Assets/Scenes/Generated/EchoVolumeProfile.asset";
            GeneratedMaterials.EnsureFolder("Assets/Scenes/Generated");
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
            root.transform.rotation = Quaternion.LookRotation(SceneBuilder.SurfacePoint(0f, 1f) - SceneBuilder.SurfacePoint(0f, 0f));
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
            visual.GetComponent<Renderer>().sharedMaterial = material;
            spot.visual = visual;
            return spot;
        }
    }
}
