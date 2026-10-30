using System;
using System.IO;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class BarnBuilder
    {
        [Serializable]
        sealed class Layout
        {
            public ModelLibrary.BoxSpec[] walls, bales, loft, fence;
            public ModelLibrary.BoxSpec trough;
        }

        public static void Build(Transform clearing, Material crate)
        {
            var barn = new GameObject("Barn").transform;
            barn.SetParent(clearing, false);
            barn.localPosition = new Vector3(0f, 0f, 5f);
            var model = ModelLibrary.Instantiate("barn", barn).transform;
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(ModelLibrary.SpecPath("barn")));
            AddBoxes(model.Find("BarnWalls"), "Wall", layout.walls);
            AddBoxes(model.Find("BarnInterior"), "Bale", layout.bales);
            AddBoxes(model.Find("BarnInterior"), "Loft", layout.loft);
            AddBoxes(model.Find("BarnInterior"), "Trough", new[] { layout.trough });
            AddBoxes(model.Find("BarnFence"), "Fence", layout.fence);
            var roof = model.Find("BarnRoof");
            roof.GetComponent<Renderer>().enabled = false;
            var cover = new GameObject("Roof").AddComponent<BoxCollider>();
            cover.transform.SetParent(roof, false);
            cover.center = new Vector3(0f, 5.2f, 0f);
            cover.size = new Vector3(15f, 0.4f, 11f);
            model.Find("BarnMud").gameObject.AddComponent<GroundSurface>().type = GroundType.Dirt;
            HidingBuilder.BuildHideSpot(clearing, "Hide Stable Crate", new Vector3(-4.5f, 0f, 7f), crate, PrimitiveType.Cube);
        }

        static void AddBoxes(Transform parent, string name, ModelLibrary.BoxSpec[] boxes)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                var part = new GameObject($"{name} {i + 1}");
                part.transform.SetParent(parent, false);
                var collider = part.AddComponent<BoxCollider>();
                collider.center = boxes[i].center;
                collider.size = boxes[i].size;
            }
        }
    }
}
