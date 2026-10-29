using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchWatch
    {
        public const float SearchSeconds = 6f;
        public Vector3 Post { get; }
        public Vector3 Target { get; private set; }
        public bool Investigating { get; private set; }
        public bool RequiresVanish { get; }
        float remaining;

        public WitchWatch(Vector3 post, bool requiresVanish)
        {
            Post = Target = post;
            RequiresVanish = requiresVanish;
        }

        public void Hear(Vector3 position)
        {
            if (Vector3.Distance(position, Post) < 3f) return;
            Target = position;
            remaining = SearchSeconds;
            Investigating = true;
        }

        public void Tick(float dt, Vector3 position)
        {
            if (!Investigating || Vector3.Distance(position, Target) > 1f) return;
            remaining -= Mathf.Max(0f, dt);
            if (remaining > 0f) return;
            Investigating = false;
            Target = Post;
        }

        public bool PassageClear(Vector3 position) => Investigating && Vector3.Distance(position, Post) >= 3.5f;
    }
}
