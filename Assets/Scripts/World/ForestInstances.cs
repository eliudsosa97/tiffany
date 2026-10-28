using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame
{
    [ExecuteAlways]
    public sealed class ForestInstances : MonoBehaviour
    {
        public const int MaxInstancesPerDraw = 128;
        public ForestInstanceData data;
        readonly Plane[] planes = new Plane[6];
        ForestInstanceData.Batch[] cached;
        Matrix4x4[][] world;

        void OnEnable() => RenderPipelineManager.beginCameraRendering += Render;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Render;

        void Render(ScriptableRenderContext context, Camera camera)
        {
            if (data == null || (camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            if (cached != data.batches)
            {
                cached = data.batches;
                world = new Matrix4x4[cached.Length][];
                for (int i = 0; i < cached.Length; i++) world[i] = new Matrix4x4[cached[i].matrices.Length];
            }
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            var matrix = transform.localToWorldMatrix;
            for (int i = 0; i < cached.Length; i++)
            {
                var batch = cached[i];
                var bounds = TransformBounds(batch.bounds, matrix);
                if (!GeometryUtility.TestPlanesAABB(planes, bounds)) continue;
                for (int j = 0; j < world[i].Length; j++) world[i][j] = matrix * batch.matrices[j];
                var parameters = new RenderParams(batch.material)
                {
                    camera = camera, layer = gameObject.layer, worldBounds = bounds,
                    shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false,
                    lightProbeUsage = LightProbeUsage.Off, reflectionProbeUsage = ReflectionProbeUsage.Off,
                    motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
                };
                for (int start = 0; start < world[i].Length; start += MaxInstancesPerDraw)
                    Graphics.RenderMeshInstanced(parameters, batch.mesh, 0, world[i],
                        Mathf.Min(MaxInstancesPerDraw, world[i].Length - start), start);
            }
        }

        public static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
        {
            var bounds = new Bounds(matrix.MultiplyPoint3x4(source.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f))));
            return bounds;
        }
    }
}
