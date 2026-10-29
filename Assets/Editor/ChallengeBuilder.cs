using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame.EditorTools
{
    public static class ChallengeBuilder
    {
        static readonly RouteTerrain Terrain = new RouteTerrain();
        static readonly Pose[] Placements =
        {
            OnPath(1, 4, 0.5f), OnPath(2, 2, 0.5f), OnPath(3, 2, 0.15f), OnPath(3, 5, 0.35f)
        };

        public static void Build(WitchChapterTracker tracker, PlayerSoul soul, Storm storm)
        {
            Physics.SyncTransforms();
            BuildOne(tracker, soul, storm, ChallengeKind.Candles, 1);
            BuildOne(tracker, soul, storm, ChallengeKind.Distract, 2);
            BuildOne(tracker, soul, storm, ChallengeKind.Vanish, 3);
            BuildOne(tracker, soul, storm, ChallengeKind.Hut, 3);
            Physics.SyncTransforms();
            foreach (var stage in tracker.GetComponentsInChildren<ChallengeCaptureStage>())
                if (!stage.after) stage.FrameCamera();
        }

        static Pose OnPath(int from, int segment, float fraction)
        {
            var points = RouteLayout.Path(from);
            Vector3 position = Vector3.Lerp(points[segment - 1], points[segment], fraction);
            position.y = Terrain.Height(position.x, position.z) + TerrainBuilder.FloorOffset;
            return new Pose(position, Quaternion.LookRotation(Vector3.ProjectOnPlane(points[segment] - points[segment - 1], Vector3.up)));
        }

        public static bool Reserves(Vector3 point, float radius)
        {
            for (int i = 0; i < Placements.Length; i++)
            {
                var pose = Placements[i];
                Vector3 local = Quaternion.Inverse(pose.rotation) * (point - pose.position);
                var horizontal = new Vector2(local.x, local.z);
                if (RouteTerrain.RectangleDistance(horizontal, new Rect(-2.5f, -9f, 5f, 14f)) <= radius) return true;
                if ((i == (int)ChallengeKind.Distract || i == (int)ChallengeKind.Hut)
                    && RouteTerrain.RectangleDistance(horizontal, new Rect(2.5f, -0.5f, 5f, 4.5f)) <= radius) return true;
            }
            return false;
        }

        static void BuildOne(WitchChapterTracker tracker, PlayerSoul soul, Storm storm,
            ChallengeKind kind, int from)
        {
            Transform source = tracker.clearings[from];
            var pose = Placements[(int)kind];
            var root = new GameObject($"Challenge {(int)kind + 1} {kind}").transform;
            root.SetParent(source, false);
            root.SetPositionAndRotation(tracker.transform.TransformPoint(pose.position), tracker.transform.rotation * pose.rotation);
            var challenge = root.gameObject.AddComponent<PowerChallenge>();
            challenge.kind = kind;
            challenge.tracker = tracker;
            challenge.entryOffset = GroundLocal(root, challenge.entryOffset);
            challenge.exitOffset = GroundLocal(root, challenge.exitOffset);
            var rules = new ChallengeRules(kind);
            var timber = GeneratedMaterials.Get("RouteStructure", new Color(0.4f, 0.4f, 0.4f));
            float rechargeZ = kind == ChallengeKind.Hut ? -6f : rules.NeedsCandles ? -8f : -4f;
            challenge.checkpoint = Candle(root, "Challenge Family Candle", new Vector3(-0.8f, 0f, rechargeZ),
                CandleKind.Family, soul, storm, 1.2f);
            challenge.post = Point(root, "Watch Post", new Vector3(0f, 0f, 2.2f));

            Wall(root, "Passage Left", new Vector3(-1.6f, 0.3f, 2.2f), new Vector3(1.8f, 0.6f, 4.4f), timber);
            if (rules.NeedsDistraction)
            {
                for (int i = 0; i < 2; i++)
                    Wall(root, $"Passage Right {i}", new Vector3(1.6f, 0.3f, i == 0 ? 0.2f : 3.85f),
                        new Vector3(1.8f, 0.6f, i == 0 ? 0.4f : 1.1f), timber);
                BuildAlcove(challenge, timber);
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
                        GroundLocal(root, new Vector3(0f, 1.5f, z + 1.1f)), new Vector3(5f, 3f, 0.25f), dark);
                    gate.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                    challenge.darkness[i] = gate;
                }
            }

            for (int i = 0; i < 2; i++)
            {
                var stop = Point(root, $"{root.name} {(i == 0 ? "Before" : "After")}", new Vector3(0f, 0f, -0.3f));
                var capture = stop.gameObject.AddComponent<CaptureStop>();
                capture.overrideCamera = true;
                var stage = stop.gameObject.AddComponent<ChallengeCaptureStage>();
                stage.challenge = challenge;
                stage.after = i == 1;
            }
        }

        static void BuildAlcove(PowerChallenge challenge, Material timber)
        {
            Transform root = challenge.transform;
            for (int i = 0; i < 2; i++)
                Wall(root, $"Alcove Edge {i}", new Vector3(4.5f, 0.3f, i == 0 ? 0.3f : 3.4f), new Vector3(5f, 0.6f, 0.2f), timber);
            Wall(root, "Alcove End", new Vector3(7f, 0.3f, 1.85f), new Vector3(0.2f, 0.6f, 3.3f), timber);
            challenge.lure = PowerBuilder.BuildObject(root, ThrowableKind.Bell, root.TransformPoint(GroundLocal(root, new Vector3(6f, 0f, 2.2f))),
                0.35f, 0.35f, 0.3f, new Color(0.7f, 0.53f, 0.2f));
            challenge.lure.pushSpeed = 0.4f;
        }

        static CandleSpot Candle(Transform root, string name, Vector3 position, CandleKind kind,
            PlayerSoul soul, Storm storm, float radius)
        {
            var spot = SceneBuilder.CreateCandle(root, name, root.TransformPoint(GroundLocal(root, position)), kind, kind == CandleKind.Family, soul, storm);
            spot.radius = radius;
            spot.GetComponent<NavMeshObstacle>().radius = radius;
            return spot;
        }

        static Transform Point(Transform root, string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(root, false);
            point.localPosition = GroundLocal(root, position);
            return point;
        }

        static void Wall(Transform root, string name, Vector3 position, Vector3 size, Material material)
        {
            var wall = SceneBuilder.CreatePrimitive(name, PrimitiveType.Cube, root, GroundLocal(root, position), size, material);
            var modifier = wall.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NavMesh.GetAreaFromName("Not Walkable");
        }

        static Vector3 GroundLocal(Transform root, Vector3 offset)
        {
            Transform route = root.GetComponent<PowerChallenge>().tracker.transform;
            Vector3 point = route.InverseTransformPoint(root.TransformPoint(new Vector3(offset.x, 0f, offset.z)));
            point.y = Terrain.Height(point.x, point.z) + TerrainBuilder.FloorOffset;
            point = route.TransformPoint(point);
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * 2f, Vector3.down, 4f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (hit.transform.IsChildOf(route) && hit.collider.TryGetComponent<GroundSurface>(out _))
                    point.y = Mathf.Max(point.y, hit.point.y);
            return root.InverseTransformPoint(point + Vector3.up * offset.y);
        }

        public static bool IsMoving(Transform node)
        {
            if (node.GetComponentInParent<MovableObject>() != null) return true;
            var modifier = node.GetComponent<NavMeshModifier>();
            return modifier != null && modifier.ignoreFromBuild && node.GetComponentInParent<PowerChallenge>() != null;
        }
    }
}
