using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class ForestLayout
    {
        public const string CoverLayer = "Cover";

        public readonly struct Placement
        {
            public readonly string id;
            public readonly float x, alongSlope, yaw, scale, height;
            readonly float radius;
            public float Radius => scale * radius;
            public bool IsKenney => height > 0f;
            public bool IsPine => id.StartsWith("Nature/tree_pine", StringComparison.Ordinal);

            public Placement(string id, float x, float alongSlope, float yaw, float scale, float radius, float height = 0f)
            {
                this.id = id;
                this.x = x;
                this.alongSlope = alongSlope;
                this.yaw = yaw;
                this.scale = scale;
                this.radius = radius;
                this.height = height;
            }
        }

        public static readonly IReadOnlyList<Placement> Placements = Array.AsReadOnly(new[]
        {
            new Placement("Nature/tree_pineTallA_detailed", -24f, 4f, 25f, 0.9f, 2f, 10f),
            new Placement("Nature/tree_pineTallC_detailed", 24f, 5f, 140f, 1f, 2.3f, 10f),
            new Placement("Nature/tree_pineRoundC", 0f, 20f, 270f, 1.2f, 3f, 10f),
            new Placement("oak", -13f, 20f, 35f, 1f, 4.3f),
            new Placement("oak", 12f, 22f, 210f, 0.85f, 4.3f),
            new Placement("bougainvillea", -7.5f, 5f, 160f, 0.85f, 1.9f),
            new Placement("bougainvillea", 7.5f, 8f, 200f, 1.15f, 1.9f),
            new Placement("Nature/rock_largeA", -7.5f, 1.5f, 40f, 0.8f, 1.6f, 0.6f),
            new Placement("Nature/rock_smallC", 7.5f, 1.5f, 220f, 1.2f, 1.1f, 0.5f),
            new Placement("Nature/stone_tallA", 5.7f, 18f, 110f, 1f, 0.9f, 1.4f),
            new Placement("Nature/log", -23f, 20f, 65f, 1f, 1.8f, 0.8f),
            new Placement("Nature/stump_oldTall", 23f, 20f, 190f, 0.9f, 0.6f, 1.4f)
        });

        public static bool IsClear(float x, float alongSlope, float radius)
        {
            if (radius < 0f || Mathf.Abs(x) + radius > 28f || Mathf.Abs(alongSlope) + radius > 28f)
                return false;
            if (x * x + alongSlope * alongSlope <= (4f + radius) * (4f + radius))
                return false;
            for (int column = -1; column <= 1; column++)
            for (int row = -1; row <= 1; row += 2)
            {
                float dx = Mathf.Max(Mathf.Abs(x - column * 15f) - 5f, 0f);
                float dz = Mathf.Max(Mathf.Abs(alongSlope - row * 9f) - 5f, 0f);
                if (dx * dx + dz * dz <= radius * radius) return false;
            }
            return true;
        }
    }
}
