using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.EditorTools
{
    public static class TrailBirds
    {
        public static void Place(Transform path, int index, RouteTerrain terrain, int seed)
        {
            var polyline = RouteLayout.Path(index);
            var points = TrailBirdLayout.Points(polyline, terrain, seed);
            var spec = ModelLibrary.LoadSpec("trail-perch");
            Vector3 origin = RouteLayout.Clearing(index) + path.localPosition;
            for (int i = 0; i < points.Count; i++)
            {
                var group = new GameObject($"Trail Bird {i + 1}").transform;
                group.SetParent(path, false);
                group.localPosition = points[i].Position - origin;
                Vector3 forward = i + 1 < points.Count ? points[i + 1].Position - points[i].Position
                    : polyline[polyline.Length - 1] - polyline[polyline.Length - 2];
                group.localRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up));
                var perch = ModelLibrary.Instantiate("trail-perch", group);
                float heightScale = spec.heightScales[(int)(unchecked((uint)seed + (uint)i) % (uint)spec.heightScales.Length)];
                perch.transform.localScale = new Vector3(1f, heightScale, 1f);
                var collider = perch.AddComponent<CapsuleCollider>();
                collider.center = spec.trunkCollider.center;
                collider.radius = spec.trunkCollider.radius;
                collider.height = spec.trunkCollider.height;
                foreach (var node in perch.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(node.gameObject, StaticEditorFlags.BatchingStatic);
                var bird = ModelLibrary.Instantiate("trail-bird", group);
                bird.transform.localPosition = Vector3.Scale(spec.perchOffset, perch.transform.localScale);
                bird.AddComponent<TrailBird>().seed = unchecked(seed * 397 ^ i);
                foreach (var node in bird.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(node.gameObject, 0);
                foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                if (index == 0 && i == 0)
                {
                    Vector3 focus = points[i].Position + group.localRotation * bird.transform.localPosition;
                    Vector3 direction = Vector3.ProjectOnPlane(focus - polyline[0], Vector3.up).normalized;
                    Vector3 player = focus - direction * 3f;
                    player.y = terrain.Height(player.x, player.z);
                    Vector3 position = path.TransformPoint(player - origin);
                    Capture("Trail Bird 1", group, position, bird.transform.position,
                        path.rotation * Quaternion.Euler(FollowCamera.DefaultPitch, FollowCamera.DefaultYaw, 0f), FollowCamera.DefaultDistance);
                    Capture("Trail Bird 2", group, position, bird.transform.position,
                        path.rotation * Quaternion.Euler(15f, FollowCamera.DefaultYaw, 0f), 4f);
                }
            }
        }

        static void Capture(string name, Transform parent, Vector3 player, Vector3 focus, Quaternion rotation, float distance)
        {
            var stop = new GameObject(name);
            stop.transform.SetParent(parent, false);
            stop.transform.SetPositionAndRotation(player, Quaternion.LookRotation(Vector3.ProjectOnPlane(focus - player, Vector3.up)));
            var capture = stop.AddComponent<CaptureStop>();
            capture.overrideCamera = true;
            capture.cameraPosition = focus - rotation * Vector3.forward * distance;
            capture.cameraRotation = rotation;
            stop.AddComponent<LightCaptureStage>();
        }
    }
}
