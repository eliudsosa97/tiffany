using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class MemoryCatalog
    {
        private readonly Dictionary<int, MemoryData> memories = new Dictionary<int, MemoryData>();

        public int Count => memories.Count;
        public IReadOnlyList<MemoryData> All { get; }

        public MemoryCatalog(IEnumerable<TextAsset> assets)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            var ordered = new List<MemoryData>();
            foreach (TextAsset asset in assets)
            {
                if (asset == null)
                {
                    Warn("<null>", "el asset es null.");
                    continue;
                }

                MemoryData memory;
                try
                {
                    memory = JsonUtility.FromJson<MemoryData>(asset.text);
                }
                catch (Exception exception)
                {
                    Warn(asset.name, $"JSON inválido: {exception.Message}");
                    continue;
                }

                if (memory == null)
                {
                    Warn(asset.name, "el JSON está vacío");
                    continue;
                }

                if (memory.id < 1)
                {
                    Warn(asset.name, $"el id debe ser 1 o mayor (tiene {memory.id})");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(memory.title))
                {
                    Warn(asset.name, "falta el título");
                    continue;
                }

                if (memories.ContainsKey(memory.id))
                {
                    Warn(asset.name, $"el id {memory.id} ya lo usa otro recuerdo");
                    continue;
                }

                memory.drawings ??= Array.Empty<MemoryDrawing>();
                memories.Add(memory.id, memory);
                ordered.Add(memory);
            }

            ordered.Sort((left, right) => left.id.CompareTo(right.id));
            All = ordered.AsReadOnly();
        }

        public bool TryGet(int id, out MemoryData memory)
        {
            return memories.TryGetValue(id, out memory);
        }

        private static void Warn(string assetName, string reason)
        {
            Debug.LogWarning($"{GameInfo.LogPrefix} Recuerdo '{assetName}' omitido: {reason}");
        }
    }
}
