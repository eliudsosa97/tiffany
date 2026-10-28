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
        int currentClearing = -1;
        Bounds[] clearingBounds;
        string[] clearingLabels;
        float nextClearingCheck;
        string earlyError;
        static string startupError;
        public PlaythroughPlan Plan { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeSceneLoad()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if TIFFANY_WEB_DEBUG
            if (WebDebugQuery.Has(Application.absoluteURL, "playthrough")) SetStartupErrorCapture(true);
#endif
#else
            CaptureStartupErrors(Environment.GetCommandLineArgs());
#endif
        }

        public static void CaptureStartupErrors(string[] args) =>
            SetStartupErrorCapture(Array.IndexOf(args, "-playthrough") >= 0);

        static void SetStartupErrorCapture(bool enabled)
        {
            Application.logMessageReceived -= ReceiveStartupLog;
            startupError = null;
            if (enabled) Application.logMessageReceived += ReceiveStartupLog;
        }

        static void ReceiveStartupLog(string message, string stackTrace, LogType type)
        {
            if (startupError == null && (type == LogType.Error || type == LogType.Exception)) startupError = message;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
#if TIFFANY_WEB_DEBUG
            string url = Application.absoluteURL;
            if (!WebDebugQuery.Has(url, "playthrough")) return;
            float timeout = PlaythroughPlan.ParseTimeout(WebDebugQuery.Get(url, "playthrough-timeout"));
            new GameObject("Playthrough").AddComponent<Playthrough>().Configure(new PlaythroughPlan(timeout: timeout));
#endif
            return;
#else
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-playthrough") < 0) return;
            float timeout = PlaythroughPlan.DefaultTimeout;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-playthrough-timeout") timeout = PlaythroughPlan.ParseTimeout(args[i + 1], timeout);
            new GameObject("Playthrough").AddComponent<Playthrough>().Configure(new PlaythroughPlan(timeout: timeout), true);
#endif
        }

        public void Configure(PlaythroughPlan plan, bool quit = false)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
#if UNITY_WEBGL && !UNITY_EDITOR
            quitOnFinish = false;
#else
            quitOnFinish = quit;
#endif
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
                CacheClearings();
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
            LogClearing();
            if (Plan.MissingContent.Count > missingCount) LogMilestone("faltante: " + Plan.MissingContent[missingCount]);
            if (Plan.CompletedCount != selected)
            {
                if (Plan.CompletedCount < selected) LogMilestone("reintento del reto");
                else if (observation.MissingContent == null) LogMilestone(PlaythroughPlan.Label(milestone));
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

        void CacheClearings()
        {
            var tracker = FindAnyObjectByType<WitchChapterTracker>();
            if (tracker == null) return;
            clearingBounds = tracker.ClearingBounds;
            clearingLabels = new string[tracker.clearings.Length];
            for (int i = 0; i < clearingLabels.Length; i++)
                if (tracker.clearings[i] != null) clearingLabels[i] = "claro: " + tracker.clearings[i].name;
        }

        void LogClearing()
        {
            if (clearingBounds == null || Time.unscaledTime < nextClearingCheck) return;
            nextClearingCheck = Time.unscaledTime + 0.5f;
            int clearing = RouteChapters.FindClearing(scene.Position, clearingBounds);
            if (clearing < 0 || clearing == currentClearing) return;
            currentClearing = clearing;
            LogMilestone(clearingLabels[clearing]);
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
