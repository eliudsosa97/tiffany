using System;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace TiffanyGame
{
    public sealed class PlaythroughScene
    {
        readonly MemoryCollector collector;
        readonly PlayerController player;
        readonly PlayerSoul soul;
        readonly PlayerLight light;
        readonly PlayerHiding hiding;
        readonly PlayerRespawn respawn;
        readonly VanishPower vanish;
        readonly CandleKindler kindler;
        readonly EndingController ending;
        readonly Transform route;
        readonly Transform ball;
        readonly Transform[] clearings;
        readonly CandleSpot[] family = new CandleSpot[8];
        readonly MemorySpot[] memories = new MemorySpot[5];
        CandleSpot kindleTarget, checkpoint;
        MovableObject movable;
        HideVolume hideVolume;
        PlaythroughMilestone milestone;
        Vector3 expectedRespawn;
        float arrivalRadius;

        public Vector3 Position => player.transform.position;
        public Vector3 Target { get; private set; }
        bool AtTarget => PlaythroughPlan.HorizontalDistance(Position, Target) <= arrivalRadius;
        public float CameraYaw => player.viewCamera != null ? player.viewCamera.transform.eulerAngles.y : 0f;
        public Vector2 Aim => milestone == PlaythroughMilestone.MoveObject
            ? PlaythroughNavigation.CameraInput((movable.transform.position - Position).normalized, CameraYaw)
            : Vector2.zero;

        public PlaythroughScene()
        {
            collector = Require(Object.FindAnyObjectByType<MemoryCollector>(), "MemoryCollector");
            player = Require(collector.GetComponent<PlayerController>(), "PlayerController");
            soul = Require(collector.soul, "PlayerSoul");
            light = Require(player.GetComponent<PlayerLight>(), "PlayerLight");
            hiding = Require(collector.hiding, "PlayerHiding");
            respawn = Require(player.GetComponent<PlayerRespawn>(), "PlayerRespawn");
            vanish = Require(player.GetComponent<VanishPower>(), "VanishPower");
            kindler = Require(player.GetComponent<CandleKindler>(), "CandleKindler");
            ending = Require(Object.FindAnyObjectByType<EndingController>(), "EndingController");
            var tracker = Require(Object.FindAnyObjectByType<WitchChapterTracker>(), "WitchChapterTracker");
            route = tracker.transform;
            clearings = tracker.clearings;
            if (clearings.Length != 8) throw new InvalidOperationException("La ruta necesita ocho claros.");
            for (int i = 0; i < clearings.Length; i++)
            {
                Require(clearings[i], "claro de la ruta");
                foreach (var candle in clearings[i].GetComponentsInChildren<CandleSpot>())
                    if (candle.kind == CandleKind.Family) { family[i] = candle; break; }
                Require(family[i], "veladora familiar");
            }
            ball = Require(clearings[0].Find("Ball"), "Ball");
            foreach (var memory in collector.spots)
                if (memory != null && memory.memoryId >= 1 && memory.memoryId <= 5) memories[memory.memoryId - 1] = memory;
        }

        public void Prepare(PlaythroughPlan plan, Action<string> log)
        {
            if (plan.Includes(PlaythroughMilestone.Kindle))
            {
                for (int i = 0; i < kindler.candles.Length; i++)
                    if (kindler.candles[i] != null && kindler.candles[i].isActiveAndEnabled
                        && kindler.candles[i].kind == CandleKind.Kindled && !kindler.candles[i].Candle.IsLit)
                    { kindleTarget = kindler.candles[i]; break; }
                Require(kindleTarget, "veladora apagada para encender");
            }
            if (plan.Includes(PlaythroughMilestone.MoveObject))
            {
                var objects = MovableObject.Active;
                for (int i = 0; i < objects.Count; i++)
                    if (!objects[i].Used) { movable = objects[i]; break; }
                Require(movable, "objeto movible sin usar");
            }
            if (plan.Includes(PlaythroughMilestone.Hide))
            {
                var volumes = Object.FindObjectsByType<HideVolume>(FindObjectsSortMode.None);
                hideVolume = NearestHide(volumes, true);
                if (hideVolume == null)
                {
                    hideVolume = NearestHide(volumes, false);
                    if (hideVolume != null) log("escondite más cercano de la escena: " + hideVolume.name);
                }
            }
        }

        HideVolume NearestHide(HideVolume[] volumes, bool onRoute)
        {
            HideVolume nearest = null;
            float distance = float.PositiveInfinity;
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(Position, out var start, 5f, NavMesh.AllAreas)) return null;
            foreach (var volume in volumes)
            {
                if (!volume.isActiveAndEnabled || (onRoute && !volume.transform.IsChildOf(route))) continue;
                float candidate = (volume.HidePoint - Position).sqrMagnitude;
                if (candidate >= distance) continue;
                if (!NavMesh.SamplePosition(volume.HidePoint, out var end, 0.5f, NavMesh.AllAreas)
                    || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                    || path.status != NavMeshPathStatus.PathComplete) continue;
                nearest = volume;
                distance = candidate;
            }
            return nearest;
        }

        public void Select(PlaythroughMilestone next)
        {
            milestone = next;
            Target = Position;
            arrivalRadius = 0.4f;
            int id = PlaythroughPlan.MemoryId(next);
            if (id > 0)
            {
                Target = Require(memories[id - 1], "recuerdo de la ruta").transform.position;
                arrivalRadius = 1.3f;
                return;
            }
            switch (next)
            {
                case PlaythroughMilestone.Ball:
                    Target = ball.position;
                    arrivalRadius = 1f;
                    break;
                case PlaythroughMilestone.Hide:
                    if (hideVolume != null) Target = hideVolume.HidePoint;
                    break;
                case PlaythroughMilestone.Recharge2: SelectCandle(1); break;
                case PlaythroughMilestone.Recharge3: SelectCandle(2); break;
                case PlaythroughMilestone.Recharge4: SelectCandle(3); break;
                case PlaythroughMilestone.LakeCandle: SelectCandle(5); break;
                case PlaythroughMilestone.Kindle: Target = kindleTarget.transform.position + Vector3.back * 1.4f; break;
                case PlaythroughMilestone.MoveObject: Target = movable.transform.position + Vector3.back * 2f; break;
                case PlaythroughMilestone.Capture:
                    expectedRespawn = soul.Respawn.Position + Vector3.back * respawn.sideOffset;
                    break;
                case PlaythroughMilestone.Road: Target = clearings[6].position; break;
                case PlaythroughMilestone.Manifest:
                    Target = ending.transform.position + Vector3.back * 1.5f;
                    Target = new Vector3(Target.x, clearings[7].position.y, Target.z);
                    arrivalRadius = 0.25f;
                    break;
            }
        }

        void SelectCandle(int clearing)
        {
            checkpoint = family[clearing];
            Target = checkpoint.transform.position + Vector3.back * 1.5f;
        }

        public PlaythroughObservation Observe() => new PlaythroughObservation
        {
            Position = Position,
            AtTarget = AtTarget,
            MemoryFound = PlaythroughPlan.MemoryId(milestone) > 0 && collector.IsFound(PlaythroughPlan.MemoryId(milestone)),
            MissingContent = milestone switch
            {
                PlaythroughMilestone.Ball when AtTarget => "recogida real de la pelota",
                PlaythroughMilestone.Hide when hideVolume == null => "escondite alcanzable por NavMesh desde la ruta",
                _ => null
            },
            LightOn = light.State.IsOn,
            Flashing = light.State.IsFlashing,
            Hidden = hiding.Hidden,
            CandleLit = kindleTarget != null && kindleTarget.Candle.IsLit,
            ObjectMoved = movable != null && movable.Used,
            Vanished = vanish.State.IsActive,
            Energy = soul.Energy.Current,
            CheckpointReached = checkpoint != null && checkpoint.Site.PlayerInside && soul.Respawn.Position == checkpoint.Candle.Position,
            Respawning = respawn.Active,
            AtRespawn = player.enabled && PlaythroughPlan.HorizontalDistance(Position, expectedRespawn) < 0.35f,
            Credits = ending.Rules.Stage == EndingStage.Credits,
            Frozen = collector.gameInterface.IsFrozen
        };

        public void Apply(in PlaythroughCommand command)
        {
            if (command.ForceRespawn) respawn.Trigger();
        }

        static T Require<T>(T value, string name) where T : Object =>
            value != null ? value : throw new InvalidOperationException("Falta " + name + " en la escena.");
    }
}
