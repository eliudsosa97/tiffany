using System;
using UnityEngine;

namespace TiffanyGame
{
    public enum LitterKind { Leaves, Twig, PineCone, Stone, Root }

    public static class GroundLitter
    {
        public const float ChunkSize = 20f;

        public static float Density(GroundType ground, LitterKind kind) => (ground, kind) switch
        {
            (GroundType.Leaves, LitterKind.Leaves) => 0.22f,
            (GroundType.Leaves, LitterKind.Twig) => 0.025f,
            (GroundType.Leaves, LitterKind.Stone) => 0.009f,
            (GroundType.Leaves, LitterKind.Root) => 0.0008f,
            (GroundType.Branches, LitterKind.Twig) => 0.26f,
            (GroundType.Branches, LitterKind.Leaves) => 0.025f,
            (GroundType.Branches, LitterKind.PineCone) => 0.006f,
            (GroundType.Branches, LitterKind.Root) => 0.001f,
            (GroundType.Branches, LitterKind.Stone) => 0.005f,
            (GroundType.Grass, LitterKind.Leaves) => 0.06f,
            (GroundType.Grass, LitterKind.Twig) => 0.01f,
            (GroundType.Grass, LitterKind.Stone) => 0.04f,
            (GroundType.Gravel, LitterKind.Stone) => 0.24f,
            (GroundType.Gravel, LitterKind.Twig) => 0.01f,
            (GroundType.Dirt, LitterKind.Stone) => 0.04f,
            (GroundType.Dirt, LitterKind.Leaves) => 0.04f,
            (GroundType.Dirt, LitterKind.Twig) => 0.015f,
            (GroundType.Slab, LitterKind.Leaves) => 0.002f,
            (GroundType.Slab, LitterKind.Stone) => 0.001f,
            _ => 0f
        };

        public static float CellSize(LitterKind kind) => kind switch
        {
            LitterKind.Leaves => 1f,
            LitterKind.Twig => 1f,
            LitterKind.PineCone => 2f,
            LitterKind.Stone => 1f,
            LitterKind.Root => 4f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static Vector2Int Chunk(Vector3 position) =>
            new Vector2Int(Mathf.FloorToInt(position.x / ChunkSize), Mathf.FloorToInt(position.z / ChunkSize));

        public static int GravelVariant(Vector3 position)
        {
            var chunk = Chunk(position);
            return 1 + (int)(Hash(LitterKind.Stone, chunk.x, chunk.y, 5) % 3u);
        }

        public readonly struct Candidate
        {
            public readonly LitterKind Kind;
            public readonly Vector3 Position;
            public readonly float Yaw;
            public readonly float Scale;
            public readonly int Variant;
            readonly float threshold;

            internal Candidate(LitterKind kind, Vector3 position, float yaw, float scale, int variant, float threshold)
            {
                Kind = kind;
                Position = position;
                Yaw = yaw;
                Scale = scale;
                Variant = variant;
                this.threshold = threshold;
            }

            public bool IsPresent(GroundType ground) => threshold < Density(ground, Kind) * CellSize(Kind) * CellSize(Kind);
        }

        public static Candidate Sample(LitterKind kind, int cellX, int cellZ)
        {
            float size = CellSize(kind);
            float Value(uint salt) => (Hash(kind, cellX, cellZ, salt) >> 8) * (1f / 16777216f);
            var position = new Vector3((cellX + 0.001f + Value(1) * 0.998f) * size, 0f,
                (cellZ + 0.001f + Value(2) * 0.998f) * size);
            var chunk = Chunk(position);
            int variants = kind == LitterKind.Root || kind == LitterKind.Leaves || kind == LitterKind.Twig ? 3 : 1;
            int variant = (int)(Hash(kind, chunk.x, chunk.y, 5) % (uint)variants);
            float scale = kind switch
            {
                LitterKind.Stone => Mathf.Lerp(0.18f, 0.45f, Value(4)),
                LitterKind.Root => Mathf.Lerp(0.8f, 1.15f, Value(4)),
                LitterKind.Twig => Mathf.Lerp(0.8f, 1.1f, Value(4)),
                LitterKind.PineCone => Mathf.Lerp(0.97f, 1f, Value(4)),
                _ => Mathf.Lerp(0.6f, 1.5f, Value(4))
            };
            return new Candidate(kind, position, Value(3) * 360f, scale, variant, Value(0));
        }

        static uint Hash(LitterKind kind, int x, int z, uint salt)
        {
            unchecked
            {
                uint value = (uint)x * 73856093u ^ (uint)z * 19349663u ^ ((uint)kind + 1u) * 83492791u ^ salt * 2654435761u;
                value = (value ^ (value >> 16)) * 0x7feb352du;
                value = (value ^ (value >> 15)) * 0x846ca68bu;
                return value ^ (value >> 16);
            }
        }
    }
}
