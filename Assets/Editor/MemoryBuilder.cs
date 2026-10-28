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
            collector.challenges = zone.GetComponentsInChildren<PowerChallenge>();
            collector.echoVolume = BuildEchoVolume(zone, profilePath);
            collector.viewCamera = viewCamera;
            var blackout = EchoBuilder.BuildBlackout(gameInterface);
            var echoes = new GameObject("Memory Echoes").transform;
            echoes.SetParent(zone, false);
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (spot.kind == MemorySpotKind.Ball) continue;
                EchoBuilder.Build(spot, echoes, gameInterface, blackout);
                EchoData.TryLoad(spot.echoPlayer.echoAsset.text, out var data, out _);
                var stop = new GameObject($"Echo {spot.memoryId} Memory");
                stop.transform.SetParent(zone, false);
                Vector3 position = TylerCaptureStage.GroundPosition(spot.transform.TransformPoint(new Vector3(-3f, 0f, -4f)));
                stop.transform.SetPositionAndRotation(position, spot.transform.rotation);
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
            var firstSpot = spots[0].transform;
            preview.transform.SetPositionAndRotation(
                TylerCaptureStage.GroundPosition(firstSpot.TransformPoint(Vector3.back * 1.5f)), firstSpot.rotation);
            var previewCapture = preview.AddComponent<CaptureStop>();
            previewCapture.overrideCamera = true;
            previewCapture.cameraPosition = firstSpot.TransformPoint(new Vector3(4f, 3f, -3f));
            previewCapture.cameraRotation = Quaternion.LookRotation(firstSpot.position - previewCapture.cameraPosition);
            preview.AddComponent<LightCaptureStage>().energy = SoulEnergy.Max;
            var memoryStage = preview.AddComponent<MemoryCaptureStage>();
            memoryStage.collector = collector;
            memoryStage.spot = spots[0];
            memoryStage.collect = false;
            BuildBallCaptureStops(zone, collector, spots[1]);
        }

        static void BuildBallCaptureStops(Transform zone, MemoryCollector collector, MemorySpot spot)
        {
            Vector3 position = spot.transform.position - Vector3.forward * 1.5f;
            Vector3 cameraPosition = position + new Vector3(4f, 3f, -3f);
            Quaternion cameraRotation = Quaternion.LookRotation(position - cameraPosition);
            for (int i = 0; i < 2; i++)
            {
                var stop = new GameObject(i == 0 ? "Ball 0 Before" : "Ball 1 Tyler");
                stop.transform.SetParent(zone, false);
                stop.transform.position = position;
                var capture = stop.AddComponent<CaptureStop>();
                capture.wait = 1.5f;
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
