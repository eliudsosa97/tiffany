using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.EditorTools
{
    public static class StormBuilder
    {
        public static Storm BuildStorm(Transform player, Transform spawn)
        {
            var storm = new GameObject("Storm").AddComponent<Storm>();
            storm.raining = false;
            storm.lightning = new GameObject("Lightning").AddComponent<Light>();
            storm.lightning.transform.SetParent(storm.transform, false);
            storm.lightning.transform.rotation = Quaternion.Euler(60f, 20f, 0f);
            storm.lightning.type = LightType.Directional;
            storm.lightning.color = new Color(0.8f, 0.9f, 1f);
            storm.lightning.shadows = LightShadows.None;
            storm.lightning.intensity = 0f;
            storm.lightning.enabled = false;
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
            for (int i = 0; i < 2; i++)
            {
                var stop = new GameObject(i == 0 ? "Stop 7 Storm" : "Stop 8 Lightning");
                stop.transform.SetParent(storm.transform, false);
                stop.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                stop.AddComponent<CaptureStop>().wait = i == 0 ? 1.5f : 0.3f;
                var stage = stop.AddComponent<StormCaptureStage>();
                stage.raining = true;
                stage.lightning = i == 1;
            }
            return storm;
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
