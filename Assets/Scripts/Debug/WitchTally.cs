using System;
using System.Collections.Generic;

namespace TiffanyGame
{
    [Flags]
    public enum WitchTallyEvent
    {
        None = 0,
        Suspicion = 1,
        Chase = 2,
        Sprint = 4,
        Catch = 8
    }

    public sealed class WitchTally
    {
        readonly int[,] counts;
        WitchState previousState = WitchState.Flying;
        bool previousChasing;

        public WitchTally(int clearingCount)
        {
            if (clearingCount < 0) throw new ArgumentOutOfRangeException(nameof(clearingCount));
            counts = new int[clearingCount + 1, 4];
        }

        public WitchTallyEvent Observe(int clearing, WitchState state, bool chasing)
        {
            var events = state == previousState ? WitchTallyEvent.None : state switch
            {
                WitchState.Suspicious => WitchTallyEvent.Suspicion,
                WitchState.Sprinting => WitchTallyEvent.Sprint,
                WitchState.Caught => WitchTallyEvent.Catch,
                _ => WitchTallyEvent.None
            };
            if (chasing && !previousChasing) events |= WitchTallyEvent.Chase;
            int bucket = Bucket(clearing);
            for (int i = 0; i < 4; i++)
                if ((events & (WitchTallyEvent)(1 << i)) != 0) counts[bucket, i]++;
            previousState = state;
            previousChasing = chasing;
            return events;
        }

        public int Suspicions(int clearing) => counts[Bucket(clearing), 0];
        public int Chases(int clearing) => counts[Bucket(clearing), 1];
        public int Sprints(int clearing) => counts[Bucket(clearing), 2];
        public int Catches(int clearing) => counts[Bucket(clearing), 3];
        public int TotalSuspicions => Total(0);
        public int TotalChases => Total(1);
        public int TotalSprints => Total(2);
        public int TotalCatches => Total(3);

        int Bucket(int clearing) => clearing >= 0 && clearing < counts.GetLength(0) - 1
            ? clearing : counts.GetLength(0) - 1;

        int Total(int column)
        {
            int total = 0;
            for (int i = 0; i < counts.GetLength(0); i++) total += counts[i, column];
            return total;
        }

        public IEnumerable<string> SummaryLines(string[] clearingNames)
        {
            int unknown = counts.GetLength(0) - 1;
            for (int i = 0; i < unknown; i++)
                yield return SummaryLine(clearingNames[i], counts[i, 0], counts[i, 1], counts[i, 2], counts[i, 3]);
            if (counts[unknown, 0] + counts[unknown, 1] + counts[unknown, 2] + counts[unknown, 3] > 0)
                yield return SummaryLine("sin claro", counts[unknown, 0], counts[unknown, 1], counts[unknown, 2], counts[unknown, 3]);
            yield return SummaryLine("totales", TotalSuspicions, TotalChases, TotalSprints, TotalCatches);
        }

        static string SummaryLine(string name, int suspicions, int chases, int sprints, int catches) =>
            $"{name}: sospechas {suspicions}, cazas {chases}, embestidas {sprints}, atrapadas {catches}";
    }
}
