using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.EditorTools
{
    public static class RiverPieces
    {
        public const float BankLength = 10f;
        public const float BankCurveRadius = 18f;

        public static GameObject Rock(int index, Transform parent)
        {
            if (index < 1 || index > 4) throw new ArgumentOutOfRangeException(nameof(index));
            var root = Create($"river-rock-{index}", parent, index >= 3);
            AddStoneColliders(root, false);
            return root;
        }

        public static GameObject Step(int index, Transform parent)
        {
            if (index < 1 || index > 2) throw new ArgumentOutOfRangeException(nameof(index));
            var root = Create($"river-step-{index}", parent);
            AddStoneColliders(root, true);
            return root;
        }

        public static GameObject Log(Transform parent)
        {
            var root = Create("river-log", parent);
            var spec = ModelLibrary.LoadSpec("river-log").trunkCollider;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.center = spec.center;
            collider.radius = spec.radius;
            collider.height = spec.height;
            root.AddComponent<GroundSurface>().type = GroundType.Branches;
            return root;
        }

        public static GameObject Bank(bool curve, Transform parent, bool mirror = false)
        {
            var root = Create(curve ? "river-bed-curve" : "river-bed-straight", parent);
            root.transform.localScale = new Vector3(mirror ? -1f : 1f, 1f, 1f);
            return root;
        }

        public static GameObject Plant(Transform parent) => Create("plant-tussock", parent);

        public static void LayBanks(Transform parent, RouteTerrain terrain)
        {
            var pieces = RiverBankLayout.Pieces(terrain, BankLength, BankLength);
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                var bank = Bank(false, parent, piece.Mirror).transform;
                bank.localPosition = new Vector3(piece.Start.x, terrain.Crossing.y, piece.Start.y);
                bank.localRotation = Quaternion.LookRotation(new Vector3(piece.Direction.x, 0f, piece.Direction.y));
                bank.localScale = new Vector3(piece.Mirror ? -1f : 1f, 1f, piece.Length / BankLength);
                if (i % 3 != 1) continue;
                var outward = new Vector2(piece.Direction.y, -piece.Direction.x) * (piece.Mirror ? 1f : -1f);
                var spot = piece.Middle + outward * 1.6f;
                Plant(parent).transform.localPosition = new Vector3(spot.x, terrain.GroundHeight(spot.x, spot.y), spot.y);
            }
        }

        static void AddStoneColliders(GameObject root, bool walkable)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = true;
                if (walkable) filter.gameObject.AddComponent<GroundSurface>().type = GroundType.Gravel;
            }
        }

        static GameObject Create(string id, Transform parent, bool shadows = false)
        {
            var root = ModelLibrary.Instantiate(id, parent);
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(node.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(node.gameObject) | StaticEditorFlags.BatchingStatic);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            return root;
        }
    }
}
