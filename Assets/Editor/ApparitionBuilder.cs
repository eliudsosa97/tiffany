using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace TiffanyGame.EditorTools
{
    public static class ApparitionBuilder
    {
        public static void BuildApparitions(Transform route, Transform player, WitchController witch, PlayerLight playerLight)
        {
            var root = new GameObject("Witch Apparitions").transform;
            root.SetParent(route, false);
            var terrain = new RouteTerrain();
            var candles = route.GetComponentsInChildren<CandleSpot>(true);
            var paths = new Vector3[RouteLayout.Count - 1][];
            for (int i = 0; i < paths.Length; i++) paths[i] = RouteLayout.Path(i);
            var material = SilhouetteMaterial();
            var idle = WitchAnimator.Clips()["Idle"];
            for (int i = 1; i <= 6; i++)
            {
                Vector3 entry = RouteLayout.Openings(i)[0];
                var path = paths[i - 1];
                Vector3 forward = path[path.Length - 1] - path[path.Length - 2];
                forward.y = 0f;
                forward.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, forward) * (i % 2 == 1 ? -1f : 1f);
                Vector3 position = entry + forward * 14f + side * 6f;
                while (Blocked(position, paths, candles, route)) position += side * 2f;
                position.y = terrain.Height(position.x, position.z);
                var figure = ModelLibrary.Instantiate("witch", root);
                figure.name = $"Witch Apparition {i + 1}";
                idle.SampleAnimation(figure, 0f);
                foreach (var animator in figure.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
                foreach (var collider in figure.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                figure.transform.localPosition = position;
                Vector3 facing = entry - position;
                facing.y = 0f;
                figure.transform.localRotation = Quaternion.LookRotation(facing);
                var apparition = figure.AddComponent<WitchApparition>();
                (apparition.player, apparition.witch, apparition.playerLight, apparition.clearing) = (player, witch, playerLight, i + 1);
                apparition.renderers = figure.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in apparition.renderers)
                {
                    var materials = renderer.sharedMaterials;
                    for (int slot = 0; slot < materials.Length; slot++) materials[slot] = material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.enabled = false;
                }
            }
        }
        private static bool Blocked(Vector3 point, Vector3[][] paths, CandleSpot[] candles, Transform route)
        {
            foreach (var path in paths)
                for (int i = 1; i < path.Length; i++)
                    if (FogDensity.DistanceToSegment(point, path[i - 1], path[i]) <= RouteLayout.PathWidth) return true;
            foreach (var candle in candles)
            {
                Vector3 offset = point - route.InverseTransformPoint(candle.transform.position);
                offset.y = 0f;
                float radius = Mathf.Max(CandleSite.DefaultRadius, candle.radius);
                if (offset.sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }
        private static Material SilhouetteMaterial()
        {
            var material = GeneratedMaterials.Get("WitchApparition", new Color(0.008f, 0.006f, 0.01f), unlit: true);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
