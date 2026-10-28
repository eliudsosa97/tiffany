using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame.EditorTools
{
    public static class ChallengeBuilder
    {
        public static void Build(WitchChapterTracker tracker, PlayerSoul soul, Storm storm)
        {
            BuildOne(tracker, soul, storm, ChallengeKind.Candles, 1, 0.5f);
            BuildOne(tracker, soul, storm, ChallengeKind.Distract, 2, 0.5f);
            BuildOne(tracker, soul, storm, ChallengeKind.Vanish, 3, 0.35f);
            BuildOne(tracker, soul, storm, ChallengeKind.Hut, 3, 0.60f);
        }

        static void BuildOne(WitchChapterTracker tracker, PlayerSoul soul, Storm storm,
            ChallengeKind kind, int from, float fraction)
        {
            Transform source = tracker.clearings[from], next = tracker.clearings[from + 1];
            Vector3 direction = Vector3.ProjectOnPlane(next.position - source.position, Vector3.up).normalized;
            var root = new GameObject($"Challenge {(int)kind + 1} {kind}").transform;
            root.SetParent(source, false);
            root.SetPositionAndRotation(Vector3.Lerp(source.position, next.position, fraction), Quaternion.LookRotation(direction));
            var challenge = root.gameObject.AddComponent<PowerChallenge>();
            challenge.kind = kind;
            challenge.tracker = tracker;
            var rules = new ChallengeRules(kind);
            var timber = GeneratedMaterials.Get("RouteStructure", new Color(0.4f, 0.4f, 0.4f));
            challenge.checkpoint = Candle(root, "Challenge Family Candle", new Vector3(-0.8f, 0f, rules.NeedsCandles ? -8f : -4f),
                CandleKind.Family, soul, storm, 1.2f);
            challenge.post = Point(root, "Watch Post", new Vector3(0f, 0f, 2.2f));

            Wall(root, "Passage Left", new Vector3(-1.6f, 0.3f, 2.2f), new Vector3(1.8f, 0.6f, 4.4f), timber);
            if (rules.NeedsDistraction)
            {
                for (int i = 0; i < 2; i++)
                    Wall(root, $"Passage Right {i}", new Vector3(1.6f, 0.3f, i == 0 ? 0.2f : 3.85f),
                        new Vector3(1.8f, 0.6f, i == 0 ? 0.4f : 1.1f), timber);
                BuildAlcove(challenge, source, timber);
            }
            else Wall(root, "Passage Right", new Vector3(1.6f, 0.3f, 2.2f), new Vector3(1.8f, 0.6f, 4.4f), timber);

            if (rules.NeedsCandles)
            {
                challenge.candles = new CandleSpot[2];
                challenge.darkness = new GameObject[2];
                var dark = GeneratedMaterials.Get("ChallengeDarkness", new Color(0.005f, 0.006f, 0.012f), unlit: true);
                for (int i = 0; i < 2; i++)
                {
                    float z = -5f + i * 3f;
                    challenge.candles[i] = Candle(root, $"Challenge Kindled Candle {i + 1}", new Vector3(-1f, 0f, z),
                        CandleKind.Kindled, soul, storm, 1f);
                    var gate = SceneBuilder.CreatePrimitive($"Darkness {i + 1}", PrimitiveType.Cube, root,
                        new Vector3(0f, 1.5f, z + 1.1f), new Vector3(5f, 3f, 0.25f), dark);
                    gate.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                    challenge.darkness[i] = gate;
                }
            }

            for (int i = 0; i < 2; i++)
            {
                var stop = Point(root, $"{root.name} {(i == 0 ? "Before" : "After")}", new Vector3(0f, 0f, -0.3f));
                var capture = stop.gameObject.AddComponent<CaptureStop>();
                capture.overrideCamera = true;
                capture.cameraPosition = root.TransformPoint(new Vector3(-8f, 11f, -12f));
                capture.cameraRotation = Quaternion.LookRotation(root.TransformPoint(Vector3.forward) - capture.cameraPosition);
                var stage = stop.gameObject.AddComponent<ChallengeCaptureStage>();
                stage.challenge = challenge;
                stage.after = i == 1;
            }
        }

        static void BuildAlcove(PowerChallenge challenge, Transform source, Material timber)
        {
            Transform root = challenge.transform;
            var floor = GeneratedMaterials.Get("RouteGround", new Color(0.25f, 0.25f, 0.25f));
            SceneBuilder.CreatePrimitive("Lure Alcove", PrimitiveType.Cube, root,
                new Vector3(4.5f, -0.25f, 1.85f), new Vector3(5f, 0.5f, 2.9f), floor)
                .AddComponent<GroundSurface>().type = GroundType.Dirt;
            for (int i = 0; i < 2; i++)
                Wall(root, $"Alcove Edge {i}", new Vector3(4.5f, 0.3f, i == 0 ? 0.3f : 3.4f), new Vector3(5f, 0.6f, 0.2f), timber);
            Wall(root, "Alcove End", new Vector3(7f, 0.3f, 1.85f), new Vector3(0.2f, 0.6f, 3.3f), timber);
            challenge.lure = PowerBuilder.BuildObject(root, ThrowableKind.Bell, root.TransformPoint(new Vector3(6f, 0f, 2.2f)),
                0.35f, 0.35f, 0.3f, new Color(0.7f, 0.53f, 0.2f));
            challenge.lure.pushSpeed = 0.4f;

            var path = source.Find("Path to Next Clearing");
            foreach (Transform tree in path)
            {
                if (!tree.name.StartsWith("Trunk ", System.StringComparison.Ordinal)) continue;
                Vector3 local = root.InverseTransformPoint(tree.position);
                if (local.x > 2.5f && local.x < 7.5f && local.z > -0.5f && local.z < 4f)
                    tree.position += root.right * 5f;
            }
            var border = path.Find("Border 1").GetComponent<BoxCollider>();
            Vector3 center = border.transform.InverseTransformPoint(root.TransformPoint(new Vector3(2.75f, 0f, 2.2f)));
            float low = border.center.z - border.size.z * 0.5f;
            float high = border.center.z + border.size.z * 0.5f;
            float openingLow = Mathf.Clamp(center.z - 1.8f, low, high);
            float openingHigh = Mathf.Clamp(center.z + 1.1f, low, high);
            Vector3 size = border.size;
            Vector3 originalCenter = border.center;
            border.center = new Vector3(originalCenter.x, originalCenter.y, (low + openingLow) * 0.5f);
            border.size = new Vector3(size.x, size.y, openingLow - low);
            if (openingHigh < high)
            {
                var remainder = new GameObject("Border Beyond Alcove").transform;
                remainder.SetParent(path, false);
                remainder.SetPositionAndRotation(border.transform.position, border.transform.rotation);
                var collider = remainder.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(originalCenter.x, originalCenter.y, (openingHigh + high) * 0.5f);
                collider.size = new Vector3(size.x, size.y, high - openingHigh);
            }
        }

        static CandleSpot Candle(Transform root, string name, Vector3 position, CandleKind kind,
            PlayerSoul soul, Storm storm, float radius)
        {
            var spot = SceneBuilder.CreateCandle(root, name, root.TransformPoint(position), kind, kind == CandleKind.Family, soul, storm);
            spot.radius = radius;
            spot.GetComponent<NavMeshObstacle>().radius = radius;
            return spot;
        }

        static Transform Point(Transform root, string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(root, false);
            point.localPosition = position;
            return point;
        }

        static void Wall(Transform root, string name, Vector3 position, Vector3 size, Material material)
        {
            var wall = SceneBuilder.CreatePrimitive(name, PrimitiveType.Cube, root, position, size, material);
            var modifier = wall.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NavMesh.GetAreaFromName("Not Walkable");
        }

        public static bool IsMoving(Transform node)
        {
            if (node.GetComponentInParent<MovableObject>() != null) return true;
            var modifier = node.GetComponent<NavMeshModifier>();
            return modifier != null && modifier.ignoreFromBuild;
        }
    }
}
