using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace TiffanyGame
{
    public sealed class PlaythroughScene : IDisposable
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
        readonly TylerController tyler;
        readonly Transform route;
        readonly Transform ball;
        readonly Transform[] clearings;
        readonly CandleSpot[] family = new CandleSpot[8];
        readonly MemorySpot[] memories = new MemorySpot[5];
        readonly List<GameObject> copies = new List<GameObject>();
        readonly bool tylerWasActive;
        CandleSpot kindleTarget, originalKindle, checkpoint;
        MovableObject movable;
        HideVolume hideVolume;
        PlaythroughMilestone milestone;
        Vector3 expectedRespawn;
        float arrivalRadius;

        public Vector3 Position => player.transform.position;
        public Vector3 Target { get; private set; }
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
            tyler = Require(collector.tyler, "Tyler");
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
            tylerWasActive = tyler.gameObject.activeSelf;
        }

        public void Prepare(PlaythroughPlan plan, Action<string> log)
        {
            if (plan.Includes(PlaythroughMilestone.Ball)) tyler.gameObject.SetActive(false);
            if (plan.Includes(PlaythroughMilestone.Kindle))
            {
                for (int i = 0; i < kindler.candles.Length; i++)
                    if (kindler.candles[i] != null && !kindler.candles[i].Candle.IsLit
                        && kindler.candles[i].transform.IsChildOf(clearings[1])) { kindleTarget = kindler.candles[i]; break; }
                if (kindleTarget == null)
                {
                    if (kindler.candles.Length == 0) throw new InvalidOperationException("No hay veladoras para encender.");
                    originalKindle = Require(kindler.candles[0], "veladora apagada");
                    kindleTarget = Copy(originalKindle, family[1].transform.position + Vector3.right * 1.4f, clearings[1]);
                    kindler.candles[0] = kindleTarget;
                    log("veladora de prueba copiada a la planta de agua");
                }
            }
            if (plan.Includes(PlaythroughMilestone.MoveObject))
            {
                var objects = MovableObject.Active;
                for (int i = 0; i < objects.Count; i++)
                    if (!objects[i].Used && objects[i].transform.IsChildOf(clearings[2])) { movable = objects[i]; break; }
                if (movable == null)
                {
                    if (objects.Count == 0) throw new InvalidOperationException("No hay objetos que mover.");
                    var source = objects[0];
                    Vector3 position = family[2].transform.position + Vector3.right * 1.2f;
                    position.y += source.transform.localPosition.y;
                    movable = Copy(source, position, clearings[2]);
                    log("objeto de prueba copiado a las vías");
                }
            }
            if (plan.Includes(PlaythroughMilestone.Hide))
            {
                var volumes = Object.FindObjectsByType<HideVolume>(FindObjectsSortMode.None);
                hideVolume = NearestHide(volumes, true);
                if (hideVolume == null)
                {
                    hideVolume = Require(NearestHide(volumes, false), "HideVolume");
                    log("escondite más cercano de la escena: " + hideVolume.name);
                    var path = new NavMeshPath();
                    if (!NavMesh.SamplePosition(hideVolume.HidePoint, out var end, 2f, NavMesh.AllAreas)
                        || !NavMesh.CalculatePath(Position, end.position, NavMesh.AllAreas, path)
                        || path.status != NavMeshPathStatus.PathComplete)
                    {
                        hideVolume = Copy(hideVolume, clearings[0].TransformPoint(new Vector3(6f, 0f, -6f)), clearings[0]);
                        log("escondite sin sendero conectado: copia temporal en el cementerio");
                    }
                }
            }
            Physics.SyncTransforms();
        }

        HideVolume NearestHide(HideVolume[] volumes, bool onRoute)
        {
            HideVolume nearest = null;
            float distance = float.PositiveInfinity;
            foreach (var volume in volumes)
            {
                if (onRoute && !volume.transform.IsChildOf(route)) continue;
                float candidate = (volume.HidePoint - Position).sqrMagnitude;
                if (candidate >= distance) continue;
                nearest = volume;
                distance = candidate;
            }
            return nearest;
        }

        T Copy<T>(T source, Vector3 position, Transform parent) where T : Component
        {
            T copy = Object.Instantiate(source, position, source.transform.rotation, parent);
            copies.Add(copy.gameObject);
            return copy;
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
                case PlaythroughMilestone.Hide: Target = hideVolume.HidePoint; break;
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
            AtTarget = PlaythroughPlan.HorizontalDistance(Position, Target) <= arrivalRadius,
            MemoryFound = PlaythroughPlan.MemoryId(milestone) > 0 && collector.IsFound(PlaythroughPlan.MemoryId(milestone)),
            TylerVisible = tyler.gameObject.activeInHierarchy && tyler.figure.activeInHierarchy,
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
            if (command.ShowTyler) tyler.gameObject.SetActive(true);
            if (command.ForceRespawn) respawn.Trigger();
        }

        public void Dispose()
        {
            if (originalKindle != null && kindler != null) kindler.candles[0] = originalKindle;
            if (tyler != null) tyler.gameObject.SetActive(tylerWasActive);
            foreach (var copy in copies) if (copy != null) Object.Destroy(copy);
            copies.Clear();
        }

        static T Require<T>(T value, string name) where T : Object =>
            value != null ? value : throw new InvalidOperationException("Falta " + name + " en la escena.");
    }
}
