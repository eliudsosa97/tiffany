using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public static class SaveRules
    {
        public const int Version = 2;

        public static SaveData Capture(MemoryProgress progress, IReadOnlyList<int> memoryIds,
            string candleId, Chapter chapter, SoulEnergy energy, IReadOnlyList<ChallengeRules> challenges)
        {
            var found = new List<int>();
            for (int i = 0; i < memoryIds.Count; i++)
                if (progress.IsFound(memoryIds[i])) found.Add(memoryIds[i]);
            int completed = 0;
            for (int i = 0; i < challenges.Count; i++)
                if (challenges[i] != null && challenges[i].Completed) completed |= 1 << (int)challenges[i].Kind;
            return new SaveData
            {
                version = Version, memories = found.ToArray(), candleId = candleId,
                chapter = chapter, energy = energy.Current, completedChallenges = completed
            };
        }

        public static bool IsValid(SaveData data, IReadOnlyList<int> memoryIds, IReadOnlyList<string> candleIds)
        {
            if (data == null || data.version != Version || data.memories == null
                || data.memories.Length > memoryIds.Count || string.IsNullOrEmpty(data.candleId)
                || !Contains(candleIds, data.candleId) || data.chapter < Chapter.Cemetery
                || data.chapter > Chapter.Return || float.IsNaN(data.energy) || float.IsInfinity(data.energy)
                || data.energy < 0f || data.energy > SoulEnergy.Max || (data.completedChallenges & ~15) != 0) return false;
            bool lastMemory = false;
            for (int i = 0; i < data.memories.Length; i++)
            {
                int id = data.memories[i];
                if ((id < 1 || id > 5) && id != MemoryProgress.BallId) return false;
                if (!Contains(memoryIds, id)) return false;
                if (!RouteProgress.CanCollectMemory(id, data.completedChallenges)) return false;
                for (int j = 0; j < i; j++)
                    if (data.memories[j] == id) return false;
                if (RouteProgress.StartsReturn(id)) lastMemory = true;
            }
            return lastMemory == (data.chapter == Chapter.Return);
        }

        public static bool TryRead(string json, IReadOnlyList<int> memoryIds,
            IReadOnlyList<string> candleIds, out SaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var parsed = JsonUtility.FromJson<SaveData>(json);
                if (!IsValid(parsed, memoryIds, candleIds)) return false;
                data = parsed;
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        public static Chapter Apply(SaveData data, MemoryProgress progress, PowerSet powers,
            IReadOnlyList<Power> startingPowers, RespawnPoint respawn, Candle candle, SoulEnergy energy,
            IReadOnlyList<ChallengeRules> challenges)
        {
            progress.Clear();
            powers.Clear();
            for (int i = 0; i < startingPowers.Count; i++) powers.Unlock(startingPowers[i]);
            for (int i = 0; i < data.memories.Length; i++)
            {
                int id = data.memories[i];
                progress.MarkFound(id);
                if (id >= 1 && id <= 5) powers.Unlock(PowerSet.ForMemory(id));
            }
            for (int i = 0; i < challenges.Count; i++)
                if (challenges[i] != null && (data.completedChallenges & (1 << (int)challenges[i].Kind)) != 0)
                    challenges[i].MarkCompleted();
            respawn.Touch(candle);
            respawn.Respawn(energy);
            return data.chapter;
        }

        public static bool IsAutomaticRun(string[] args) => Array.IndexOf(args, "-playthrough") >= 0
            || Array.IndexOf(args, "-autocapture") >= 0 || Array.IndexOf(args, "-echodemo") >= 0
            || Array.IndexOf(args, "-nightpreview") >= 0;

        static bool Contains<T>(IReadOnlyList<T> values, T value)
        {
            for (int i = 0; i < values.Count; i++)
                if (EqualityComparer<T>.Default.Equals(values[i], value)) return true;
            return false;
        }
    }
}
