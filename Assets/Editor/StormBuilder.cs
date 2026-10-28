using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.EditorTools
{
    public static class StormBuilder
    {
        public static Storm BuildStorm(Transform player, Transform spawn)
        {
            if (RenderSettings.sun == null)
                throw new System.InvalidOperationException($"{GameInfo.LogPrefix} No se encontró la luna en RenderSettings.sun para la tormenta.");
            var rainClip = LoadClip("rain");
            var thunderClips = new[] { LoadClip("thunder_1"), LoadClip("thunder_2") };
            var storm = new GameObject("Storm").AddComponent<Storm>();
            storm.raining = false;
            storm.moon = RenderSettings.sun;
            storm.rainSource = new GameObject("Rain").AddComponent<AudioSource>();
            storm.rainSource.transform.SetParent(storm.transform, false);
            storm.rainSource.spatialBlend = 0f;
            storm.rainSource.playOnAwake = false;
            storm.rainSource.loop = true;
            storm.rainSource.volume = 0.5f;
            storm.rainSource.clip = rainClip;
            storm.thunderSource = new GameObject("Thunder").AddComponent<AudioSource>();
            storm.thunderSource.transform.SetParent(storm.transform, false);
            storm.thunderSource.spatialBlend = 0f;
            storm.thunderSource.playOnAwake = false;
            storm.thunderSource.volume = 1f;
            storm.thunderClips = thunderClips;
            storm.rain = new GameObject("Rain").AddComponent<ParticleSystem>();
            storm.rain.transform.SetParent(player, false);
            storm.rain.transform.localPosition = Vector3.up * 8f;
            storm.rain.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            storm.rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = storm.rain.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.45f;
            main.startSpeed = 18f;
            main.startSize = 0.025f;
            main.maxParticles = 1200;
            var emission = storm.rain.emission;
            emission.rateOverTime = 900f;
            var shape = storm.rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 24f, 0.1f);
            var collision = storm.rain.collision;
            collision.enabled = false;
            var lights = storm.rain.lights;
            lights.enabled = false;
            var renderer = storm.rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 6f;
            renderer.velocityScale = 0.01f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = RainMaterial();
            for (int i = 0; i < 3; i++)
            {
                string stopName = i == 0 ? "Stop 7 Storm" : i == 1 ? "Stop 8 Lightning" : "Stop 9 Thunder";
                var stop = new GameObject(stopName);
                stop.transform.SetParent(storm.transform, false);
                stop.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                stop.AddComponent<CaptureStop>().wait = i == 0 ? 1.5f : i == 1 ? 0.3f : 2.5f;
                var stage = stop.AddComponent<StormCaptureStage>();
                stage.raining = true;
                stage.lightning = i > 0;
            }
            return storm;
        }

        static AudioClip LoadClip(string name)
        {
            string path = $"Assets/Audio/Sfx/{name}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                throw new System.InvalidOperationException($"{GameInfo.LogPrefix} No se encontró el audio de tormenta: {path}");
            return clip;
        }

        static Material RainMaterial()
        {
            GeneratedMaterials.EnsureFolder(GeneratedMaterials.folder);
            string path = $"{GeneratedMaterials.folder}/Rain.mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) throw new System.InvalidOperationException($"{GameInfo.LogPrefix} No se encontró el shader de lluvia.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew) material = new Material(shader) { name = "Rain" };
            material.shader = shader;
            material.shaderKeywords = null;
            material.SetColor("_BaseColor", new Color(0.65f, 0.8f, 1f, 0.18f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            if (isNew) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }
    }
}
