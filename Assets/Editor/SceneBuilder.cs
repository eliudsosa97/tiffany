using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TiffanyGame.EditorTools
{
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const float SlopeAngle = 8f;
        const float PatchHeight = 0.1f;
        const float PatchElevation = 0.03f;

        [MenuItem("Tiffany/Build Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureNight();
            CreateMoon();
            var spawn = BuildTestZone();
            var player = CreatePlayer(spawn, out var playerLight);
            CreateCamera(player);
            playerLight.witch = BuildWitch(spawn.parent, player.transform);
            var storm = StormBuilder.BuildStorm(player.transform, spawn);
            var firstCandle = BuildCandles(spawn.parent, player.GetComponent<PlayerSoul>(), storm);
            BuildInterface(out var caughtFade);
            var respawn = player.gameObject.AddComponent<PlayerRespawn>();
            respawn.witch = playerLight.witch;
            respawn.overlay = caughtFade;
            BuildLightStops(spawn.parent);
            HidingBuilder.BuildHideTest(spawn.parent, player);
            var candleStop = new GameObject("Candle 1 Safe");
            candleStop.transform.SetParent(spawn.parent, false);
            candleStop.transform.position = SurfacePoint(-5f, 1.5f);
            var capture = candleStop.AddComponent<CaptureStop>();
            capture.overrideCamera = false;
            capture.wait = 1.5f;
            candleStop.AddComponent<LightCaptureStage>().energy = SoulEnergy.Max;
            var caughtStop = new GameObject("Witch 8 Caught");
            caughtStop.transform.SetParent(spawn.parent, false);
            caughtStop.transform.position = spawn.position;
            var caughtCapture = caughtStop.AddComponent<CaptureStop>();
            caughtCapture.overrideCamera = false;
            caughtCapture.wait = CaughtSequence.FireDuration + CaughtSequence.FadeOutDuration + CaughtSequence.FadeInDuration * 0.5f;
            caughtStop.AddComponent<CaughtCaptureStage>().spot = firstCandle;
            SaveScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"{GameInfo.LogPrefix} Escena generada en {ScenePath}.");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static void ConfigureNight()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.035f, 0.065f);
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.025f, 0.035f, 0.055f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 90f;
        }

        static void CreateMoon()
        {
            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.7f, 0.8f, 1f);
            moon.intensity = 0.32f;
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            RenderSettings.sun = moon;
        }

        static Transform BuildTestZone()
        {
            var root = new GameObject("Test Zone").transform;
            var rotation = Quaternion.Euler(-SlopeAngle, 0f, 0f);
            var normal = rotation * Vector3.up;
            var slope = CreatePrimitive("Hillside", PrimitiveType.Cube, root,
                -normal * 0.5f, new Vector3(60f, 1f, 60f),
                GeneratedMaterials.GetTextured("Hillside", new Color(0.095f, 0.11f, 0.14f), "dry_ground_rocks", 20f));
            slope.transform.rotation = rotation;

            var colors = new[]
            {
                new Color(0.46f, 0.30f, 0.18f),
                new Color(0.28f, 0.39f, 0.23f),
                new Color(0.44f, 0.48f, 0.52f),
                new Color(0.65f, 0.60f, 0.49f),
                new Color(0.64f, 0.37f, 0.13f),
                new Color(0.30f, 0.13f, 0.085f)
            };
            var textures = new[]
            {
                "dry_ground_rocks",
                "dry_ground_rocks",
                "rock_boulder_dry",
                "gravel_ground_01",
                "dry_decay_leaves",
                "burned_ground_01"
            };
            var types = (GroundType[])Enum.GetValues(typeof(GroundType));
            if (colors.Length != types.Length || textures.Length != types.Length)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} Cada GroundType necesita su color, su textura y su lugar en la zona de prueba.");
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                float x = (i % 3 - 1) * 15f;
                float alongSlope = i / 3 == 0 ? -9f : 9f;
                var top = SurfacePoint(x, alongSlope) + normal * PatchElevation;
                var patch = CreatePrimitive($"Ground {type}", PrimitiveType.Cube, root,
                    top - normal * (PatchHeight * 0.5f), new Vector3(10f, PatchHeight, 10f),
                    GeneratedMaterials.GetTextured($"Ground{type}", colors[i], textures[i], 3.5f));
                patch.transform.rotation = rotation;
                patch.AddComponent<GroundSurface>().type = type;

                var stop = new GameObject($"Stop {i + 1} {type}");
                stop.transform.SetParent(root, false);
                stop.transform.position = top;
                stop.AddComponent<CaptureStop>().overrideCamera = false;
            }

            CreateBorder("West Border", root, SurfacePoint(-30f, 0f) + normal,
                rotation, new Vector3(0.5f, 2f, 60.5f));
            CreateBorder("East Border", root, SurfacePoint(30f, 0f) + normal,
                rotation, new Vector3(0.5f, 2f, 60.5f));
            CreateBorder("Lower Border", root, SurfacePoint(0f, -30f) + normal,
                rotation, new Vector3(60.5f, 2f, 0.5f));
            CreateBorder("Upper Border", root, SurfacePoint(0f, 30f) + normal,
                rotation, new Vector3(60.5f, 2f, 0.5f));

            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(root, false);
            spawn.position = SurfacePoint(0f, 0f);
            return spawn;
        }

        static void BakeNavMesh(Transform zoneRoot)
        {
            var surface = zoneRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            surface.navMeshData.name = "WitchNavMesh";
            const string path = "Assets/Scenes/Generated/WitchNavMesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(surface.navMeshData, existing);
                surface.navMeshData = existing;
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(surface.navMeshData, path);
            }
        }

        static WitchController BuildWitch(Transform zoneRoot, Transform player)
        {
            BakeNavMesh(zoneRoot);
            var route = new[]
            {
                SurfacePoint(-20f, -20f) + Vector3.up * 14f,
                SurfacePoint(20f, -20f) + Vector3.up * 14f,
                SurfacePoint(20f, 20f) + Vector3.up * 14f,
                SurfacePoint(-20f, 20f) + Vector3.up * 14f
            };
            var witch = new GameObject("Witch");
            witch.transform.SetParent(zoneRoot, false);
            witch.transform.position = route[0];
            var agent = witch.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 2.3f;
            agent.enabled = false;
            var controller = witch.AddComponent<WitchController>();
            controller.player = player;
            controller.skyRoute = route;
            controller.chapter = Chapter.Ravine;
            controller.figure = WitchBuilder.BuildFigure(witch.transform, out var stand, out var crouch);
            controller.figureStand = stand;
            controller.figureCrouch = crouch;
            controller.fireLight = WitchBuilder.BuildFireBall(witch.transform, out var ball);
            ball.transform.localPosition = Vector3.up * 1.5f;
            controller.fireBall = ball;

            var liveStop = new GameObject("Witch 0 Live");
            liveStop.transform.SetParent(zoneRoot, false);
            liveStop.transform.position = SurfacePoint(0f, -4f);
            liveStop.AddComponent<CaptureStop>().wait = 6f;
            liveStop.AddComponent<WitchCaptureStage>().live = true;

            var states = new[] { WitchState.Flying, WitchState.Landing, WitchState.Stalking, WitchState.WindUp,
                WitchState.Sprinting, WitchState.Recovering, WitchState.TakingOff };
            for (int i = 0; i < states.Length; i++)
            {
                var stop = new GameObject($"Witch {i + 1} {states[i]}");
                stop.transform.SetParent(zoneRoot, false);
                stop.transform.position = SurfacePoint(0f, -4f);
                var capture = stop.AddComponent<CaptureStop>();
                capture.overrideCamera = states[i] == WitchState.Flying;
                if (capture.overrideCamera)
                {
                    capture.cameraPosition = SurfacePoint(0f, -4f) + new Vector3(0f, 8f, -34f);
                    capture.cameraRotation = Quaternion.LookRotation(Vector3.forward);
                }
                capture.wait = 1.5f;
                var stage = stop.AddComponent<WitchCaptureStage>();
                stage.state = states[i];
                stage.fireLit = stage.state == WitchState.Flying || stage.state == WitchState.TakingOff;
                stage.witchOffset = stage.state == WitchState.Flying ? new Vector3(0f, 14f, 6f)
                    : stage.state == WitchState.TakingOff ? new Vector3(2f, 3f, 5f)
                    : stage.state == WitchState.Landing ? new Vector3(2f, 2.5f, 6f) : new Vector3(1.5f, 0f, 3f);
            }
            return controller;
        }

        static CandleSpot BuildCandles(Transform zoneRoot, PlayerSoul soul, Storm storm)
        {
            CandleSpot first = null;
            var glassMaterial = GeneratedMaterials.Get("CandleGlass", new Color(0.5f, 0.16f, 0.07f), smoothness: 0.8f);
            var flameMaterial = GeneratedMaterials.Get("CandleFlame", new Color(1f, 0.78f, 0.3f), unlit: true);
            for (int i = 0; i < 2; i++)
            {
                var root = new GameObject($"Family Candle {i + 1}");
                root.transform.SetParent(zoneRoot, false);
                root.transform.position = SurfacePoint(i == 0 ? -5f : 5f, 4f);
                var obstacle = root.AddComponent<NavMeshObstacle>();
                var spot = root.AddComponent<CandleSpot>();
                if (i == 0) first = spot;
                spot.kind = CandleKind.Family;
                spot.player = soul;
                spot.storm = storm;
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.radius = spot.radius;
                obstacle.height = 2f;
                obstacle.center = Vector3.up;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                CreatePrimitive("Glass", PrimitiveType.Cylinder, root.transform,
                    Vector3.up * 0.11f, new Vector3(0.14f, 0.11f, 0.14f), glassMaterial);
                var flame = CreatePrimitive("Flame", PrimitiveType.Sphere, root.transform,
                    Vector3.up * 0.26f, new Vector3(0.06f, 0.1f, 0.06f), flameMaterial);
                UnityEngine.Object.DestroyImmediate(flame.GetComponent<SphereCollider>());
                var light = new GameObject("Candle Light").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.transform.localPosition = Vector3.up * 0.35f;
                light.type = LightType.Point;
                light.color = new Color(1f, 0.62f, 0.3f);
                light.range = 5f;
                light.intensity = 4f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.Auto;
                spot.flame = light;
            }
            return first;
        }

        static void BuildLightStops(Transform zoneRoot)
        {
            var names = new[] { "Light 1 On", "Light 2 Low", "Light 3 Flash" };
            for (int i = 0; i < names.Length; i++)
            {
                var stop = new GameObject(names[i]);
                stop.transform.SetParent(zoneRoot, false);
                stop.transform.SetPositionAndRotation(SurfacePoint(0f, 0f), Quaternion.identity);
                var capture = stop.AddComponent<CaptureStop>();
                capture.overrideCamera = false;
                capture.wait = 1.5f;
                var stage = stop.AddComponent<LightCaptureStage>();
                stage.energy = i == 1 ? 8f : SoulEnergy.Max;
                stage.flash = i == 2;
            }
        }

        static Vector3 SurfacePoint(float x, float alongSlope)
        {
            float angle = SlopeAngle * Mathf.Deg2Rad;
            float z = alongSlope * Mathf.Cos(angle);
            return new Vector3(x, z * Mathf.Tan(angle), z);
        }

        static void CreateBorder(string name, Transform parent, Vector3 position,
            Quaternion rotation, Vector3 size)
        {
            var border = new GameObject(name);
            border.transform.SetParent(parent, false);
            border.transform.SetPositionAndRotation(position, rotation);
            border.AddComponent<BoxCollider>().size = size;
        }

        static PlayerController CreatePlayer(Transform spawn, out PlayerLight playerLight)
        {
            var player = new GameObject("Tiffany");
            player.tag = "Player";
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.2f;
            controller.radius = 0.25f;
            controller.center = Vector3.up * (controller.height * 0.5f);

            var body = CreatePrimitive("Body", PrimitiveType.Capsule, player.transform,
                new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.6f, 0.5f),
                GeneratedMaterials.Get("TiffanyBody", new Color(0.86f, 0.86f, 0.72f)));
            UnityEngine.Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
            var facing = CreatePrimitive("Facing", PrimitiveType.Cube, player.transform,
                new Vector3(0f, 0.97f, 0.26f), new Vector3(0.18f, 0.14f, 0.18f),
                GeneratedMaterials.Get("TiffanyFacing", new Color(0.8f, 0.43f, 0.16f)));
            UnityEngine.Object.DestroyImmediate(facing.GetComponent<BoxCollider>());
            player.AddComponent<PlayerSoul>().startingPowers = new[] { Power.Light };
            var spot = new GameObject("Soul Light").AddComponent<Light>();
            spot.transform.SetParent(player.transform, false);
            spot.transform.localPosition = new Vector3(0f, 3.2f, -0.4f);
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.78f, 0.52f);
            spot.spotAngle = 84f;
            spot.innerSpotAngle = 56f;
            spot.range = 13f;
            spot.intensity = 45f;
            spot.shadows = LightShadows.Soft;
            spot.renderMode = LightRenderMode.ForcePixel;
            var glow = CreatePrimitive("Hand Glow", PrimitiveType.Sphere, player.transform,
                new Vector3(0f, 0.72f, 0.3f), Vector3.one * 0.16f,
                GeneratedMaterials.Get("TiffanyHandGlow", new Color(1f, 0.9f, 0.65f), true));
            UnityEngine.Object.DestroyImmediate(glow.GetComponent<SphereCollider>());
            var glowRenderer = glow.GetComponent<Renderer>();
            glowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;
            playerLight = player.AddComponent<PlayerLight>();
            playerLight.spot = spot;
            playerLight.glow = glow.transform;
            playerLight.startOn = false;
            playerLight.pitch = 42f;
            return player.AddComponent<PlayerController>();
        }

        static void CreateCamera(PlayerController player)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.farClipPlane = 80f;
            cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<FollowCamera>();
            follow.target = player.transform;
            camera.fieldOfView = follow.fieldOfView;
            follow.Snap();
            player.viewCamera = camera;
        }

        static GameInterface BuildInterface(out Image caughtFade)
        {
            var root = new GameObject("Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var ui = root.AddComponent<GameInterface>();

            caughtFade = InterfacePanel("Caught Fade", root.transform, 1f).GetComponent<Image>();
            caughtFade.gameObject.SetActive(false);
            ui.noticeText = InterfaceText("Power Notice", root.transform, font, "", 42,
                new Vector2(0.12f, 0.65f), new Vector2(0.88f, 0.74f));
            ui.noticeText.color = new Color(1f, 0.87f, 0.63f);
            ui.noticeText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            ui.subtitleText = InterfaceText("Subtitle", root.transform, font, "", 38,
                new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.18f));
            ui.subtitleText.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);

            ui.pausePanel = InterfacePanel("Pause", root.transform, 0.6f);
            InterfaceText("Title", ui.pausePanel.transform, font, InterfaceTexts.PauseTitle, 76,
                new Vector2(0.1f, 0.47f), new Vector2(0.9f, 0.60f));
            InterfaceText("Hint", ui.pausePanel.transform, font, InterfaceTexts.PauseHint, 30,
                new Vector2(0.1f, 0.37f), new Vector2(0.9f, 0.46f));

            ui.notebookPanel = InterfacePanel("Notebook", root.transform, 0.92f);
            var notebook = ui.notebookPanel.transform;
            var frame = InterfaceRect("Drawing Frame", notebook,
                new Vector2(0.08f, 0.23f), new Vector2(0.92f, 0.84f));
            var drawing = InterfaceRect("Drawing", frame, Vector2.zero, Vector2.one);
            ui.drawingImage = drawing.gameObject.AddComponent<RawImage>();
            ui.drawingImage.raycastTarget = false;
            ui.drawingFitter = drawing.gameObject.AddComponent<AspectRatioFitter>();
            ui.drawingFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            ui.drawingFitter.aspectRatio = 4f / 3f;
            ui.notebookTitle = InterfaceText("Title", notebook, font, "", 48,
                new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.96f));
            ui.notebookPhrase = InterfaceText("Phrase", notebook, font, "", 40,
                new Vector2(0.10f, 0.11f), new Vector2(0.90f, 0.21f));
            ui.notebookPage = InterfaceText("Page", notebook, font, "", 28,
                new Vector2(0.84f, 0.025f), new Vector2(0.97f, 0.085f));
            ui.notebookPage.alignment = TextAnchor.MiddleRight;
            InterfaceText("Hint", notebook, font, InterfaceTexts.NotebookHint, 26,
                new Vector2(0.15f, 0.025f), new Vector2(0.85f, 0.085f));
            ui.notebookEmpty = InterfaceText("Empty", notebook, font, InterfaceTexts.NotebookEmpty, 44,
                new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.6f));

            ui.memoryAssets = LoadInterfaceAssets<TextAsset>("t:TextAsset", "Assets/Data/Memories");
            ui.drawings = LoadInterfaceAssets<Texture2D>("t:Texture2D", "Assets/Art/Memories");
            ui.pausePanel.SetActive(false);
            ui.notebookPanel.SetActive(false);
            return ui;
        }

        static T[] LoadInterfaceAssets<T>(string filter, string folder) where T : UnityEngine.Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) return Array.Empty<T>();
            var paths = AssetDatabase.FindAssets(filter, new[] { folder });
            for (int i = 0; i < paths.Length; i++) paths[i] = AssetDatabase.GUIDToAssetPath(paths[i]);
            if (typeof(T) == typeof(TextAsset))
                paths = Array.FindAll(paths, path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            Array.Sort(paths, StringComparer.Ordinal);
            var assets = new T[paths.Length];
            for (int i = 0; i < paths.Length; i++)
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(paths[i]);
            return assets;
        }

        static RectTransform InterfaceRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static GameObject InterfacePanel(string name, Transform parent, float alpha)
        {
            var rect = InterfaceRect(name, parent, Vector2.zero, Vector2.one);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, alpha);
            image.raycastTarget = false;
            return rect.gameObject;
        }

        static Text InterfaceText(string name, Transform parent, Font font, string value, int size,
            Vector2 min, Vector2 max)
        {
            var text = InterfaceRect(name, parent, min, max).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localScale = scale;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            return instance;
        }

        static void SaveScene()
        {
            if (!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath))
                throw new InvalidOperationException($"{GameInfo.LogPrefix} No se pudo guardar la escena en {ScenePath}.");
        }
    }
}
