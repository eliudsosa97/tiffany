using System;
using UnityEngine;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-100)]
    public sealed class SaveGame : MonoBehaviour
    {
        public const string PlayerKey = "Tiffany.SaveGame";
        public static bool DisabledForTests;
        public static string TestKey;

        public PlayerSoul soul;
        public MemoryCollector memories;
        public WitchController witch;
        public WitchChapterTracker route;
        public GameInterface gameInterface;
        public EndingController ending;
        public CandleSpot[] candles = Array.Empty<CandleSpot>();
        public CandleSpot initialCandle;

        SaveData loaded;
        CandleSpot checkpoint;
        int[] memoryIds;
        string[] candleIds;
        string key;
        bool persistenceEnabled;
        PlayerRespawn respawn;
        FollowCamera follow;

        void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            bool testKey = !string.IsNullOrEmpty(TestKey) && TestKey.StartsWith("Tiffany.Tests.", StringComparison.Ordinal);
            key = testKey ? TestKey : PlayerKey;
            persistenceEnabled = !SaveRules.IsAutomaticRun(args) && (testKey
                || (!DisabledForTests && Array.IndexOf(args, "-runTests") < 0));
#if UNITY_WEBGL && !UNITY_EDITOR && TIFFANY_WEB_DEBUG
            if (WebDebugQuery.Has(Application.absoluteURL, "playthrough")) persistenceEnabled = false;
#endif
            checkpoint = initialCandle;
            respawn = soul.GetComponent<PlayerRespawn>();
            if (memories.viewCamera != null) follow = memories.viewCamera.GetComponent<FollowCamera>();
            memoryIds = new int[memories.spots.Length];
            for (int i = 0; i < memoryIds.Length; i++) memoryIds[i] = memories.spots[i].memoryId;
            candleIds = new string[candles.Length];
            for (int i = 0; i < candleIds.Length; i++) candleIds[i] = candles[i].saveId;
        }

        void OnEnable()
        {
            memories.Collected += MemoryCollected;
            ending.CreditsReached += Delete;
            for (int i = 0; i < candles.Length; i++) candles[i].FamilyTouched += TouchCandle;
        }

        void Start()
        {
            if (persistenceEnabled) SaveRules.TryRead(PlayerPrefs.GetString(key, string.Empty), memoryIds, candleIds, out loaded);
            gameInterface.ConfigureSave(this, loaded != null);
        }

        public bool Continue()
        {
            if (loaded == null || !SaveRules.IsValid(loaded, memoryIds, candleIds)) return false;
            for (int i = 0; i < candles.Length; i++)
                if (candles[i].saveId == loaded.candleId) { checkpoint = candles[i]; break; }
            witch.chapter = SaveRules.Apply(loaded, memories.Progress, soul.Powers, soul.startingPowers,
                soul.Respawn, checkpoint.Candle, soul.Energy);
            respawn.PlaceAtCheckpoint();
            witch.ResetBrain();
            memories.RestoreFound();
            if (follow != null) follow.Snap();
            loaded = null;
            return true;
        }

        public void Delete()
        {
            loaded = null;
            if (!persistenceEnabled) return;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        void TouchCandle(CandleSpot candle)
        {
            if (gameInterface.Screen != InterfaceScreen.Playing) return;
            checkpoint = candle;
            Write();
        }

        void MemoryCollected(int id) => Write();

        void Write()
        {
            if (!persistenceEnabled || gameInterface.Screen != InterfaceScreen.Playing || checkpoint == null) return;
            Chapter chapter = witch.chapter;
            int clearing = RouteChapters.FindClearing(soul.transform.position, route.ClearingBounds);
            if (clearing >= 0) chapter = RouteChapters.Resolve(chapter, clearing + 1);
            var data = SaveRules.Capture(memories.Progress, memoryIds, checkpoint.saveId, chapter, soul.Energy);
            if (!SaveRules.IsValid(data, memoryIds, candleIds)) return;
            PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        void OnDisable()
        {
            memories.Collected -= MemoryCollected;
            ending.CreditsReached -= Delete;
            for (int i = 0; i < candles.Length; i++) candles[i].FamilyTouched -= TouchCandle;
        }
    }
}
