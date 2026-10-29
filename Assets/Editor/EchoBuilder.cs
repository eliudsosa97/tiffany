using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static class EchoBuilder
    {
        sealed class Palette
        {
            public readonly Material pale = GeneratedMaterials.Get("EchoPale", new Color(0.78f, 0.84f, 0.82f), true);
            public readonly Material stone = GeneratedMaterials.Get("EchoStone", new Color(0.26f, 0.29f, 0.28f), true);
            public readonly Material dark = GeneratedMaterials.Get("EchoDark", new Color(0.025f, 0.02f, 0.025f), true);
            public readonly Material fire = GeneratedMaterials.Get("EchoFire", new Color(1f, 0.34f, 0.05f), true);
        }

        public static Image BuildBlackout(GameInterface ui)
        {
            var root = new GameObject("Echo Blackout", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(ui.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex(0);
            var image = root.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            root.SetActive(false);
            return image;
        }

        public static void Build(MemorySpot spot, Transform parent, GameInterface ui, Image blackout)
        {
            string path = $"Assets/Data/Echoes/echo-{spot.memoryId}.json";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta {path}.");
            if (!EchoData.TryLoad(asset.text, out var data, out string error))
                throw new InvalidOperationException($"{GameInfo.LogPrefix} {path}: {error}.");
            if (data.id != spot.memoryId) throw new InvalidOperationException($"{GameInfo.LogPrefix} {path}: id distinto del MemorySpot.");
            var palette = new Palette();
            var stage = Root($"Echo {spot.memoryId}", parent);
            stage.SetPositionAndRotation(spot.transform.position, spot.transform.rotation);
            var player = spot.gameObject.AddComponent<EchoPlayer>();
            spot.echoPlayer = player;
            player.echoAsset = asset;
            player.gameInterface = ui;
            player.blackout = blackout;
            player.stageRoot = stage.gameObject;
            Physics.SyncTransforms();
            if (Physics.Raycast(spot.transform.position + Vector3.up * 3f, Vector3.down, out var ground,
                Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) player.ground = ground.collider;
            player.girl = Root("Living Girl", stage);
            Part("Girl", PrimitiveType.Capsule, player.girl, new Vector3(0f, 0.6f, 0f), new Vector3(0.38f, 0.6f, 0.34f), palette.pale);
            Part("Facing", PrimitiveType.Sphere, player.girl, new Vector3(0f, 1f, 0.16f), Vector3.one * 0.1f, palette.stone);
            player.tyler = Root("Living Tyler", stage);
            TylerBuilder.BuildFigure(player.tyler, out _);
            player.ApplyPose(player.girl, EchoEvaluator.Pose(data.girl, 0f));
            player.ApplyPose(player.tyler, EchoEvaluator.Pose(data.tyler, 0f));
            player.props = new Transform[data.props.Length];
            for (int i = 0; i < data.props.Length; i++)
            {
                var prop = data.props[i];
                var propParent = prop.parent == "girl" ? player.girl : prop.parent == "tyler" ? player.tyler : stage;
                var root = Root(prop.type, propParent);
                BuildProp(prop.type, root, palette);
                player.ApplyPose(root, EchoEvaluator.Pose(prop, 0f));
                player.props[i] = root;
            }
            int count = 0;
            player.clips = new AudioClip[data.sounds.Length];
            for (int i = 0; i < data.sounds.Length; i++)
            {
                string clipPath = $"Assets/Audio/Sfx/{data.sounds[i].clip}.wav";
                player.clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (player.clips[i] == null) throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta el audio {clipPath}.");
                count += 1 + data.sounds[i].copies.Length;
            }
            player.sources = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                var source = Root($"Echo Voice {i + 1}", stage).gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                player.sources[i] = source;
            }
            stage.gameObject.SetActive(false);
        }

        static void BuildProp(string type, Transform root, Palette p)
        {
            switch (type)
            {
                case "grave":
                    Part("Stone", PrimitiveType.Cube, root, new Vector3(0f, 0.6f, 0f), new Vector3(0.9f, 1.2f, 0.24f), p.stone);
                    Part("Mound", PrimitiveType.Cube, root, new Vector3(0f, 0.05f, -0.7f), new Vector3(1f, 0.1f, 1.5f), p.stone);
                    break;
                case "flowers":
                    for (int i = 0; i < 5; i++)
                        Part("Flower", PrimitiveType.Sphere, root, new Vector3((i - 2) * 0.12f, 0.1f, i % 2 * 0.15f), Vector3.one * 0.2f, p.pale);
                    break;
                case "ball":
                    Part("Ball", PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.24f, p.pale);
                    break;
                case "arm":
                    Part("Arm", PrimitiveType.Capsule, root, Vector3.down * 0.22f, new Vector3(0.1f, 0.25f, 0.1f), p.pale);
                    break;
                case "gate":
                    for (int i = -4; i <= 4; i++)
                        Part("Bar", PrimitiveType.Cube, root, new Vector3(i * 0.35f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.05f), p.stone);
                    for (int i = 0; i < 2; i++)
                        Part("Crossbar", PrimitiveType.Cube, root, new Vector3(0f, 0.3f + i * 1.2f, 0f), new Vector3(3f, 0.06f, 0.06f), p.stone);
                    break;
                case "ribbon":
                    Part("Knot", PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.1f, p.pale);
                    Part("Ribbon", PrimitiveType.Cube, root, Vector3.down * 0.15f, new Vector3(0.16f, 0.36f, 0.03f), p.pale);
                    break;
                case "rail":
                    for (int i = -4; i <= 4; i++)
                        Part("Sleeper", PrimitiveType.Cube, root, new Vector3(0f, 0.04f, i * 0.8f), new Vector3(1.8f, 0.08f, 0.2f), p.stone);
                    for (int i = -1; i <= 1; i += 2)
                        Part("Rail", PrimitiveType.Cube, root, new Vector3(i * 0.55f, 0.15f, 0f), new Vector3(0.12f, 0.18f, 7f), p.pale);
                    break;
                case "doll":
                    Part("Body", PrimitiveType.Capsule, root, Vector3.up * 0.16f, new Vector3(0.16f, 0.16f, 0.12f), p.pale);
                    Part("Head", PrimitiveType.Sphere, root, Vector3.up * 0.36f, Vector3.one * 0.18f, p.pale);
                    Part("Legs", PrimitiveType.Cube, root, new Vector3(0f, 0.06f, 0.13f), new Vector3(0.18f, 0.1f, 0.28f), p.pale);
                    break;
                case "mud":
                    Part("Mud", PrimitiveType.Cube, root, Vector3.zero, new Vector3(2.8f, 0.06f, 2.6f), p.dark);
                    break;
                case "shoe":
                    Part("Shoe", PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.15f, 0.12f, 0.3f), p.stone);
                    break;
                case "door":
                    for (int i = -1; i <= 1; i += 2)
                        Part("Post", PrimitiveType.Cube, root, new Vector3(i * 1.1f, 1f, 0f), new Vector3(0.16f, 2f, 0.15f), p.stone);
                    Part("Lintel", PrimitiveType.Cube, root, Vector3.up * 2f, new Vector3(2.4f, 0.2f, 0.2f), p.stone);
                    Part("Broken Door", PrimitiveType.Cube, root, new Vector3(-1f, 0.65f, 0.25f), new Vector3(0.5f, 1.3f, 0.1f), p.stone, new Vector3(0f, 25f, -18f));
                    break;
                case "hut":
                    BuildHut(root, p);
                    break;
                case "bracelet":
                case "beads":
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI * 0.25f;
                        var position = type == "bracelet" ? new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.08f
                            : new Vector3((i - 3.5f) * 0.06f, 0f, (i % 3 - 1) * 0.09f);
                        Part("Bead", PrimitiveType.Sphere, root, position, Vector3.one * 0.045f, p.pale);
                    }
                    break;
                case "hand":
                    BuildHand(root, p.dark);
                    break;
            }
        }

        static void BuildHut(Transform root, Palette p)
        {
            Part("Dark Doorway", PrimitiveType.Cube, root, new Vector3(0f, 1f, 0.15f), new Vector3(1.4f, 2f, 0.08f), p.dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Part("Side Wall", PrimitiveType.Cube, root, new Vector3(side * 1.5f, 1f, 1.3f), new Vector3(0.15f, 2f, 2.6f), p.stone);
                Part("Fire Behind Gaps", PrimitiveType.Cube, root, new Vector3(side * 1.1f, 1f, 0.05f), new Vector3(0.75f, 1.9f, 0.04f), p.fire);
                for (int i = 0; i < 7; i++)
                    Part("Plank", PrimitiveType.Cube, root, new Vector3(side * 1.1f, 0.15f + i * 0.29f, -0.01f), new Vector3(0.8f, 0.26f, 0.08f), p.stone);
            }
            Part("Back Wall", PrimitiveType.Cube, root, new Vector3(0f, 1f, 2.6f), new Vector3(3.1f, 2f, 0.15f), p.stone);
            Part("Roof", PrimitiveType.Cube, root, new Vector3(0f, 2.1f, 1.2f), new Vector3(3.4f, 0.2f, 3f), p.stone);
            var fire = Root("Firelight", root).gameObject.AddComponent<Light>();
            fire.transform.localPosition = new Vector3(0f, 0.8f, 0.6f);
            fire.type = LightType.Point;
            fire.color = new Color(1f, 0.35f, 0.08f);
            fire.intensity = 2f;
            fire.range = 4f;
            fire.shadows = LightShadows.None;
        }

        static void BuildHand(Transform root, Material dark)
        {
            // El modelo de la bruja une cada pose en una sola malla, incluida la mano.
            Part("Forearm", PrimitiveType.Capsule, root, new Vector3(0f, 0f, 1f), new Vector3(0.12f, 1f, 0.1f), dark, new Vector3(90f, 0f, 0f));
            Part("Palm", PrimitiveType.Sphere, root, Vector3.zero, new Vector3(0.25f, 0.1f, 0.38f), dark);
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * 0.06f;
                Part("Finger", PrimitiveType.Capsule, root, new Vector3(x, 0f, -0.3f), new Vector3(0.045f, 0.22f, 0.045f), dark, new Vector3(80f, 0f, (i - 2) * 5f));
                Part("Fingertip", PrimitiveType.Capsule, root, new Vector3(x, -0.07f, -0.49f), new Vector3(0.04f, 0.1f, 0.04f), dark, new Vector3(25f, 0f, 0f));
            }
        }

        static Transform Root(string name, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        static void Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, Vector3 rotation = default)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(rotation));
            part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
