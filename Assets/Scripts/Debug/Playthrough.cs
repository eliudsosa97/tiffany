using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-900)]
    public sealed class Playthrough : MonoBehaviour
    {
        PlaythroughScene scene;
        PlaythroughNavigation navigation;
        bool quitOnFinish, reported;
        int selected = -1;
        string earlyError;
        static string startupError;
        public PlaythroughPlan Plan { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeSceneLoad()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            CaptureStartupErrors(Environment.GetCommandLineArgs());
#endif
        }

        public static void CaptureStartupErrors(string[] args)
        {
            Application.logMessageReceived -= ReceiveStartupLog;
            startupError = null;
            if (Array.IndexOf(args, "-playthrough") >= 0) Application.logMessageReceived += ReceiveStartupLog;
        }

        static void ReceiveStartupLog(string message, string stackTrace, LogType type)
        {
            if (startupError == null && (type == LogType.Error || type == LogType.Exception)) startupError = message;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-playthrough") < 0) return;
            float timeout = PlaythroughPlan.DefaultTimeout;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-playthrough-timeout" && float.TryParse(args[i + 1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value) && value > 0f && !float.IsInfinity(value)) timeout = value;
            new GameObject("Playthrough").AddComponent<Playthrough>().Configure(new PlaythroughPlan(timeout: timeout), true);
#endif
        }

        public void Configure(PlaythroughPlan plan, bool quit = false)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            quitOnFinish = quit;
            if (startupError != null) Plan.RecordConsoleError(startupError);
            if (earlyError != null) Plan.RecordConsoleError(earlyError);
            Application.logMessageReceived -= ReceiveStartupLog;
            startupError = null;
        }

        void Awake() => Application.logMessageReceived += ReceiveLog;

        IEnumerator Start()
        {
            NeutralInput();
            yield return null;
            var gameInterface = FindAnyObjectByType<GameInterface>();
            if (gameInterface != null) gameInterface.StartGame();
            if (Plan == null) Configure(new PlaythroughPlan());
            try
            {
                scene = new PlaythroughScene();
                scene.Prepare(Plan, LogMilestone);
                navigation = new PlaythroughNavigation();
            }
            catch (Exception exception) { FailException(exception); }
        }

        void Update()
        {
            if (reported || Plan == null || scene == null) return;
            try { Drive(); }
            catch (Exception exception) { FailException(exception); }
        }

        void Drive()
        {
            NeutralInput();
            if (Plan.Finished) { Report(); return; }
            bool targetChanged = selected != Plan.CompletedCount;
            if (targetChanged)
            {
                selected = Plan.CompletedCount;
                scene.Select(Plan.Current);
            }
            PlaythroughMilestone milestone = Plan.Current;
            var observation = scene.Observe();
            if (targetChanged && observation.MissingContent == null
                && PlaythroughPlan.NeedsNavigation(milestone) && !CalculatePath()) return;
            int missingCount = Plan.MissingContent.Count;
            var command = Plan.Tick(Time.unscaledDeltaTime, in observation);
            if (Plan.MissingContent.Count > missingCount) LogMilestone("faltante: " + Plan.MissingContent[missingCount]);
            if (Plan.CompletedCount != selected)
            {
                if (observation.MissingContent == null) LogMilestone(PlaythroughPlan.Label(milestone));
                if (Plan.Finished) Report();
                return;
            }
            if (Plan.Finished) { Report(); return; }
            if (command.Repath && !CalculatePath()) return;
            if (command.Move)
            {
                GameInput.MoveOverride = navigation.Move(scene.Position, scene.CameraYaw, Time.deltaTime, out bool run);
                GameInput.RunOverride = run;
            }
            GameInput.AimOverride = scene.Aim;
            GameInput.InteractOverride = command.Interact;
            GameInput.LightToggleOverride = command.ToggleLight;
            GameInput.FlashOverride = command.Flash;
            GameInput.KindleOverride = command.Kindle;
            GameInput.PoltergeistOverride = command.MoveObject;
            GameInput.FadeOverride = command.Vanish;
            scene.Apply(in command);
            if (command.ForceRespawn) LogMilestone("captura (forzado)");
        }

        bool CalculatePath()
        {
            if (navigation.Calculate(scene.Position, scene.Target)) return true;
            Plan.Fail("no hay camino completo por NavMesh al objetivo");
            Report();
            return false;
        }

        void ReceiveLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            if (earlyError == null) earlyError = message;
            Plan?.RecordConsoleError(message);
        }

        void FailException(Exception exception)
        {
            Debug.LogException(exception);
            Plan.Fail(exception.Message);
            Report();
        }

        void LogMilestone(string label) => Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "{0} Partida {1:0.0}s {2}", GameInfo.LogPrefix, Plan.Elapsed, label));

        void Report()
        {
            if (reported) return;
            reported = true;
            NeutralInput();
            Debug.Log(Plan.ExitCode switch
            {
                0 => string.Format(CultureInfo.InvariantCulture, "{0} Partida completa en {1:0.0}s", GameInfo.LogPrefix, Plan.Elapsed),
                2 => string.Format(CultureInfo.InvariantCulture, "{0} Partida completa con faltantes en {1:0.0}s: {2}",
                    GameInfo.LogPrefix, Plan.Elapsed, string.Join("; ", Plan.MissingContent)),
                _ => $"{GameInfo.LogPrefix} Partida fallida en {PlaythroughPlan.Label(Plan.Current)}: {Plan.FailureReason}"
            });
            if (quitOnFinish) Application.Quit(Plan.ExitCode);
            enabled = false;
        }

        static void NeutralInput()
        {
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = GameInput.InteractOverride = false;
            GameInput.LightToggleOverride = GameInput.FlashOverride = GameInput.KindleOverride = false;
            GameInput.PoltergeistOverride = GameInput.FadeOverride = false;
        }

        void OnDisable()
        {
            Application.logMessageReceived -= ReceiveLog;
            GameInput.ClearOverrides();
        }
    }
}
