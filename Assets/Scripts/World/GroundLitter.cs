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
            (GroundType.Leaves, LitterKind.Leaves) => 0.275f,
            (GroundType.Leaves, LitterKind.Twig) => 0.03125f,
            (GroundType.Leaves, LitterKind.Stone) => 0.01125f,
            (GroundType.Leaves, LitterKind.Root) => 0.001f,
            (GroundType.Branches, LitterKind.Twig) => 0.325f,
            (GroundType.Branches, LitterKind.Leaves) => 0.03125f,
            (GroundType.Branches, LitterKind.PineCone) => 0.0075f,
            (GroundType.Branches, LitterKind.Root) => 0.00125f,
            (GroundType.Branches, LitterKind.Stone) => 0.00625f,
            (GroundType.Grass, LitterKind.Leaves) => 0.075f,
            (GroundType.Grass, LitterKind.Twig) => 0.0125f,
            (GroundType.Grass, LitterKind.Stone) => 0.05f,
            (GroundType.Gravel, LitterKind.Stone) => 0.3f,
            (GroundType.Gravel, LitterKind.Twig) => 0.0125f,
            (GroundType.Dirt, LitterKind.Stone) => 0.05f,
            (GroundType.Dirt, LitterKind.Leaves) => 0.05f,
            (GroundType.Dirt, LitterKind.Twig) => 0.01875f,
            (GroundType.Slab, LitterKind.Leaves) => 0.0025f,
            (GroundType.Slab, LitterKind.Stone) => 0.00125f,
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

        public static float PatchMask(LitterKind kind, Vector3 position)
        {
            float x = (position.x + (int)kind * 0.8f) / 10f;
            float z = (position.z + (int)kind * 1.1f) / 10f;
            float noise = ValueNoise(x, z, 8) * 0.85f + ValueNoise(x * 2f, z * 2f, 9) * 0.15f;
            return Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.64f, noise)));
        }

        static float ValueNoise(float x, float z, uint salt)
        {
            int cellX = Mathf.FloorToInt(x), cellZ = Mathf.FloorToInt(z);
            float u = Mathf.SmoothStep(0f, 1f, x - cellX), v = Mathf.SmoothStep(0f, 1f, z - cellZ);
            float Value(int dx, int dz) => (Hash(LitterKind.Leaves, cellX + dx, cellZ + dz, salt) >> 8) * (1f / 16777216f);
            return Mathf.Lerp(Mathf.Lerp(Value(0, 0), Value(1, 0), u), Mathf.Lerp(Value(0, 1), Value(1, 1), u), v);
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

            public bool IsPresent(GroundType ground) =>
                threshold < Density(ground, Kind) * CellSize(Kind) * CellSize(Kind) * PatchMask(Kind, Position);
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
