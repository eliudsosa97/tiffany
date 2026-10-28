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
            string profilePath = "Assets/Scenes/Generated/EchoVolumeProfile.asset", Transform ball = null)
        {
            if (positions == null || positions.Length != 5)
                throw new System.ArgumentException($"{GameInfo.LogPrefix} La ruta necesita cinco posiciones de recuerdos.", nameof(positions));
            if (ball == null) ball = positions[0].parent.Find("Ball");
            var spots = new MemorySpot[positions.Length + 1];
            for (int i = 0; i < positions.Length; i++)
                spots[i == 0 ? 0 : i + 1] = BuildSpot(positions[i], i + 1, positions[i].position);
            spots[1] = BuildSpot(ball.parent, MemoryProgress.BallId, ball.position, MemorySpotKind.Ball, ball.gameObject);
            var collector = player.gameObject.AddComponent<MemoryCollector>();
            collector.soul = player.GetComponent<PlayerSoul>();
            collector.hiding = player.GetComponent<PlayerHiding>();
            collector.gameInterface = gameInterface;
            collector.tyler = tyler;
            collector.spots = spots;
            collector.echoVolume = BuildEchoVolume(zone, profilePath);
            collector.viewCamera = viewCamera;

            BuildCaptureStops(zone, collector, spots[0], "Memory 0 Spot", "Memory 1 Echo");
            BuildCaptureStops(zone, collector, spots[1], "Ball 0 Before", "Ball 1 Tyler");
        }

        static void BuildCaptureStops(Transform zone, MemoryCollector collector, MemorySpot spot,
            string before, string after)
        {
            Vector3 focus = spot.transform.position;
            Vector3 position = focus - Vector3.forward * 1.5f;
            if (spot.kind == MemorySpotKind.Ball) focus = position;
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

        static MemorySpot BuildSpot(Transform zone, int id, Vector3 position,
            MemorySpotKind kind = MemorySpotKind.Memory, GameObject visual = null)
        {
            var root = new GameObject(kind == MemorySpotKind.Ball ? "Ball Pickup" : $"Memory {id}");
            root.transform.SetParent(zone, false);
            root.transform.position = position;
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 1.2f;
            sphere.isTrigger = true;
            var spot = root.AddComponent<MemorySpot>();
            spot.memoryId = id;
            spot.kind = kind;
            if (visual != null)
            {
                spot.visual = visual;
                return spot;
            }
            // Veladora provisional hasta recibir el objeto de Clau.
            visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
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
