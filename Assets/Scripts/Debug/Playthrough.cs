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
        public PlaythroughPlan Plan { get; private set; }

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
            if (earlyError != null) Plan.RecordConsoleError(earlyError);
        }

        void Awake() => Application.logMessageReceived += ReceiveLog;

        IEnumerator Start()
        {
            NeutralInput();
            yield return null;
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
            if (selected != Plan.CompletedCount)
            {
                selected = Plan.CompletedCount;
                scene.Select(Plan.Current);
                if (PlaythroughPlan.NeedsNavigation(Plan.Current) && !CalculatePath()) return;
            }
            PlaythroughMilestone milestone = Plan.Current;
            var observation = scene.Observe();
            var command = Plan.Tick(Time.unscaledDeltaTime, in observation);
            if (Plan.CompletedCount != selected)
            {
                LogMilestone(PlaythroughPlan.Label(milestone));
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
            Debug.Log(Plan.ExitCode == 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} Partida completa en {1:0.0}s", GameInfo.LogPrefix, Plan.Elapsed)
                : $"{GameInfo.LogPrefix} Partida fallida en {PlaythroughPlan.Label(Plan.Current)}: {Plan.FailureReason}");
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
            scene?.Dispose();
        }
    }
}
