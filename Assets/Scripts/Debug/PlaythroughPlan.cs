using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TiffanyGame
{
    public enum PlaythroughMilestone
    {
        Memory1, Ball, Light, Flash, Kindle, MoveObject, Hide, LeaveHide, Memory2, Recharge2,
        Memory3, Recharge3, Memory4, Recharge4, Vanish, Memory5,
        Capture, Respawn, LakeCandle, Road, Manifest,
        Challenge1Recharge, Challenge1Candle1, Challenge1Candle2, Challenge1Cross,
        Challenge2Recharge, Challenge2Distract, Challenge2Wait, Challenge2Cross,
        Challenge3Recharge, Challenge3Entry, Challenge3Vanish, Challenge3Cross,
        Challenge4Recharge, Challenge4Candle1, Challenge4Candle2, Challenge4Distract, Challenge4Wait, Challenge4Vanish, Challenge4Cross,
        RampCandle
    }

    public enum ChallengeAction { None, Recharge, Candle1, Candle2, Distract, Wait, Entry, Vanish, Cross }

    public struct PlaythroughObservation
    {
        public Vector3 Position, Target;
        public bool AtTarget, MemoryFound, BallCollected, LightOn, Flashing, Hidden;
        public bool CandleLit, ObjectMoved, Vanished, CheckpointReached, Respawning, AtRespawn, Credits, Frozen;
        public float Energy, RespawnGraceRemaining;
        public bool InSafeZone;
        public string MissingContent;
        public bool ChallengeCompleted, Distracted, RetryChallenge;
        public WitchState WitchState;
        public Vector3 WitchPosition;
    }

    public struct PlaythroughCommand
    {
        public bool Move, Run, Repath, Interact, ToggleLight, Flash, Kindle, MoveObject, Vanish, ForceRespawn;
    }

    public sealed class PlaythroughPlan
    {
        public const float DefaultTimeout = 90f;
        public const float LakeCandleTimeout = 180f;
        public const float CaptureTimeout = 40f;
        public const float WalkLightEnergy = 30f, WitchFireNear = 25f;
        public const float WitchGroundNear = 30f;
        public const float StuckSeconds = 3f;
        public const float ProgressDistance = 1f;
        public const float ReturnDepartureClearance = 12f;
        const float PressInterval = 0.3f;
        readonly PlaythroughMilestone[] milestones;
        readonly List<string> missingContent = new List<string>();
        readonly float timeout;
        Vector3 progressPosition;
        float stalledSeconds, pressCooldown;
        bool trackingMovement, forceRequested, wasRespawning, repathAfterRespawn;

        public int CompletedCount { get; private set; }
        public int Count => milestones.Length;
        public PlaythroughMilestone Current => milestones[Math.Min(CompletedCount, Count - 1)];
        public float Elapsed { get; private set; }
        public float MilestoneElapsed { get; private set; }
        public bool Finished { get; private set; }
        public int ExitCode { get; private set; } = -1;
        public string FailureReason { get; private set; }
        public string FirstConsoleError { get; private set; }
        public IReadOnlyList<string> MissingContent => missingContent;

        public PlaythroughPlan(PlaythroughMilestone[] milestones = null, float timeout = DefaultTimeout)
        {
            if (!(timeout > 0f) || float.IsInfinity(timeout)) throw new ArgumentOutOfRangeException(nameof(timeout));
            this.timeout = timeout;
            this.milestones = milestones == null
                ? new[]
                {
                    PlaythroughMilestone.Memory1, PlaythroughMilestone.Ball, PlaythroughMilestone.Light, PlaythroughMilestone.Flash,
                    PlaythroughMilestone.Hide, PlaythroughMilestone.LeaveHide, PlaythroughMilestone.Memory2,
                    PlaythroughMilestone.Challenge1Recharge, PlaythroughMilestone.Challenge1Candle1,
                    PlaythroughMilestone.Challenge1Candle2, PlaythroughMilestone.Challenge1Cross,
                    PlaythroughMilestone.Memory3, PlaythroughMilestone.Challenge2Recharge, PlaythroughMilestone.Challenge2Distract,
                    PlaythroughMilestone.Challenge2Wait, PlaythroughMilestone.Challenge2Cross,
                    PlaythroughMilestone.Memory4, PlaythroughMilestone.Challenge3Recharge, PlaythroughMilestone.Challenge3Entry,
                    PlaythroughMilestone.Challenge3Vanish, PlaythroughMilestone.Challenge3Cross,
                    PlaythroughMilestone.Challenge4Recharge, PlaythroughMilestone.Challenge4Candle1, PlaythroughMilestone.Challenge4Candle2,
                    PlaythroughMilestone.Challenge4Distract, PlaythroughMilestone.Challenge4Wait,
                    PlaythroughMilestone.Challenge4Vanish, PlaythroughMilestone.Challenge4Cross,
                    PlaythroughMilestone.Memory5, PlaythroughMilestone.Capture, PlaythroughMilestone.Respawn,
                    PlaythroughMilestone.RampCandle, PlaythroughMilestone.LakeCandle, PlaythroughMilestone.Road,
                    PlaythroughMilestone.Manifest
                }
                : (PlaythroughMilestone[])milestones.Clone();
            if (Count == 0) throw new ArgumentException("La partida necesita hitos.", nameof(milestones));
        }

        public static float ParseTimeout(string value, float fallback = DefaultTimeout) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
                && seconds > 0f && !float.IsInfinity(seconds) ? seconds : fallback;

        public bool Includes(PlaythroughMilestone milestone) => Array.IndexOf(milestones, milestone) >= 0;

        public void RecordConsoleError(string message)
        {
            if (!Finished && FirstConsoleError == null) FirstConsoleError = message;
        }

        public void Fail(string reason)
        {
            if (Finished) return;
            FailureReason = FirstConsoleError == null ? reason : reason + ". Primer error de consola: " + FirstConsoleError;
            Finished = true;
            ExitCode = 1;
        }

        public PlaythroughCommand Tick(float dt, in PlaythroughObservation observation)
        {
            if (Finished) return default;
            if (!(dt >= 0f) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            Elapsed += dt;
            MilestoneElapsed += dt;
            if (wasRespawning && !observation.Respawning)
            {
                MilestoneElapsed = stalledSeconds = 0f;
                trackingMovement = false;
                repathAfterRespawn = true;
            }
            wasRespawning = observation.Respawning;
            pressCooldown -= dt;
            if (ChallengeIndex(Current) >= 0 && (observation.Respawning || observation.RetryChallenge))
            {
                int challenge = ChallengeIndex(Current);
                while (CompletedCount > 0 && ChallengeIndex(milestones[CompletedCount - 1]) == challenge) CompletedCount--;
                MilestoneElapsed = stalledSeconds = pressCooldown = 0f;
                trackingMovement = false;
                return default;
            }
            float milestoneTimeout = Current == PlaythroughMilestone.LakeCandle ? LakeCandleTimeout : timeout;
            if (MilestoneElapsed >= milestoneTimeout)
            {
                Fail("tiempo límite agotado");
                return default;
            }
            if (observation.MissingContent != null || Satisfied(in observation))
            {
                bool skipLeaveHide = Current == PlaythroughMilestone.Hide && observation.MissingContent != null;
                if (observation.MissingContent != null && !missingContent.Contains(observation.MissingContent))
                    missingContent.Add(observation.MissingContent);
                CompletedCount++;
                if (skipLeaveHide && CompletedCount < Count && Current == PlaythroughMilestone.LeaveHide) CompletedCount++;
                if (CompletedCount == Count)
                {
                    if (FirstConsoleError != null) Fail("error de consola durante la partida");
                    else { Finished = true; ExitCode = missingContent.Count == 0 ? 0 : 2; }
                }
                MilestoneElapsed = stalledSeconds = pressCooldown = 0f;
                trackingMovement = forceRequested = repathAfterRespawn = false;
                return default;
            }

            var command = new PlaythroughCommand();
            ChallengeAction action = Action(Current);
            if (observation.Frozen || observation.Respawning) return command;
            bool returning = Current == PlaythroughMilestone.Respawn || Current == PlaythroughMilestone.RampCandle
                || Current == PlaythroughMilestone.LakeCandle || Current == PlaythroughMilestone.Road;
            bool waiting = returning && observation.InSafeZone && observation.RespawnGraceRemaining > 0f
                && HorizontalDistance(observation.WitchPosition, observation.Target) < ReturnDepartureClearance;
            command.Run = returning && !waiting;
            command.Move = NeedsNavigation(Current) && !observation.AtTarget && !waiting;
            command.Repath = (command.Move && repathAfterRespawn) || CheckStuck(dt, observation.Position, command.Move);
            if (command.Repath) repathAfterRespawn = false;
            bool press = pressCooldown <= 0f;
            bool wantsLight = Current == PlaythroughMilestone.Light || Current == PlaythroughMilestone.MoveObject
                || action == ChallengeAction.Distract || action == ChallengeAction.Wait
                || (Current == PlaythroughMilestone.Capture && observation.AtTarget)
                || WalksWithLight(Current, command.Move, in observation);
            command.ToggleLight = press && observation.LightOn != wantsLight;
            if (observation.AtTarget && press)
            {
                command.Interact = MemoryId(Current) > 0 || Current == PlaythroughMilestone.Ball || Current == PlaythroughMilestone.Hide
                    || Current == PlaythroughMilestone.LeaveHide || Current == PlaythroughMilestone.Manifest;
                command.Flash = Current == PlaythroughMilestone.Flash;
                command.Kindle = Current == PlaythroughMilestone.Kindle || action == ChallengeAction.Candle1 || action == ChallengeAction.Candle2;
                command.MoveObject = Current == PlaythroughMilestone.MoveObject || action == ChallengeAction.Distract;
                command.Vanish = Current == PlaythroughMilestone.Vanish || action == ChallengeAction.Vanish;
            }
            if (Current == PlaythroughMilestone.Capture && MilestoneElapsed >= CaptureTimeout && !forceRequested)
            {
                command.ForceRespawn = true;
                forceRequested = true;
            }
            if (press) pressCooldown = PressInterval;
            return command;
        }

        bool Satisfied(in PlaythroughObservation o)
        {
            switch (Action(Current))
            {
                case ChallengeAction.Recharge: return o.CheckpointReached && o.Energy >= SoulEnergy.Max - 1f;
                case ChallengeAction.Candle1:
                case ChallengeAction.Candle2: return o.CandleLit;
                case ChallengeAction.Distract: return o.ObjectMoved;
                case ChallengeAction.Wait: return o.Distracted;
                case ChallengeAction.Entry: return o.AtTarget;
                case ChallengeAction.Vanish: return o.Vanished;
                case ChallengeAction.Cross: return o.ChallengeCompleted;
            }
            if (MemoryId(Current) > 0) return o.MemoryFound;
            switch (Current)
            {
                case PlaythroughMilestone.Ball: return o.AtTarget && o.BallCollected;
                case PlaythroughMilestone.Light: return o.LightOn;
                case PlaythroughMilestone.Flash: return o.Flashing;
                case PlaythroughMilestone.Hide: return o.Hidden;
                case PlaythroughMilestone.LeaveHide: return !o.Hidden;
                case PlaythroughMilestone.Kindle: return o.CandleLit;
                case PlaythroughMilestone.MoveObject: return o.ObjectMoved;
                case PlaythroughMilestone.Vanish: return o.Vanished;
                case PlaythroughMilestone.Recharge2:
                case PlaythroughMilestone.Recharge3:
                case PlaythroughMilestone.Recharge4:
                case PlaythroughMilestone.RampCandle:
                case PlaythroughMilestone.LakeCandle: return o.CheckpointReached && o.Energy >= SoulEnergy.Max - 1f;
                case PlaythroughMilestone.Capture: return o.Respawning;
                case PlaythroughMilestone.Respawn: return !o.Respawning && o.AtRespawn;
                case PlaythroughMilestone.Road: return o.AtTarget;
                case PlaythroughMilestone.Manifest: return o.Credits;
                default: return false;
            }
        }

        bool CheckStuck(float dt, Vector3 position, bool moving)
        {
            if (!moving) { trackingMovement = false; stalledSeconds = 0f; return false; }
            if (!trackingMovement || HorizontalDistance(progressPosition, position) >= ProgressDistance)
            {
                progressPosition = position;
                trackingMovement = true;
                stalledSeconds = 0f;
                return false;
            }
            stalledSeconds += dt;
            if (stalledSeconds < StuckSeconds) return false;
            stalledSeconds = 0f;
            progressPosition = position;
            return true;
        }

        public static bool NeedsNavigation(PlaythroughMilestone milestone) =>
            milestone != PlaythroughMilestone.Light && milestone != PlaythroughMilestone.Flash
            && milestone != PlaythroughMilestone.LeaveHide && milestone != PlaythroughMilestone.Vanish
            && milestone != PlaythroughMilestone.Capture && milestone != PlaythroughMilestone.Respawn;

        public static bool WalksWithLight(PlaythroughMilestone milestone, bool moving, in PlaythroughObservation o) =>
            moving && ChallengeIndex(milestone) < 0 && milestone != PlaythroughMilestone.Hide
            && milestone != PlaythroughMilestone.LeaveHide && milestone != PlaythroughMilestone.Vanish
            && o.Energy >= WalkLightEnergy
            && !(o.WitchState == WitchState.Flying && HorizontalDistance(o.Position, o.WitchPosition) < WitchFireNear)
            && (o.WitchState == WitchState.Flying || o.WitchState == WitchState.Landing || o.WitchState == WitchState.TakingOff
                || HorizontalDistance(o.Position, o.WitchPosition) > WitchGroundNear);

        public static int ChallengeIndex(PlaythroughMilestone milestone)
        {
            if (milestone < PlaythroughMilestone.Challenge1Recharge || milestone > PlaythroughMilestone.Challenge4Cross) return -1;
            if (milestone >= PlaythroughMilestone.Challenge4Recharge) return 3;
            if (milestone >= PlaythroughMilestone.Challenge3Recharge) return 2;
            if (milestone >= PlaythroughMilestone.Challenge2Recharge) return 1;
            return 0;
        }

        public static ChallengeAction Action(PlaythroughMilestone milestone) => milestone switch
        {
            PlaythroughMilestone.Challenge1Recharge or PlaythroughMilestone.Challenge2Recharge
                or PlaythroughMilestone.Challenge3Recharge or PlaythroughMilestone.Challenge4Recharge => ChallengeAction.Recharge,
            PlaythroughMilestone.Challenge1Candle1 or PlaythroughMilestone.Challenge4Candle1 => ChallengeAction.Candle1,
            PlaythroughMilestone.Challenge1Candle2 or PlaythroughMilestone.Challenge4Candle2 => ChallengeAction.Candle2,
            PlaythroughMilestone.Challenge2Distract or PlaythroughMilestone.Challenge4Distract => ChallengeAction.Distract,
            PlaythroughMilestone.Challenge2Wait or PlaythroughMilestone.Challenge4Wait => ChallengeAction.Wait,
            PlaythroughMilestone.Challenge3Entry => ChallengeAction.Entry,
            PlaythroughMilestone.Challenge3Vanish or PlaythroughMilestone.Challenge4Vanish => ChallengeAction.Vanish,
            PlaythroughMilestone.Challenge1Cross or PlaythroughMilestone.Challenge2Cross
                or PlaythroughMilestone.Challenge3Cross or PlaythroughMilestone.Challenge4Cross => ChallengeAction.Cross,
            _ => ChallengeAction.None
        };

        public static int MemoryId(PlaythroughMilestone milestone) => milestone switch
        {
            PlaythroughMilestone.Memory1 => 1, PlaythroughMilestone.Memory2 => 2,
            PlaythroughMilestone.Memory3 => 3, PlaythroughMilestone.Memory4 => 4,
            PlaythroughMilestone.Memory5 => 5, _ => 0
        };

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public static string Label(PlaythroughMilestone milestone) => milestone switch
        {
            PlaythroughMilestone.Memory1 => "lápida (recuerdo 1)",
            PlaythroughMilestone.Ball => "recoger la pelota",
            PlaythroughMilestone.Light => "luz",
            PlaythroughMilestone.Flash => "destello",
            PlaythroughMilestone.Hide => "entrar al escondite",
            PlaythroughMilestone.LeaveHide => "salir del escondite",
            PlaythroughMilestone.Memory2 => "listón (recuerdo 2)",
            PlaythroughMilestone.Recharge2 => "veladora de la planta de agua",
            PlaythroughMilestone.Kindle => "encender una veladora",
            PlaythroughMilestone.Memory3 => "muñeca (recuerdo 3)",
            PlaythroughMilestone.Recharge3 => "veladora de las vías",
            PlaythroughMilestone.MoveObject => "mover un objeto",
            PlaythroughMilestone.Memory4 => "zapato (recuerdo 4)",
            PlaythroughMilestone.Recharge4 => "veladora del establo",
            PlaythroughMilestone.Vanish => "desvanecerse",
            PlaythroughMilestone.Memory5 => "brazalete (recuerdo 5)",
            PlaythroughMilestone.RampCandle => "veladora de la rampa",
            PlaythroughMilestone.LakeCandle => "veladora del lago",
            PlaythroughMilestone.Capture => "captura durante el regreso",
            PlaythroughMilestone.Respawn => "reaparición en la última veladora",
            PlaythroughMilestone.Road => "carretera",
            PlaythroughMilestone.Manifest => "manifestarse en casa y créditos",
            _ when ChallengeIndex(milestone) >= 0 => $"reto {ChallengeIndex(milestone) + 1}: {Action(milestone)}",
            _ => throw new ArgumentOutOfRangeException(nameof(milestone))
        };
    }
}
