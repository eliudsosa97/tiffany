using System;

namespace TiffanyGame
{
    public sealed class RouteGuidance
    {
        public const float OffRouteDistance = 10f;
        public const float StalledSeconds = 20f;
        public const float CooldownSeconds = 30f;
        public const float NoticeSeconds = 4f;
        public const float ProgressDistance = 1f;

        int clearing = -1;
        float bestDistance = float.PositiveInfinity;
        float stalled;
        float cooldown;
        float remaining;

        public bool IsVisible => remaining > 0f;

        public static bool CanRemind(InterfaceScreen screen, bool echo, bool chasing, bool inChallenge,
            MemoryProgress progress) =>
            screen == InterfaceScreen.Playing && !echo && !chasing && !inChallenge
                && GameplayObjective.IsFollowingBirds(progress);

        public bool Tick(float dt, float distanceToRoute, int visitedClearing, float distanceToNext, bool canRemind)
        {
            dt = Math.Max(0f, dt);
            cooldown = Math.Max(0f, cooldown - dt);
            remaining = Math.Max(0f, remaining - dt);
            bool changedClearing = clearing != visitedClearing;
            if (changedClearing || !canRemind)
            {
                clearing = visitedClearing;
                bestDistance = distanceToNext;
                stalled = remaining = 0f;
                return false;
            }

            if (bestDistance - distanceToNext >= ProgressDistance)
            {
                bestDistance = distanceToNext;
                stalled = remaining = 0f;
            }
            else stalled += dt;

            if (cooldown > 0f || (distanceToRoute <= OffRouteDistance && stalled < StalledSeconds)) return false;
            cooldown = CooldownSeconds;
            remaining = NoticeSeconds;
            return true;
        }
    }
}
