using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public static class RiverBankLayout
    {
        public readonly struct Piece
        {
            public readonly Vector2 Start;
            public readonly Vector2 Direction;
            public readonly float Length;
            public readonly bool Mirror;

            public Piece(Vector2 start, Vector2 direction, float length, bool mirror)
            {
                Start = start;
                Direction = direction;
                Length = length;
                Mirror = mirror;
            }

            public Vector2 Middle => Start + Direction * (Length * 0.5f);
        }

        public static List<Piece> Pieces(RouteTerrain terrain, float pieceLength, float crossingGap)
        {
            var pieces = new List<Piece>();
            var crossing = new Vector2(terrain.Crossing.x, terrain.Crossing.z);
            for (int sign = -1; sign <= 1; sign += 2)
            for (int i = 1; i < terrain.RiverPointCount; i++)
            {
                Vector2 a = terrain.RiverBankPoint(i - 1, sign), b = terrain.RiverBankPoint(i, sign);
                float length = Vector2.Distance(a, b);
                var direction = (b - a) / length;
                float along = Vector2.Dot(crossing - a, direction);
                bool crosses = along > 0f && along < length
                    && Mathf.Abs(Cross(direction, crossing - a)) < RouteTerrain.RiverWidth;
                if (crosses)
                {
                    Fill(a, direction, 0f, along - crossingGap * 0.5f, sign, pieces, pieceLength);
                    Fill(a, direction, along + crossingGap * 0.5f, length, sign, pieces, pieceLength);
                }
                else Fill(a, direction, 0f, length, sign, pieces, pieceLength);
            }
            return pieces;
        }

        static void Fill(Vector2 origin, Vector2 direction, float from, float to, int sign, List<Piece> pieces, float pieceLength)
        {
            float span = to - from;
            if (span < pieceLength * 0.3f) return;
            int count = Mathf.Max(1, Mathf.RoundToInt(span / pieceLength));
            float length = span / count;
            for (int i = 0; i < count; i++)
                pieces.Add(new Piece(origin + direction * (from + i * length), direction, length, sign > 0));
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
