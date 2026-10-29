using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class OcclusionSceneAudit
    {
        public static void Log(Transform world, Transform tiffany, Transform tyler)
        {
            int occluders = 0, batches = 0, instances = 0;
            foreach (var renderer in world.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader.name != GeneratedMaterials.OcclusionShader
                        || material.IsKeywordEnabled("_TIFFANY_CHARACTER")) continue;
                    occluders++;
                    break;
                }
            }
            foreach (var forest in world.GetComponentsInChildren<ForestInstances>(true))
            {
                if (forest.data == null) continue;
                foreach (var batch in forest.data.batches)
                {
                    if (batch.material.shader.name != GeneratedMaterials.OcclusionShader) continue;
                    instances += batch.matrices.Length;
                    batches += (batch.matrices.Length + ForestInstances.MaxInstancesPerDraw - 1) / ForestInstances.MaxInstancesPerDraw;
                }
            }
            Debug.Log($"{GameInfo.LogPrefix} Visibilidad: {occluders} renderers con recorte, " +
                $"{instances} instancias en {batches} lotes; siluetas en {CountSilhouettes(tiffany)} renderers de Tiffany " +
                $"y {CountSilhouettes(tyler)} de Tyler (incluye poses inactivas). Sin renderers ni luces adicionales.");
        }

        static int CountSilhouettes(Transform root)
        {
            int count = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.shader.name == GeneratedMaterials.SilhouetteShader) count++;
            return count;
        }
    }
}
