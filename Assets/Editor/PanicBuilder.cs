using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static TiffanyGame.EditorTools.AudioBuilder;

namespace TiffanyGame.EditorTools
{
    public static class PanicBuilder
    {
        public static void BuildPanic(Transform spawn, PlayerLight playerLight, TylerController tyler, Transform interfaceRoot)
        {
            var panic = playerLight.gameObject.AddComponent<PanicController>();
            panic.playerLight = playerLight;
            panic.hiding = playerLight.GetComponent<PlayerHiding>();
            panic.witch = playerLight.witch;
            panic.tyler = tyler;
            panic.cueSource = playerLight.gameObject.AddComponent<AudioSource>();
            panic.cueSource.spatialBlend = 0f;
            panic.cueSource.playOnAwake = false;
            panic.fakeSteps = new[] { LoadClip("step_1"), LoadClip("step_2"), LoadClip("step_3"), LoadClip("step_4") };
            panic.whispers = new[] { LoadClip("whisper_1"), LoadClip("whisper_2"), LoadClip("whisper_3") };
            panic.sobs = new[] { LoadClip("sob_1"), LoadClip("sob_2") };
            BuildVignette(panic, interfaceRoot);
            var names = new[] { "Panic 1 Steps", "Panic 2 Whispers", "Panic 3 Sob" };
            var levels = new[] { 0.55f, 0.8f, 0.95f };
            for (int i = 0; i < names.Length; i++)
            {
                var stop = new GameObject(names[i]);
                stop.transform.SetParent(spawn.parent, false);
                stop.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                stop.AddComponent<CaptureStop>().wait = i == 1 ? 10.5f : 9f;
                stop.AddComponent<LightCaptureStage>();
                stop.AddComponent<PanicCaptureStage>().level = levels[i];
            }
        }

        static void BuildVignette(PanicController panic, Transform parent)
        {
            var root = new GameObject("Panic Vignette", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one * new PanicVignetteModel().Scale;
            var image = root.GetComponent<Image>();
            image.sprite = BuildVignetteSprite();
            image.color = Color.clear;
            image.raycastTarget = false;
            root.AddComponent<PanicVignette>().panic = panic;
        }

        static Sprite BuildVignetteSprite()
        {
            const int size = 256;
            const string path = "Assets/Scenes/Generated/PanicVignette.png";
            GeneratedMaterials.EnsureFolder("Assets/Scenes/Generated");
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x * 2f / (size - 1) - 1f;
                        float dy = y * 2f / (size - 1) - 1f;
                        float alpha = Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(0.3f, 0.95f, Mathf.Sqrt(dx * dx + dy * dy)));
                        pixels[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(alpha * 255f));
                    }
                texture.SetPixels32(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.maxTextureSize = size;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
