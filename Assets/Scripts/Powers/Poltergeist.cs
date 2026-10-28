using UnityEngine;

namespace TiffanyGame
{
    [RequireComponent(typeof(PlayerSoul))]
    public sealed class Poltergeist : MonoBehaviour
    {
        public Light handLight;
        public LayerMask occluderMask = Physics.DefaultRaycastLayers;

        private PlayerSoul soul;
        private PlayerController controller;
        private RaycastHit[] hits;

        private void Awake()
        {
            soul = GetComponent<PlayerSoul>();
            TryGetComponent(out controller);
            hits = new RaycastHit[32];
        }

        private void Update()
        {
            if (Time.timeScale == 0f) return;
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
                bool illuminated = SafeZones.Contains(targetPosition) || (handLight != null && handLight.isActiveAndEnabled
                    && PoltergeistTargeting.InSpot(handLight.transform.position, handLight.transform.forward,
                        handLight.range, handLight.spotAngle, targetPosition) && HasLineOfSight(candidate));
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

        private bool HasLineOfSight(MovableObject target)
        {
            Vector3 origin = handLight.transform.position;
            Vector3 direction = target.transform.position - origin;
            int count = Physics.RaycastNonAlloc(origin, direction, hits,
                direction.magnitude, occluderMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = hits[i].collider.transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(target.transform)) continue;
                return false;
            }
            return true;
        }
    }
}
