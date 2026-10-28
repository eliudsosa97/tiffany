using UnityEditor;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class AudioBuilder
    {
        public static void BuildFootsteps(PlayerController player)
        {
            var steps = player.gameObject.AddComponent<PlayerFootsteps>();
            steps.source = player.gameObject.AddComponent<AudioSource>();
            steps.source.spatialBlend = 0f;
            steps.source.playOnAwake = false;
            steps.dirt = LoadSteps("step");
            steps.grass = LoadSteps("step_grass");
            steps.slab = LoadSteps("step_slab");
            steps.gravel = LoadSteps("step_gravel");
            steps.leaves = LoadSteps("step_leaves");
            steps.branches = LoadSteps("step_branches");
        }

        static AudioClip[] LoadSteps(string name) => new[]
            { LoadClip($"{name}_1"), LoadClip($"{name}_2"), LoadClip($"{name}_3"), LoadClip($"{name}_4") };

        public static AudioClip LoadClip(string name)
        {
            string path = $"Assets/Audio/Sfx/{name}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                throw new System.InvalidOperationException($"{GameInfo.LogPrefix} No se encontró el audio: {path}");
            return clip;
        }
    }
}
