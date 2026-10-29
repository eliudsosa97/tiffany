using System;
using System.Collections;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class OcclusionCaptureStage : MonoBehaviour
    {
        [Serializable]
        public struct Cover
        {
            public Bounds bounds;
            public Collider collider;

            public bool Intersects(Vector3 origin, Vector3 target)
            {
                var ray = new Ray(origin, target - origin);
                float distance = Vector3.Distance(origin, target) - FollowCamera.OcclusionFocusMargin;
                return collider != null ? collider.Raycast(ray, out _, distance)
                    : bounds.IntersectRay(ray, out float entry) && entry > 0f && entry < distance;
            }
        }

        static readonly int DisabledId = Shader.PropertyToID("_TiffanyOcclusionDisabled");
        public bool after;
        public Cover[] covers = Array.Empty<Cover>();
        bool applied;
        float previousDisabled;
        float timeScale;
        PlayerController movement;
        bool movementEnabled;
        LightCaptureStage lightStage;
        FollowCamera framing;
        bool followEnabled;

        public IEnumerator Prepare(Transform player, FollowCamera follow)
        {
            Apply();
            movement = player.GetComponent<PlayerController>();
            movementEnabled = movement != null && movement.enabled;
            if (movement != null) movement.enabled = false;
            framing = follow;
            followEnabled = follow != null && follow.enabled;
            GetComponent<CaptureStop>().ApplyPose(player, follow);
            if (follow != null) follow.enabled = false;
            lightStage = GetComponent<LightCaptureStage>();
            if (lightStage != null) lightStage.Apply(player);
            yield return null;
            yield return null;
            if (follow != null) follow.PublishOcclusionFocus();
        }

        public void Apply()
        {
            Clear();
            previousDisabled = Shader.GetGlobalFloat(DisabledId);
            timeScale = Time.timeScale;
            applied = true;
            Shader.SetGlobalFloat(DisabledId, after ? 0f : 1f);
            Time.timeScale = 0f;
        }

        public void Clear()
        {
            if (!applied) return;
            applied = false;
            if (lightStage != null) lightStage.Clear();
            lightStage = null;
            if (movement != null) movement.enabled = movementEnabled;
            movement = null;
            Shader.SetGlobalFloat(DisabledId, previousDisabled);
            Time.timeScale = timeScale;
            if (framing != null) framing.enabled = followEnabled;
            framing = null;
        }

        void OnDisable() => Clear();
    }
}
