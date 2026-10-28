using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class ForestInstanceData : ScriptableObject
    {
        [Serializable]
        public sealed class Batch
        {
            public Mesh mesh;
            public Material material;
            public Matrix4x4[] matrices;
            public Bounds bounds;
        }

        public Batch[] batches = Array.Empty<Batch>();
    }
}
