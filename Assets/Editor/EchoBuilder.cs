using System;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class EchoBuilder
    {
        public static void Build(MemorySpot spot, Transform parent)
        {
            string path = $"Assets/Data/Echoes/echo-{spot.memoryId}.json";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta {path}.");
            if (!EchoData.TryLoad(asset.text, out var data, out string error))
                throw new InvalidOperationException($"{GameInfo.LogPrefix} {path}: {error}.");
            if (data.id != spot.memoryId) throw new InvalidOperationException($"{GameInfo.LogPrefix} {path}: id distinto del MemorySpot.");
            var player = spot.gameObject.AddComponent<EchoPlayer>();
            spot.echoPlayer = player;
            player.echoAsset = asset;
            var audioRoot = Root($"Echo {spot.memoryId}", parent);
            int count = 0;
            player.clips = new AudioClip[data.sounds.Length];
            for (int i = 0; i < data.sounds.Length; i++)
            {
                string clipPath = $"Assets/Audio/Sfx/{data.sounds[i].clip}.wav";
                player.clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (player.clips[i] == null) throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta el audio {clipPath}.");
                count += 1 + data.sounds[i].copies.Length;
            }
            player.sources = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                var source = Root($"Echo Voice {i + 1}", audioRoot).gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.ignoreListenerPause = true;
                player.sources[i] = source;
            }
        }

        static Transform Root(string name, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }
    }
}
