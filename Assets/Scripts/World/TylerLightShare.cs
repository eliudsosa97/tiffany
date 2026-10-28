using System;

namespace TiffanyGame
{
    public enum ShareMode { LightOnly, SlowRecharge }

    public sealed class TylerLightShare
    {
        public const float SlowRechargeRate = 2f;
        public const float SlowRechargeCap = 25f;

        private bool sharing;

        public bool Tick(float dt, ShareMode mode, bool energyEmpty, bool tylerNear,
            bool rechargingAtCandle, float currentEnergy, out float recharge)
        {
            recharge = 0f;
            if (!tylerNear || rechargingAtCandle
                || (mode == ShareMode.SlowRecharge && currentEnergy >= SlowRechargeCap))
                return sharing = false;

            if (energyEmpty) sharing = true;
            if (!sharing || mode == ShareMode.LightOnly) return sharing;
            if (dt > 0f && !float.IsInfinity(dt))
                recharge = Math.Min(SlowRechargeRate * dt, SlowRechargeCap - currentEnergy);
            if (currentEnergy + recharge >= SlowRechargeCap) sharing = false;
            return sharing;
        }
    }
}
