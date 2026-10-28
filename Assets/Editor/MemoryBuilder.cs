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
                // Pelota provisional hasta recibir el objeto de Clau.
                BuildSpot(zone, MemoryProgress.BallId, SceneBuilder.SurfacePoint(9f, 24f), MemorySpotKind.Ball),
                BuildSpot(zone, 2, SceneBuilder.SurfacePoint(-15f, 22f))
            };
            var collector = player.gameObject.AddComponent<MemoryCollector>();
            collector.soul = player.GetComponent<PlayerSoul>();
            collector.hiding = player.GetComponent<PlayerHiding>();
            collector.gameInterface = gameInterface;
            collector.tyler = tyler;
            collector.spots = spots;
            collector.echoVolume = BuildEchoVolume(zone);
            collector.viewCamera = viewCamera;

            BuildCaptureStops(zone, collector, spots[0], SceneBuilder.SurfacePoint(15f, 18.5f),
                "Memory 0 Spot", "Memory 1 Echo");
            BuildCaptureStops(zone, collector, spots[1], SceneBuilder.SurfacePoint(9f, 22.5f),
                "Ball 0 Before", "Ball 1 Tyler");
        }

        static void BuildCaptureStops(Transform zone, MemoryCollector collector, MemorySpot spot,
            Vector3 position, string before, string after)
        {
            Vector3 focus = spot.kind == MemorySpotKind.Ball ? position : spot.transform.position;
            Vector3 cameraPosition = focus + new Vector3(4f, 3f, -3f);
            Quaternion cameraRotation = Quaternion.LookRotation(focus - cameraPosition);
            for (int i = 0; i < 2; i++)
            {
                var stop = new GameObject(i == 0 ? before : after);
                stop.transform.SetParent(zone, false);
                stop.transform.position = position;
                var capture = stop.AddComponent<CaptureStop>();
                capture.wait = i == 0 || spot.kind == MemorySpotKind.Ball ? 1.5f : MemoryEcho.Fade + 1.5f;
                capture.overrideCamera = true;
                capture.cameraPosition = cameraPosition;
                capture.cameraRotation = cameraRotation;
                stop.AddComponent<LightCaptureStage>().energy = SoulEnergy.Max;
                var stage = stop.AddComponent<MemoryCaptureStage>();
                stage.collector = collector;
                stage.spot = spot;
                stage.collect = i == 1;
            }
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

        static MemorySpot BuildSpot(Transform zone, int id, Vector3 position,
            MemorySpotKind kind = MemorySpotKind.Memory)
        {
            bool ball = kind == MemorySpotKind.Ball;
            var root = new GameObject(ball ? "Ball" : $"Memory {id}");
            root.transform.SetParent(zone, false);
            root.transform.position = position;
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 1.2f;
            sphere.isTrigger = true;
            var spot = root.AddComponent<MemorySpot>();
            spot.memoryId = id;
            spot.kind = kind;
            // Veladora provisional hasta recibir el objeto de Clau.
            var visual = GameObject.CreatePrimitive(ball ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
            visual.name = ball ? "Ball" : "Memory Candle";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * 0.18f;
            visual.transform.localScale = ball ? Vector3.one * 0.36f : new Vector3(0.22f, 0.18f, 0.22f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            var material = GeneratedMaterials.Get(ball ? "MemoryBall" : "MemoryCandle",
                ball ? new Color(1f, 0.12f, 0.04f) : new Color(0.95f, 0.9f, 0.75f));
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
