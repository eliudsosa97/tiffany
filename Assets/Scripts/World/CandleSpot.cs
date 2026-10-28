using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public sealed class CandleSpot : MonoBehaviour
    {
        public CandleKind kind = CandleKind.Family;
        public bool startLit = true;
        public float radius = CandleSite.DefaultRadius;
        public PlayerSoul player;
        public Storm storm;
        public Light flame;
        public Renderer flameMesh;

        public CandleSite Site { get; private set; }
        public Candle Candle => Site.Candle;

        private NavMeshObstacle obstacle;
        private int zoneHandle;

        private void Awake()
        {
            Site = new CandleSite(new Candle(kind, transform.position, startLit), radius);
            obstacle = GetComponent<NavMeshObstacle>();
            Synchronize();
        }

        private void OnEnable() => Synchronize();

        private void OnDisable()
        {
            SafeZones.Remove(zoneHandle);
            zoneHandle = 0;
        }

        private void Update()
        {
            Site.Tick(Time.deltaTime, storm != null && storm.raining,
                player != null ? player.transform.position : Vector3.positiveInfinity,
                player != null ? player.Energy : null, player != null ? player.Respawn : null);
            if (Site.Recharging && player != null) player.RegisterCandleRecharge();
            Synchronize();
        }

        private void Synchronize()
        {
            bool safe = Candle.IsSafeZone;
            if (safe && zoneHandle == 0) zoneHandle = SafeZones.Add(transform.position, radius);
            else if (!safe && zoneHandle != 0)
            {
                SafeZones.Remove(zoneHandle);
                zoneHandle = 0;
            }
            if (obstacle != null) obstacle.enabled = safe;
            if (flame != null) flame.enabled = Candle.IsLit;
            if (flameMesh != null && flameMesh.enabled != Candle.IsLit) flameMesh.enabled = Candle.IsLit;
        }
    }
}
