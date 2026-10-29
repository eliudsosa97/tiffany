using System;
using UnityEngine;

namespace TiffanyGame
{
    [DefaultExecutionOrder(100)]
    public sealed class PowerChallenge : MonoBehaviour
    {
        public ChallengeKind kind;
        public WitchChapterTracker tracker;
        public CandleSpot checkpoint;
        public CandleSpot[] candles = Array.Empty<CandleSpot>();
        public GameObject[] darkness = Array.Empty<GameObject>();
        public MovableObject lure;
        public Transform post;
        public Vector3 entryOffset = new Vector3(0f, 0f, -0.3f);
        public Vector3 exitOffset = Vector3.forward * (ChallengeRules.PassageLength + 0.3f);
        public ChallengeRules Rules { get; private set; }
        public bool Underway { get; private set; }
        public WitchController Witch => tracker.witch;
        public Vector3 Entry => transform.TransformPoint(entryOffset);
        public Vector3 Exit => transform.TransformPoint(exitOffset);
        public Vector3 RechargePoint => checkpoint.transform.position + transform.right * 0.6f;
        public bool Distracted => lure != null && lure.Used && Witch != null && Witch.WatchPassage == transform
            && Witch.WatchPassageClear;

        PlayerSoul soul;
        PlayerRespawn respawn;
        VanishPower vanish;
        bool active;

        void Awake() => Rules = new ChallengeRules(kind);

        void Start()
        {
            soul = tracker.player.GetComponent<PlayerSoul>();
            respawn = tracker.player.GetComponent<PlayerRespawn>();
            vanish = tracker.player.GetComponent<VanishPower>();
            if (respawn != null) respawn.Respawned += ResetAttempt;
        }

        void OnDestroy()
        {
            if (respawn != null) respawn.Respawned -= ResetAttempt;
        }

        void Update()
        {
            if (Time.timeScale == 0f || soul == null || (respawn != null && respawn.Active)) return;
            Vector3 local = transform.InverseTransformPoint(soul.transform.position);
            Underway = !Rules.Completed && !ChallengeRules.HasLeftArea(local.x, local.z, checkpoint.transform.localPosition.z);
            if (Rules.Completed) return;
            if (!active && Mathf.Abs(local.x) < 8f
                && local.z >= checkpoint.transform.localPosition.z - 3f && local.z < 0f)
            {
                if (kind != ChallengeKind.Candles && (Witch == null || !Witch.isActiveAndEnabled
                    || (Witch.WatchPassage != null && Witch.WatchPassage != transform))) return;
                active = true;
                if (kind != ChallengeKind.Candles) Witch.BeginWatch(transform, post.position, Rules.NeedsVanish, Rules.NeedsDistraction);
            }
            if (!active) return;
            if (kind != ChallengeKind.Candles
                && ChallengeRules.HasLeftArea(local.x, local.z, checkpoint.transform.localPosition.z))
            {
                ResetAttempt();
                return;
            }
            int lit = 0;
            for (int i = 0; i < candles.Length; i++)
                if (candles[i].Candle.IsLit) lit |= 1 << i;
            Rules.Tick(local.z, Mathf.Abs(local.x) < 0.7f, lit, Distracted, vanish != null && vanish.State.IsActive);
            ApplyGates();
            if (Rules.Completed)
            {
                Underway = false;
                if (Witch != null && Witch.WatchPassage == transform) Witch.EndWatch();
                Debug.Log($"{GameInfo.LogPrefix} Reto resuelto: {kind}");
            }
            else if (lure != null && ChallengeRules.CanRestoreLure(checkpoint.Site.PlayerInside, lure.Used,
                Witch.WatchInvestigating, Time.time - lure.LastMovedAt))
                lure.Restore();
        }

        public void ResetAttempt()
        {
            if (Rules.Completed) return;
            if (active && vanish != null) vanish.Cancel();
            Rules.ResetAttempt();
            for (int i = 0; i < candles.Length; i++) candles[i].Candle.Extinguish();
            if (lure != null) lure.Restore();
            if (Witch != null && Witch.WatchPassage == transform) Witch.EndWatch();
            active = false;
            ApplyGates();
        }

        void ApplyGates()
        {
            for (int i = 0; i < darkness.Length; i++)
                if (darkness[i].activeSelf == Rules.GateOpen(i)) darkness[i].SetActive(!Rules.GateOpen(i));
        }
    }
}
