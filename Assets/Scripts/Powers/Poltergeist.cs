using UnityEngine;

namespace TiffanyGame
{
    [RequireComponent(typeof(PlayerSoul))]
    public sealed class Poltergeist : MonoBehaviour
    {
        public Light handLight;

        private PlayerSoul soul;
        private PlayerController controller;

        private void Awake()
        {
            soul = GetComponent<PlayerSoul>();
            TryGetComponent(out controller);
        }

        private void Update()
        {
            if (GameInput.PoltergeistPressed) TryMove();
        }

        public bool TryMove()
        {
            Vector3 position = transform.position;
            Vector3 forward = transform.forward;
            MovableObject target = null;
            float bestAngle = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;
            var objects = MovableObject.Active;
            for (int i = 0; i < objects.Count; i++)
            {
                MovableObject candidate = objects[i];
                if (candidate.Used) continue;
                Vector3 targetPosition = candidate.transform.position;
                float distance = Vector3.Distance(position, targetPosition);
                if (distance > PowerRules.MoveRange) continue;
                bool illuminated = (handLight != null && handLight.isActiveAndEnabled
                    && PoltergeistTargeting.InSpot(handLight.transform.position, handLight.transform.forward,
                        handLight.range, handLight.spotAngle, targetPosition)) || SafeZones.Contains(targetPosition);
                if (!illuminated) continue;
                float angle = PoltergeistTargeting.AimAngle(position, forward, targetPosition);
                if (target == null || PoltergeistTargeting.IsBetter(angle, distance, bestAngle, bestDistance))
                {
                    target = candidate;
                    bestAngle = angle;
                    bestDistance = distance;
                }
            }

            if (target == null) return false;
            bool raining = controller != null && controller.raining;
            if (!PowerRules.TryMove(soul.Powers, soul.Energy, target.Throwable,
                bestDistance, true, raining, out float radius)) return false;
            target.Move(position, radius);
            return true;
        }
    }
}
