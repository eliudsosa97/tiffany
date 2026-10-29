using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class NightPreview : MonoBehaviour
    {
        public const string TrailStopName = "Night Trail";
        const string CemeteryStopName = "Route 1 Cemetery";
        string directory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-nightpreview");
            if (index < 0) return;
            if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                Debug.LogError($"{GameInfo.LogPrefix} -nightpreview requiere una carpeta de salida.");
                Application.Quit(1);
                return;
            }
            new GameObject("NightPreview").AddComponent<NightPreview>().directory = Path.GetFullPath(args[index + 1]);
#endif
        }

        IEnumerator Start()
        {
            if (string.IsNullOrEmpty(directory)) yield break;
            yield return null;
            var tracker = FindAnyObjectByType<WitchChapterTracker>();
            var fog = FindAnyObjectByType<GroundFog>();
            var follow = FindAnyObjectByType<FollowCamera>();
            var stops = FindObjectsByType<CaptureStop>(FindObjectsSortMode.None);
            var cemetery = Array.Find(stops, stop => stop.name == CemeteryStopName);
            var trail = Array.Find(stops, stop => stop.name == TrailStopName);
            var tyler = FindAnyObjectByType<TylerController>(FindObjectsInactive.Include);
            if (tracker == null || tracker.player == null || fog == null || follow == null
                || cemetery == null || trail == null || tyler == null || RenderSettings.sun == null)
            {
                Debug.LogError($"{GameInfo.LogPrefix} Faltan personajes, cámara, luna, neblina o paradas de NightPreview. Regenera la escena.");
                Application.Quit(1);
                yield break;
            }

            gameObject.AddComponent<WitchAwayStage>().Apply();
            var storm = FindAnyObjectByType<Storm>();
            if (storm != null) storm.enabled = false;
            var gameInterface = FindAnyObjectByType<GameInterface>();
            if (gameInterface != null)
            {
                gameInterface.StartGame();
                gameInterface.enabled = false;
            }
            Time.timeScale = 0f;
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            var player = tracker.player;
            player.GetComponent<PlayerController>().enabled = false;
            var soul = player.GetComponent<PlayerSoul>();
            soul.Powers.Unlock(Power.Light);
            soul.Energy.ResetTo(SoulEnergy.Max);
            var light = player.GetComponent<PlayerLight>();
            var tylerStage = gameObject.AddComponent<TylerCaptureStage>();
            tylerStage.lightTyler = false;
            var properties = new MaterialPropertyBlock();
            Directory.CreateDirectory(directory);

            foreach (var stop in new[] { cemetery, trail })
            {
                stop.ApplyPose(player, follow);
                tylerStage.Apply(player);
                foreach (var view in NightView.Combinations)
                {
                    ApplyView(view, follow, fog, properties);
                    for (int state = 0; state < 2; state++)
                    {
                        if (light.State.IsOn != (state == 1)) light.Toggle();
                        yield return null;
                        yield return new WaitForEndOfFrame();
                        string name = $"{view.Name}_{stop.name}_{(state == 0 ? "off" : "on")}.png";
                        var image = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
                        Destroy(image);
                        Debug.Log($"{GameInfo.LogPrefix} Captura: {name}");
                    }
                }
            }
            Debug.Log($"{GameInfo.LogPrefix} NightPreview terminado: 16 capturas en {directory}.");
            Application.Quit(0);
        }

        public static void ApplyView(NightView view, FollowCamera follow, GroundFog fog, MaterialPropertyBlock properties)
        {
            follow.distance = view.CameraDistance;
            follow.lookAhead = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = view.FogStartDistance;
            RenderSettings.fogEndDistance = view.FogEndDistance;
            ApplyLook(view.Look, fog, properties);
            var camera = follow.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = view.Look.FogColor;
            camera.farClipPlane = view.FarClipPlane;
            follow.Snap();
        }

        static void ApplyLook(NightLook look, GroundFog fog, MaterialPropertyBlock properties)
        {
            RenderSettings.sun.intensity = look.MoonIntensity;
            RenderSettings.ambientLight = look.AmbientColor;
            RenderSettings.fogColor = look.FogColor;
            foreach (var layer in fog.layers)
            {
                if (layer.renderer == null) continue;
                layer.renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", look.GroundFogColor);
                properties.SetFloat("_LightStrength", look.GroundFogLightStrength);
                layer.renderer.SetPropertyBlock(properties);
            }
        }
    }
}
