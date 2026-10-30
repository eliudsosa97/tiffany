using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static TiffanyGame.EditorTools.AudioBuilder;

namespace TiffanyGame.EditorTools
{
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const float FireBallHeight = 1.5f;
        public const float NightFogStartDistance = NightView.FogStartDistance;
        public const float NightFogEndDistance = NightView.FogEndDistance;
        public static readonly Color NightFogColor = NightLook.Default.FogColor;
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
            var world = new GameObject("World").transform;
            var spawn = BuildTestZone();
            var testZone = spawn.parent;
            testZone.SetParent(world, false);
            BuildForestSample(testZone);
            var player = CreatePlayer(spawn, out var playerLight);
            var storm = StormBuilder.BuildStorm(player.transform, spawn);
            var firstCandle = BuildCandles(testZone, player.GetComponent<PlayerSoul>(), storm);
            var gameInterface = BuildInterface(out var caughtFade);
            gameInterface.soul = player.GetComponent<PlayerSoul>();
            BuildLightStops(testZone);
            HidingBuilder.BuildHideTest(testZone, player);
            var route = RouteBuilder.BuildRoute(world, player, storm, out var memories, out var skyRoute);
            PosterBuilder.Build(route);
            FogBuilder.Build(world, route.GetComponent<WitchChapterTracker>());
            var viewCamera = CreateCamera(player);
            BakeNavMesh(world);
            playerLight.witch = BuildWitch(route.GetChild(0), player.transform, testZone, skyRoute,
                viewCamera.GetComponent<FollowCamera>());
            ApparitionBuilder.BuildApparitions(route, player.transform, playerLight.witch, playerLight);
            route.GetComponent<WitchChapterTracker>().witch = playerLight.witch;
            storm.witch = playerLight.witch;
            var respawn = player.gameObject.AddComponent<PlayerRespawn>();
            respawn.witch = playerLight.witch;
            respawn.overlay = caughtFade;
            gameInterface.playerRespawn = respawn;
            BuildEnding(route.GetChild(7), player.GetComponent<PlayerSoul>(), playerLight.witch, gameInterface);
            var tyler = BuildTyler(route.GetChild(0), player.transform, testZone, memories[0]);
            var ball = route.GetChild(0).Find("Ball");
            tyler.transform.position = ball.position + new Vector3(1.5f, -0.2f, 0f);
            MemoryBuilder.Build(world, player, gameInterface, tyler, viewCamera, memories, ball: ball);
            gameInterface.route = route.GetComponent<WitchChapterTracker>();
            gameInterface.memories = player.GetComponent<MemoryCollector>();
            PanicBuilder.BuildPanic(route.GetChild(0).Find("Spawn"), playerLight, tyler);
            var ambience = BuildForestAmbience(player.transform, route, playerLight.witch);
            ambience.music = BuildTensionMusic(playerLight.witch, player.transform);
            ambience.panic.music = ambience.music;
            BuildBackgroundMusic(gameInterface);
            var heartbeat = player.gameObject.AddComponent<Heartbeat>();
            heartbeat.panic = player.GetComponent<PanicController>();
            heartbeat.witch = playerLight.witch;
            heartbeat.source = player.gameObject.AddComponent<AudioSource>();
            heartbeat.source.playOnAwake = false;
            heartbeat.source.spatialBlend = 0f;
            const string heartbeatPath = "Assets/Audio/Sfx/heartbeat.wav";
            heartbeat.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(heartbeatPath);
            if (heartbeat.clip == null)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} No se encontró el audio de latidos: {heartbeatPath}");
            var candleStop = new GameObject("Candle 1 Safe");
            candleStop.transform.SetParent(testZone, false);
            candleStop.transform.position = SurfacePoint(-5f, 1.5f);
            var capture = candleStop.AddComponent<CaptureStop>();
            capture.overrideCamera = false;
            capture.wait = 1.5f;
            candleStop.AddComponent<LightCaptureStage>().energy = SoulEnergy.Max;
            PowerBuilder.BuildPowers(playerLight, route.GetChild(0), storm);
            var caughtStop = new GameObject("Witch 8 Caught");
            caughtStop.transform.SetParent(spawn.parent, false);
            caughtStop.transform.position = spawn.position;
            var caughtCapture = caughtStop.AddComponent<CaptureStop>();
            caughtCapture.overrideCamera = false;
            caughtCapture.wait = CaughtSequence.FireDuration + CaughtSequence.FadeOutDuration + CaughtSequence.FadeInDuration * 0.5f;
            caughtStop.AddComponent<CaughtCaptureStage>().spot = firstCandle;
            BuildWitchVoice(playerLight.witch, tyler);
            BuildWitchAudio(playerLight.witch, player.transform, viewCamera);
            var save = player.gameObject.AddComponent<SaveGame>();
            save.soul = player.GetComponent<PlayerSoul>();
            save.memories = player.GetComponent<MemoryCollector>();
            save.witch = playerLight.witch;
            save.route = route.GetComponent<WitchChapterTracker>();
            save.gameInterface = gameInterface;
            save.ending = route.GetChild(7).GetComponentInChildren<EndingController>();
            save.candles = Array.FindAll(world.GetComponentsInChildren<CandleSpot>(true), candle => candle.kind == CandleKind.Family);
            save.initialCandle = route.GetChild(0).GetComponentInChildren<CandleSpot>();
            var trailTension = BuildTrailTension(route.GetComponent<WitchChapterTracker>(), playerLight.witch, tyler);
            BuildUnseenForest(route.GetComponent<WitchChapterTracker>(), playerLight.witch, trailTension, viewCamera.transform);
            GroundLitterBuilder.Scatter(world);
            CharacterSilhouetteBuilder.Add(player.transform);
            CharacterSilhouetteBuilder.Add(tyler.figure.transform);
            OcclusionCaptureBuilder.Build(world);
            OcclusionSceneAudit.Log(world, player.transform, tyler.figure.transform);
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
            RenderSettings.ambientLight = NightLook.Default.AmbientColor;
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = NightFogColor;
            RenderSettings.fogStartDistance = NightFogStartDistance;
            RenderSettings.fogEndDistance = NightFogEndDistance;
        }

        static void CreateMoon()
        {
            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.7f, 0.8f, 1f);
            moon.intensity = NightLook.Default.MoonIntensity;
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
                stop.AddComponent<WitchAwayStage>();
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

        public static void BuildForestSample(Transform zoneRoot)
        {
            int coverLayer = LayerMask.NameToLayer(ForestLayout.CoverLayer);
            if (coverLayer < 0)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta la capa {ForestLayout.CoverLayer}.");
            foreach (var placement in ForestLayout.Placements)
            {
                var piece = placement.IsKenney
                    ? ModelLibrary.InstantiateKenney(placement.id, zoneRoot, placement.height)
                    : ModelLibrary.Instantiate(placement.id, zoneRoot);
                if (placement.IsKenney)
                {
                    var bounds = ModelLibrary.LocalBounds(piece.transform);
                    if (placement.IsPine)
                    {
                        float canopyBottom = bounds.size.y * 0.4f;
                        var trunk = piece.AddComponent<CapsuleCollider>();
                        trunk.direction = 1;
                        trunk.radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.12f;
                        trunk.height = canopyBottom + trunk.radius;
                        trunk.center = Vector3.up * (trunk.height * 0.5f);
                        var canopy = new GameObject("Canopy");
                        canopy.transform.SetParent(piece.transform, false);
                        canopy.layer = coverLayer;
                        var cover = canopy.AddComponent<BoxCollider>();
                        cover.center = Vector3.up * ((bounds.max.y + canopyBottom) * 0.5f);
                        cover.size = new Vector3(bounds.size.x, bounds.max.y - canopyBottom, bounds.size.z);
                    }
                    else
                    {
                        var solid = piece.AddComponent<BoxCollider>();
                        solid.center = bounds.center;
                        solid.size = bounds.size;
                    }
                }
                piece.transform.localPosition = SurfacePoint(placement.x, placement.alongSlope);
                piece.transform.localRotation = Quaternion.Euler(0f, placement.yaw, 0f);
                piece.transform.localScale = Vector3.one * placement.scale;
                if (placement.IsKenney) continue;
                var spec = ModelLibrary.LoadSpec(placement.id);
                foreach (var filter in piece.GetComponentsInChildren<MeshFilter>())
                {
                    var bounds = filter.sharedMesh.bounds;
                    switch (filter.name)
                    {
                        case "Trunk":
                            var trunk = filter.gameObject.AddComponent<CapsuleCollider>();
                            trunk.direction = 1;
                            trunk.center = spec.trunkCollider.center;
                            trunk.radius = spec.trunkCollider.radius;
                            trunk.height = spec.trunkCollider.height;
                            break;
                        case "Canopy":
                            filter.gameObject.layer = coverLayer;
                            var canopy = filter.gameObject.AddComponent<BoxCollider>();
                            canopy.center = bounds.center;
                            canopy.size = bounds.size;
                            break;
                        case "Bush":
                            var hide = new GameObject("HideVolume");
                            hide.transform.SetParent(filter.transform, false);
                            hide.layer = LayerMask.NameToLayer("Ignore Raycast");
                            var volume = hide.AddComponent<BoxCollider>();
                            volume.isTrigger = true;
                            volume.center = spec.hideVolume.center;
                            volume.size = spec.hideVolume.size;
                            break;
                    }
                }
            }
        }

        public static void BakeNavMesh(Transform zoneRoot, string path = "Assets/Scenes/Generated/WitchNavMesh.asset")
        {
            var surface = zoneRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            surface.layerMask = ~LayerMask.GetMask(ForestLayout.CoverLayer, "Ignore Raycast");
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            surface.navMeshData.name = "WitchNavMesh";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing != null)
            {
                surface.RemoveData();
                var generated = surface.navMeshData;
                EditorUtility.CopySerialized(surface.navMeshData, existing);
                surface.navMeshData = existing;
                UnityEngine.Object.DestroyImmediate(generated);
                surface.AddData();
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(surface.navMeshData, path);
            }
        }

        public static WitchController BuildWitch(Transform zoneRoot, Transform player, Transform captureRoot, Vector3[] route,
            FollowCamera followCamera)
        {
            var witch = new GameObject("Witch");
            witch.transform.SetParent(zoneRoot, false);
            witch.transform.position = route[0] + Vector3.up * WitchProfiles.For(Chapter.Cemetery).FlightHeightOffset;
            var agent = witch.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 2.3f;
            agent.enabled = false;
            var controller = witch.AddComponent<WitchController>();
            controller.groundMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask(ForestLayout.CoverLayer);
            controller.player = player;
            controller.skyRoute = route;
            controller.chapter = Chapter.Cemetery;
            controller.figure = WitchBuilder.BuildFigure(witch.transform);
            controller.fireLight = WitchBuilder.BuildFireBall(witch.transform, out var ball);
            ball.transform.localPosition = Vector3.up * FireBallHeight;
            controller.fireBall = ball;
            var viewDirection = Quaternion.Euler(0f, followCamera.yaw, 0f) * Vector3.forward;
            var flyingOffset = Vector3.forward * followCamera.lookAhead - viewDirection * 2f
                + Vector3.up * RouteBuilder.SkyHeight;

            var liveStop = new GameObject("Witch 0 Live");
            liveStop.transform.SetParent(captureRoot, false);
            liveStop.transform.position = SurfacePoint(0f, -4f);
            liveStop.AddComponent<CaptureStop>().wait = 6f;
            liveStop.AddComponent<WitchCaptureStage>().live = true;

            var states = new[] { WitchState.Flying, WitchState.Landing, WitchState.Stalking, WitchState.WindUp,
                WitchState.Sprinting, WitchState.Recovering, WitchState.TakingOff, WitchState.Suspicious };
            for (int i = 0; i < states.Length; i++)
            {
                var stop = new GameObject(states[i] == WitchState.Suspicious ? "Witch Suspicious" : $"Witch {i + 1} {states[i]}");
                stop.transform.SetParent(captureRoot, false);
                stop.transform.position = SurfacePoint(0f, -4f);
                var capture = stop.AddComponent<CaptureStop>();
                capture.overrideCamera = false;
                capture.wait = 1.5f;
                var stage = stop.AddComponent<WitchCaptureStage>();
                stage.state = states[i];
                stage.fireLit = stage.state == WitchState.Flying || stage.state == WitchState.TakingOff;
                stage.witchOffset = stage.state == WitchState.Flying ? flyingOffset
                    : stage.state == WitchState.TakingOff ? new Vector3(2f, 3f, 5f)
                    : stage.state == WitchState.Landing ? new Vector3(2f, 2.5f, 6f) : new Vector3(1.5f, 0f, 3f);
            }

            var litStop = new GameObject("Witch 9 Flying Lit");
            litStop.transform.SetParent(captureRoot, false);
            litStop.transform.position = SurfacePoint(-5f, 1.5f);
            var litCapture = litStop.AddComponent<CaptureStop>();
            litCapture.overrideCamera = false;
            litCapture.wait = 1.5f;
            var litWitch = litStop.AddComponent<WitchCaptureStage>();
            litWitch.state = WitchState.Flying;
            litWitch.fireLit = true;
            litWitch.witchOffset = flyingOffset;
            var litLight = litStop.AddComponent<LightCaptureStage>();
            litLight.hideWitch = false;
            return controller;
        }

        static void BuildWitchVoice(WitchController witch, TylerController tyler)
        {
            var voice = witch.gameObject.AddComponent<WitchVoice>();
            voice.witch = witch;
            voice.tyler = tyler;
            voice.source = witch.gameObject.AddComponent<AudioSource>();
            voice.echo = witch.gameObject.AddComponent<AudioSource>();
            foreach (var source in new[] { voice.source, voice.echo })
            {
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                // El AudioListener va en la cámara, a unos 36 m de Tiffany.
                source.minDistance = 30f;
                source.maxDistance = 71f;
            }
            voice.barks = new AudioClip[4];
            for (int i = 0; i < voice.barks.Length; i++)
            {
                string path = $"Assets/Audio/Sfx/bark_{i + 1}.wav";
                voice.barks[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (voice.barks[i] == null)
                    throw new InvalidOperationException($"{GameInfo.LogPrefix} No se encontró el ladrido: {path}");
            }
        }

        static void BuildWitchAudio(WitchController witch, Transform player, Camera view)
        {
            var audio = witch.gameObject.AddComponent<WitchAudio>();
            audio.witch = witch;
            audio.player = player;
            audio.fireLoop = witch.gameObject.AddComponent<AudioSource>();
            audio.breathLoop = witch.gameObject.AddComponent<AudioSource>();
            audio.oneShot = witch.gameObject.AddComponent<AudioSource>();
            foreach (var source in new[] { audio.fireLoop, audio.breathLoop, audio.oneShot })
            {
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            audio.fireLoop.loop = audio.breathLoop.loop = true;
            audio.fireLoop.clip = LoadClip("fireball");
            audio.breathLoop.clip = LoadClip("witch_breath");
            (audio.land, audio.crack, audio.whoosh) = (LoadClip("witch_land"), LoadClip("witch_crack"), LoadClip("witch_whoosh"));
            audio.steps = new[] { LoadClip("monster_step_1"), LoadClip("monster_step_2"), LoadClip("monster_step_3"), LoadClip("monster_step_4") };
            audio.cackles = new[] { LoadClip("cackle_1"), LoadClip("cackle_2"), LoadClip("cackle_3") };
            var presence = witch.gameObject.AddComponent<WitchPresence>();
            (presence.witch, presence.player, presence.view) = (witch, player, view);
            presence.source = witch.gameObject.AddComponent<AudioSource>();
            (presence.source.playOnAwake, presence.source.loop, presence.source.volume) = (false, true, 0f);
            presence.source.spatialBlend = 0.9f;
            presence.source.rolloffMode = AudioRolloffMode.Linear;
            (presence.source.minDistance, presence.source.maxDistance, presence.source.dopplerLevel) = (36f, 80f, 0f);
            presence.source.clip = LoadClip("witch_murmur");
        }

        static TensionMusic BuildTensionMusic(WitchController witch, Transform player)
        {
            var root = new GameObject("Tension Music");
            var music = root.AddComponent<TensionMusic>();
            (music.witch, music.player, music.hiding) = (witch, player, player.GetComponent<PlayerHiding>());
            music.drone = root.AddComponent<AudioSource>();
            music.pulse = root.AddComponent<AudioSource>();
            foreach (var source in new[] { music.drone, music.pulse })
                (source.loop, source.playOnAwake, source.spatialBlend, source.volume) = (true, false, 0f, 0f);
            (music.drone.clip, music.pulse.clip) = (LoadClip("music_stalk"), LoadClip("music_chase"));
            return music;
        }

        static BackgroundMusic BuildBackgroundMusic(GameInterface gameInterface)
        {
            var root = new GameObject("Background Music");
            var music = root.AddComponent<BackgroundMusic>();
            music.gameInterface = gameInterface;
            music.source = root.AddComponent<AudioSource>();
            (music.source.loop, music.source.playOnAwake, music.source.spatialBlend, music.source.volume) = (true, false, 0f, 0f);
            music.source.ignoreListenerPause = true;
            music.theme = music.finale = LoadClip("music_theme");
            return music;
        }

        static ForestAmbience BuildForestAmbience(Transform player, Transform route, WitchController witch)
        {
            var root = new GameObject("Forest Ambience");
            var audio = root.AddComponent<ForestAmbience>();
            (audio.player, audio.witch, audio.panic) = (player, witch, player.GetComponent<PanicController>());
            audio.clearings = route.GetComponent<WitchChapterTracker>().clearings;
            audio.loopClips = new[] { LoadClip("crickets"), LoadClip("wind"), LoadClip("leaves"), LoadClip("frogs"), LoadClip("lake_water") };
            audio.loops = new AudioSource[AmbienceMix.LayerCount];
            for (int i = 0; i < audio.loops.Length; i++)
            {
                var source = root.AddComponent<AudioSource>();
                (source.loop, source.playOnAwake, source.spatialBlend, source.clip) = (true, false, 0f, audio.loopClips[i]);
                audio.loops[i] = source;
            }
            audio.calls = root.AddComponent<AudioSource>();
            (audio.calls.playOnAwake, audio.calls.spatialBlend) = (false, 0f);
            (audio.owl, audio.barnOwl, audio.coyote) = (LoadClip("owl"), LoadClip("lechuza"), LoadClip("coyote"));
            audio.howls = new[] { LoadClip("distant_howl_1"), LoadClip("distant_howl_2"), LoadClip("distant_howl_3") };
            return audio;
        }

        static TrailTension BuildTrailTension(WitchChapterTracker tracker, WitchController witch, TylerController tyler)
        {
            var root = new GameObject("Trail Tension");
            var tension = root.AddComponent<TrailTension>();
            tension.clearingRadius = RouteBuilder.TrailRadius;
            (tension.tracker, tension.witch, tension.tyler) = (tracker, witch, tyler);
            tension.challenges = tracker.GetComponentsInChildren<PowerChallenge>(true);
            tension.oneShot = root.AddComponent<AudioSource>();
            (tension.oneShot.playOnAwake, tension.oneShot.spatialBlend) = (false, 0f);
            tension.phantomSteps = new[] { LoadClip("monster_step_1"), LoadClip("monster_step_2"), LoadClip("monster_step_3"), LoadClip("monster_step_4") };
            tension.branchSnaps = new[] { LoadClip("branch_snap_1"), LoadClip("branch_snap_2"), LoadClip("branch_snap_3"), LoadClip("branch_snap_4") };
            tension.skyFire = LoadClip("fireball");
            var decoy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            decoy.name = "Sky Decoy";
            decoy.transform.SetParent(root.transform, false);
            decoy.transform.localScale = Vector3.one * 0.9f;
            UnityEngine.Object.DestroyImmediate(decoy.GetComponent<Collider>());
            var renderer = decoy.GetComponent<Renderer>();
            renderer.sharedMaterial = WitchBuilder.FireMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            decoy.SetActive(false);
            tension.skyDecoy = decoy.transform;
            return tension;
        }

        static void BuildUnseenForest(WitchChapterTracker tracker, WitchController witch, TrailTension trailTension, Transform view)
        {
            var root = new GameObject("Unseen Forest");
            var forest = root.AddComponent<UnseenForest>();
            (forest.tracker, forest.witch, forest.trailTension, forest.view) = (tracker, witch, trailTension, view);
            var child = new GameObject("Unseen Forest Source");
            child.transform.SetParent(root.transform, false);
            forest.source = child.AddComponent<AudioSource>();
            forest.source.playOnAwake = false;
            forest.source.spatialBlend = 0.85f;
            forest.source.rolloffMode = AudioRolloffMode.Linear;
            (forest.source.minDistance, forest.source.maxDistance, forest.source.dopplerLevel) = (36f, 80f, 0f);
            forest.branches = new[] { LoadClip("branch_snap_1"), LoadClip("branch_snap_2"), LoadClip("branch_snap_3"), LoadClip("branch_snap_4") };
            forest.steps = new[] { LoadClip("monster_step_1"), LoadClip("monster_step_2"), LoadClip("monster_step_3"), LoadClip("monster_step_4") };
            forest.whispers = new[] { LoadClip("whisper_1"), LoadClip("whisper_2"), LoadClip("whisper_3") };
            forest.knocks = new[] { LoadClip("forest_knock_1"), LoadClip("forest_knock_2"), LoadClip("forest_knock_3") };
            var names = new AudioClip[4];
            int count = 0;
            for (int i = 1; i <= names.Length; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Sfx/tiffany_whisper_{i}.wav");
                if (clip != null) names[count++] = clip;
            }
            Array.Resize(ref names, count);
            forest.names = names;
        }

        static CandleSpot BuildCandles(Transform zoneRoot, PlayerSoul soul, Storm storm)
        {
            CandleSpot first = null;
            for (int i = 0; i < 2; i++)
            {
                var spot = CreateCandle(zoneRoot, $"Family Candle {i + 1}", SurfacePoint(i == 0 ? -5f : 5f, 4f),
                    CandleKind.Family, true, soul, storm);
                if (i == 0) first = spot;
            }
            return first;
        }

        static void BuildEnding(Transform house, PlayerSoul soul, WitchController witch, GameInterface gameInterface)
        {
            var ending = house.Find("Window Candle").gameObject.AddComponent<EndingController>();
            ending.soul = soul;
            ending.witch = witch;
            ending.gameInterface = gameInterface;
            for (int i = 0; i < 2; i++)
            {
                var stop = new GameObject(i == 0 ? "Ending 1 Fade" : "Ending 2 Credits");
                stop.transform.SetParent(house, false);
                stop.transform.position = new Vector3(ending.transform.position.x, house.position.y, ending.transform.position.z - 1.5f);
                var capture = stop.AddComponent<CaptureStop>();
                capture.overrideCamera = false;
                capture.wait = 0.5f;
                var stage = stop.AddComponent<EndingCaptureStage>();
                stage.ending = ending;
                stage.credits = i == 1;
            }
        }

        internal static CandleSpot CreateCandle(Transform parent, string name, Vector3 position,
            CandleKind kind, bool startLit, PlayerSoul soul, Storm storm)
        {
            var glassMaterial = GeneratedMaterials.Get("CandleGlass", new Color(0.5f, 0.16f, 0.07f), smoothness: 0.8f);
            var flameMaterial = GeneratedMaterials.Get("CandleFlame", new Color(1f, 0.78f, 0.3f), unlit: true);
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var obstacle = root.AddComponent<NavMeshObstacle>();
            var spot = root.AddComponent<CandleSpot>();
            spot.kind = kind;
            if (kind == CandleKind.Family) spot.saveId = parent.name + "/" + name;
            spot.startLit = startLit;
            spot.player = soul;
            spot.storm = storm;
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = spot.radius;
            obstacle.height = 2f;
            obstacle.center = Vector3.up;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.enabled = startLit;
            CreatePrimitive("Glass", PrimitiveType.Cylinder, root.transform,
                Vector3.up * 0.11f, new Vector3(0.14f, 0.11f, 0.14f), glassMaterial);
            var flame = CreatePrimitive("Flame", PrimitiveType.Sphere, root.transform,
                Vector3.up * 0.26f, new Vector3(0.06f, 0.1f, 0.06f), flameMaterial);
            UnityEngine.Object.DestroyImmediate(flame.GetComponent<SphereCollider>());
            spot.flameMesh = flame.GetComponent<Renderer>();
            spot.flameMesh.enabled = startLit;
            var light = new GameObject("Candle Light").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = Vector3.up * 0.35f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.range = 5f;
            light.intensity = 4f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            light.enabled = startLit;
            spot.flame = light;
            return spot;
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

        static TylerController BuildTyler(Transform zoneRoot, Transform player, Transform captureRoot, Transform nextMemory)
        {
            var tyler = new GameObject("Tyler");
            tyler.transform.SetParent(zoneRoot, false);
            tyler.transform.position = player.position - player.forward * 1.5f;
            var agent = tyler.AddComponent<NavMeshAgent>();
            agent.radius = 0.25f;
            agent.height = 0.6f;
            agent.avoidancePriority = 99;
            agent.enabled = false;
            var controller = tyler.AddComponent<TylerController>();
            controller.player = player;
            controller.soul = player.GetComponent<PlayerSoul>();
            controller.shareLight = TylerBuilder.BuildShareLight(tyler.transform);
            controller.witch = zoneRoot.GetComponentInChildren<WitchController>();
            controller.nextMemory = nextMemory;
            controller.figure = TylerBuilder.BuildFigure(tyler.transform, out var head);
            controller.head = head;
            controller.animator = controller.figure.GetComponent<Animator>();
            controller.voice = tyler.AddComponent<AudioSource>();
            controller.voice.spatialBlend = 1f;
            controller.voice.rolloffMode = AudioRolloffMode.Linear;
            controller.voice.minDistance = 2f;
            controller.voice.maxDistance = 30f;
            controller.voice.dopplerLevel = 0f;
            controller.voice.playOnAwake = false;
            controller.barkClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/bark_1.wav");
            controller.growlClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/growl_1.wav");
            controller.whineClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/whine.wav");
            var icon = new GameObject("Reaction Icon", typeof(RectTransform), typeof(Canvas));
            icon.transform.SetParent(tyler.transform, false);
            icon.transform.localPosition = head.position - tyler.transform.position + Vector3.up * 0.6f;
            icon.transform.localScale = Vector3.one * 0.01f;
            icon.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
            label.transform.SetParent(icon.transform, false);
            label.rectTransform.sizeDelta = new Vector2(80f, 50f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 36;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.enabled = false;
            controller.reactionIcon = label;
            var names = new[] { "Tyler 1 Follow", "Tyler 2 Guide", "Tyler 3 Whine", "Tyler 4 Growl", "Tyler 5 Shares Light" };
            var reactions = new[] { TylerReaction.None, TylerReaction.Guide, TylerReaction.Whine, TylerReaction.Growl, TylerReaction.None };
            for (int i = 0; i < reactions.Length; i++)
            {
                var stop = new GameObject(names[i]);
                stop.transform.SetParent(captureRoot, false);
                stop.transform.position = i == 4 ? SurfacePoint(0f, 0f) : SurfacePoint(-6f, 4f);
                var capture = stop.AddComponent<CaptureStop>();
                var stage = stop.AddComponent<TylerCaptureStage>();
                stage.reaction = reactions[i];
                stage.lightTyler = i > 0 && i < 4;
                stage.shareLight = i == 4;
                if (stage.lightTyler)
                {
                    stage.fillLight = TylerBuilder.BuildShareLight(stop.transform);
                    stage.fillLight.name = "Tyler Capture Light";
                    stage.fillLight.intensity = 2f;
                }
                if (stage.shareLight) stop.AddComponent<LightCaptureStage>().energy = 0f;
                stage.witchOffset = reactions[i] == TylerReaction.Whine
                    ? new Vector3(0f, 12f, 16f) : new Vector3(0f, 0f, 10f);
                Vector3 position = stage.GetTylerPosition(stop.transform);
                Vector3 target = TylerPoses.For(stage.reaction).FaceWitch
                    ? stop.transform.position + stop.transform.rotation * stage.witchOffset
                    : stage.reaction == TylerReaction.Guide ? nextMemory.position : stop.transform.position;
                Vector3 direction = Vector3.ProjectOnPlane(target - position, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                Vector3 toPlayer = Vector3.ProjectOnPlane(stop.transform.position - position, Vector3.up);
                if (Vector3.Dot(side, toPlayer) > 0f) side = -side;
                Vector3 cameraOffset = (side - toPlayer.normalized * 0.35f).normalized * 3f;
                capture.overrideCamera = true;
                capture.cameraPosition = TylerCaptureStage.GroundPosition(position + cameraOffset) + Vector3.up * 1.2f;
                Vector3 focus = position + toPlayer * 0.25f + Vector3.up * 0.4f;
                capture.cameraRotation = Quaternion.LookRotation(focus - capture.cameraPosition);
            }
            tyler.SetActive(false);
            return controller;
        }

        internal static Vector3 SurfacePoint(float x, float alongSlope)
        {
            float angle = SlopeAngle * Mathf.Deg2Rad;
            float z = alongSlope * Mathf.Cos(angle);
            return new Vector3(x, z * Mathf.Tan(angle), z);
        }

        internal static void CreateBorder(string name, Transform parent, Vector3 position,
            Quaternion rotation, Vector3 size)
        {
            var border = new GameObject(name);
            border.transform.SetParent(parent, false);
            border.transform.SetPositionAndRotation(position, rotation);
            border.AddComponent<BoxCollider>().size = size;
        }

        public static PlayerController CreatePlayer(Transform spawn, out PlayerLight playerLight)
        {
            var player = new GameObject("Tiffany");
            player.tag = "Player";
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.2f;
            controller.radius = 0.25f;
            controller.center = Vector3.up * (controller.height * 0.5f);

            var body = ModelLibrary.Instantiate("tiffany-provisional", player.transform);
            body.transform.localPosition = controller.center - Vector3.up * (controller.height * 0.5f);
            player.AddComponent<PlayerSoul>().startingPowers = Array.Empty<Power>();
            var spot = new GameObject("Soul Light").AddComponent<Light>();
            spot.transform.SetParent(player.transform, false);
            spot.transform.localPosition = new Vector3(0f, 0.85f, 0.12f);
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.78f, 0.52f);
            spot.spotAngle = 84f;
            spot.innerSpotAngle = 56f;
            spot.range = 18f;
            spot.intensity = 140f;
            spot.shadows = LightShadows.Soft;
            spot.renderMode = LightRenderMode.ForcePixel;
            var baseGlow = new GameObject("Soul Glow").AddComponent<Light>();
            baseGlow.transform.SetParent(player.transform, false);
            baseGlow.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            baseGlow.type = LightType.Point;
            baseGlow.range = 2.5f;
            baseGlow.color = new Color(0.75f, 0.85f, 1f);
            baseGlow.intensity = 1.2f;
            baseGlow.shadows = LightShadows.None;
            baseGlow.renderMode = LightRenderMode.Auto;
            playerLight = player.AddComponent<PlayerLight>();
            playerLight.spot = spot;
            playerLight.baseGlow = baseGlow;
            playerLight.bodyRenderers = body.GetComponentsInChildren<Renderer>(true);
            playerLight.bodyGlowBoost = 1.2f;
            playerLight.startOn = false;
            playerLight.pitch = 7f;
            var movement = player.AddComponent<PlayerController>();
            var animation = player.AddComponent<TiffanyAnimator>();
            animation.animator = body.GetComponent<Animator>();
            animation.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var spec = ModelLibrary.LoadSpec("tiffany-provisional");
            bool hasLocomotionContacts = false;
            foreach (var clip in spec.clips)
            {
                if (clip.name == TiffanyAnimation.Flash) animation.flashDuration = clip.frames / (float)spec.fps;
                if (clip.contacts == null) continue;
                var contacts = new Vector2(clip.contacts.left, clip.contacts.right);
                if (clip.speed > 0f)
                {
                    if (hasLocomotionContacts && animation.locomotionContacts != contacts)
                        throw new System.IO.InvalidDataException("Los ciclos de locomoción deben compartir las fases de contacto.");
                    animation.locomotionContacts = contacts;
                    hasLocomotionContacts = true;
                }
                if (clip.name == TiffanyAnimation.TurnLeft)
                {
                    animation.leftTurnContacts = contacts;
                    animation.authoredTurnSpeeds.x = clip.turnSpeed;
                }
                if (clip.name == TiffanyAnimation.TurnRight)
                {
                    animation.rightTurnContacts = contacts;
                    animation.authoredTurnSpeeds.y = clip.turnSpeed;
                }
            }
            AudioBuilder.BuildFootsteps(movement);
            return movement;
        }

        static Camera CreateCamera(PlayerController player)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.farClipPlane = NightView.FarClipPlane;
            cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<FollowCamera>();
            follow.target = player.transform;
            follow.distance = NightView.CameraDistance;
            follow.lookAhead = 0f;
            camera.fieldOfView = follow.fieldOfView;
            follow.Snap();
            player.viewCamera = camera;
            return camera;
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
            var hud = root.AddComponent<GameplayHud>();
            hud.gameInterface = ui;
            hud.objectiveText = InterfaceText("Objective", root.transform, font, InterfaceTexts.GraveObjective, 26,
                new Vector2(0.1f, 0.91f), new Vector2(0.9f, 0.96f));
            hud.interactionText = InterfaceText("Interaction", root.transform, font, "", 26,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            hud.interactionText.rectTransform.sizeDelta = new Vector2(600f, 48f);
            hud.objectiveText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            hud.interactionText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            hud.objectiveText.gameObject.SetActive(false);
            hud.interactionText.gameObject.SetActive(false);

            caughtFade = InterfacePanel("Caught Fade", root.transform, 1f).GetComponent<Image>();
            caughtFade.gameObject.SetActive(false);
            ui.noticeText = InterfaceText("Power Notice", root.transform, font, "", 32,
                new Vector2(0.12f, 0.64f), new Vector2(0.88f, 0.80f));
            ui.noticeText.color = new Color(1f, 0.87f, 0.63f);
            ui.noticeText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            ui.subtitleText = InterfaceText("Subtitle", root.transform, font, "", 38,
                new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.18f));
            ui.subtitleText.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);

            ui.soulPanel = InterfaceRect("Soul Energy", root.transform,
                new Vector2(0.39f, 0.02f), new Vector2(0.61f, 0.06f)).gameObject;
            var track = InterfaceRect("Track", ui.soulPanel.transform, Vector2.zero, new Vector2(1f, 0.22f));
            track.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.06f, 0.04f, 0.9f);
            ui.soulFill = InterfaceRect("Fill", track, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            ui.soulFill.color = new Color(1f, 0.87f, 0.63f);
            ui.soulFill.raycastTarget = false;
            InterfaceText("Label", ui.soulPanel.transform, font, InterfaceTexts.SoulEnergy, 22,
                new Vector2(0f, 0.25f), Vector2.one).color = new Color(1f, 0.87f, 0.63f);
            ui.soulPanel.SetActive(false);

            ui.pausePanel = InterfacePanel("Pause", root.transform, 0.6f);
            InterfaceText("Title", ui.pausePanel.transform, font, InterfaceTexts.PauseTitle, 76,
                new Vector2(0.1f, 0.80f), new Vector2(0.9f, 0.94f));
            ui.pauseControls = InterfaceText("Controls", ui.pausePanel.transform, font, InterfaceTexts.Controls, 24,
                new Vector2(0.06f, 0.22f), new Vector2(0.49f, 0.78f));
            ui.pauseOptions = new[]
            {
                InterfaceText("Resume", ui.pausePanel.transform, font, InterfaceTexts.PauseResume, 32,
                    new Vector2(0.50f, 0.63f), new Vector2(0.97f, 0.73f)),
                InterfaceText("Respawn", ui.pausePanel.transform, font, InterfaceTexts.PauseRespawn, 32,
                    new Vector2(0.50f, 0.49f), new Vector2(0.97f, 0.59f)),
                InterfaceText("Fullscreen", ui.pausePanel.transform, font, InterfaceTexts.PauseFullscreen, 32,
                    new Vector2(0.50f, 0.35f), new Vector2(0.97f, 0.45f)),
                InterfaceText("Restart", ui.pausePanel.transform, font, InterfaceTexts.PauseRestart, 32,
                    new Vector2(0.50f, 0.21f), new Vector2(0.97f, 0.31f))
            };
            InterfaceText("Hint", ui.pausePanel.transform, font,
                InterfaceTexts.ControllerText(InterfaceTexts.PauseHint, GameInterface.HasControllerSymbols(font)), 24,
                new Vector2(0.06f, 0.07f), new Vector2(0.94f, 0.19f));

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

            ui.endingPanel = InterfacePanel("Ending", root.transform, 0f).GetComponent<Image>();
            ui.endingPanel.color = new Color(1f, 1f, 1f, 0f);
            ui.endingText = InterfaceText("Ending Text", ui.endingPanel.transform, font, "", 48,
                new Vector2(0.12f, 0.2f), new Vector2(0.88f, 0.8f));
            ui.endingText.color = new Color(0.12f, 0.12f, 0.12f);
            ui.endingPanel.gameObject.SetActive(false);

            ui.titlePanel = InterfacePanel("Title", root.transform, 0.9f);
            var title = InterfaceText("Title", ui.titlePanel.transform, font, InterfaceTexts.Title, 108,
                new Vector2(0.1f, 0.59f), new Vector2(0.9f, 0.79f));
            title.color = new Color(1f, 0.87f, 0.63f);
            ui.startHint = InterfaceText("Start Hint", ui.titlePanel.transform, font, InterfaceTexts.StartHint, 32,
                new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.57f));
            ui.continueOption = InterfaceText("Continue", ui.titlePanel.transform, font, InterfaceTexts.Continue, 32,
                new Vector2(0.3f, 0.49f), new Vector2(0.7f, 0.57f));
            ui.newGameOption = InterfaceText("New Game", ui.titlePanel.transform, font, InterfaceTexts.NewGame, 32,
                new Vector2(0.3f, 0.40f), new Vector2(0.7f, 0.48f));
            ui.continueOption.gameObject.SetActive(false);
            ui.newGameOption.gameObject.SetActive(false);
            var controls = InterfaceText("Controls", ui.titlePanel.transform, font, InterfaceTexts.Controls, 28,
                new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.39f));
            controls.lineSpacing = 1.25f;

            ui.storyPanel = InterfacePanel("Story", root.transform, 1f);
            ui.storyText = InterfaceText("Story Text", ui.storyPanel.transform, font, "", 42,
                new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.7f));
            InterfaceText("Hint", ui.storyPanel.transform, font, InterfaceTexts.StoryHint, 26,
                new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.15f));
            ui.storyPanel.SetActive(false);

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

        internal static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent,
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
