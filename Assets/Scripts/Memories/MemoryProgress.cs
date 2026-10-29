using System;
using System.Collections.Generic;

namespace TiffanyGame
{
    public sealed class MemoryProgress
    {
        public const int BallId = 100;

        readonly List<int> order = new List<int>();
        readonly HashSet<int> found = new HashSet<int>();

        public int FoundCount => found.Count;
        public bool AllFound => FoundCount == order.Count;

        public MemoryProgress(IReadOnlyList<int> order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            for (int i = 0; i < order.Count; i++)
                if (order[i] > 0 && !this.order.Contains(order[i])) this.order.Add(order[i]);
        }

        public bool MarkFound(int id) => order.Contains(id) && found.Add(id);

        public bool IsFound(int id) => found.Contains(id);

        public void Clear() => found.Clear();

        public bool TryGetNext(out int id)
        {
            for (int i = 0; i < order.Count; i++)
            {
                if (IsFound(order[i])) continue;
                id = order[i];
                return true;
            }
            id = 0;
            return false;
        }
    }
}
