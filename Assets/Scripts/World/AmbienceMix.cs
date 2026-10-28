using UnityEngine;
namespace TiffanyGame
{
    public enum AmbienceLayer { Crickets, Wind, Leaves, Frogs, Lake }
    public enum AmbienceCall { Owl, BarnOwl, Coyote, Howl }
    public sealed class AmbienceMix
    {
        public const float Fade = 0.5f, WitchNear = 8f, WitchFar = 25f, PanicThreshold = 0.75f, PanicVolume = 0.3f;
        public const float SilenceEvery = 55f, SilenceLength = 7f, SilenceVolume = 0.15f, CallBaseVolume = 0.5f;
        public const float FirstInterval = 14f, SecondInterval = 22f, ThirdInterval = 18f, FourthInterval = 26f;
        public const int LayerCount = 5, ClearingCount = 8, HowlVariants = 3;
        public const float CemeteryCrickets = 0.6f, CemeteryWind = 0.35f, CemeteryLeaves = 0.3f;
        public const float PlantCrickets = 0.3f, PlantWind = 0.5f, PlantLeaves = 0.15f;
        public const float TracksCrickets = 0.25f, TracksWind = 0.75f;
        public const float StableCrickets = 0.4f, StableWind = 0.15f, StableLeaves = 0.5f;
        public const float HutWind = 0.2f;
        public const float LakeCrickets = 0.2f, LakeWind = 0.45f, LakeFrogs = 0.6f, LakeWater = 0.6f;
        public const float RoadCrickets = 0.3f, RoadWind = 0.7f;
        public const float HouseCrickets = 0.5f, HouseWind = 0.2f, HouseLeaves = 0.2f;
        private static readonly float[,] Profiles =
        {
            { CemeteryCrickets, CemeteryWind, CemeteryLeaves, 0f, 0f },
            { PlantCrickets, PlantWind, PlantLeaves, 0f, 0f },
            { TracksCrickets, TracksWind, 0f, 0f, 0f },
            { StableCrickets, StableWind, StableLeaves, 0f, 0f },
            { 0f, HutWind, 0f, 0f, 0f },
            { LakeCrickets, LakeWind, 0f, LakeFrogs, LakeWater },
            { RoadCrickets, RoadWind, 0f, 0f, 0f },
            { HouseCrickets, HouseWind, HouseLeaves, 0f, 0f }
        };
        private static readonly AmbienceCall[][] Calls =
        {
            new[] { AmbienceCall.Owl }, new[] { AmbienceCall.Coyote },
            new[] { AmbienceCall.Coyote, AmbienceCall.Howl }, new[] { AmbienceCall.BarnOwl },
            new[] { AmbienceCall.Howl }, new[] { AmbienceCall.Owl },
            new[] { AmbienceCall.Coyote, AmbienceCall.Howl }, new[] { AmbienceCall.Owl }
        };
        private static readonly float[] Intervals = { FirstInterval, SecondInterval, ThirdInterval, FourthInterval };
        private readonly float[] volumes = new float[LayerCount];
        private readonly int[] rotations = new int[ClearingCount];
        private double clock, nextCall = FirstInterval;
        private int intervalIndex, howlIndex;
        public bool Call { get; private set; }
        public AmbienceCall CallKind { get; private set; }
        public int CallVariant { get; private set; }
        public float CallVolume { get; private set; }
        public float Volume(AmbienceLayer layer) => volumes[(int)layer];
        public void Tick(float dt, int clearing, float witchDistance, float panic)
        {
            (Call, CallVolume) = (false, 0f);
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            clock += dt;
            int profile = clearing >= 0 && clearing < ClearingCount ? clearing : 0;
            bool silent = clock >= SilenceEvery && clock % SilenceEvery < SilenceLength;
            float witch = Mathf.Clamp01((witchDistance - WitchNear) / (WitchFar - WitchNear));
            float factor = witch * (panic >= PanicThreshold ? PanicVolume : 1f) * (silent ? SilenceVolume : 1f);
            for (int i = 0; i < volumes.Length; i++)
                volumes[i] = Mathf.MoveTowards(volumes[i], Profiles[profile, i] * factor, Fade * dt);
            if (clock < nextCall) return;
            do
            {
                intervalIndex = (intervalIndex + 1) % Intervals.Length;
                nextCall += Intervals[intervalIndex];
            } while (nextCall <= clock);
            if (witchDistance < WitchFar || panic >= PanicThreshold || silent) return;
            var choices = Calls[profile];
            CallKind = choices[rotations[profile]];
            rotations[profile] = (rotations[profile] + 1) % choices.Length;
            CallVariant = CallKind == AmbienceCall.Howl ? howlIndex : 0;
            if (CallKind == AmbienceCall.Howl) howlIndex = (howlIndex + 1) % HowlVariants;
            (Call, CallVolume) = (true, CallBaseVolume * witch);
        }
    }
}
