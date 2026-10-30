using System;
using System.IO;
using Unity.AI.Navigation;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class WitchHutBuilder
    {
        [Serializable]
        sealed class Layout
        {
            public ModelLibrary.BoxSpec[] walls;
            public ModelLibrary.BoxSpec[] fence;
            public ModelLibrary.BoxSpec[] table;
        }

        public static void Build(Transform clearing)
        {
            var shelter = new GameObject("Hut").transform;
            shelter.SetParent(clearing, false);
            shelter.localPosition = new Vector3(0f, 0f, 5f);
            var model = ModelLibrary.Instantiate("witch-hut", shelter).transform;
            model.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var spec = ModelLibrary.LoadSpec("witch-hut");
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(ModelLibrary.SpecPath("witch-hut")));
            AddBoxes(model.Find("HutWalls"), "Wall", layout.walls);
            AddBoxes(model.Find("HutFence"), "Fence", layout.fence);
            AddBoxes(model.Find("HutInterior"), "Table", layout.table);
            var volume = HidingBuilder.BuildHideSpot(model, "Hide Hut Table",
                new Vector3(spec.hideVolume.center.x, 0f, spec.hideVolume.center.z), null);
            var trigger = volume.GetComponent<BoxCollider>();
            trigger.center = Vector3.up * spec.hideVolume.center.y;
            trigger.size = spec.hideVolume.size;

            var roof = model.Find("HutRoof");
            roof.gameObject.layer = LayerMask.NameToLayer(ForestLayout.CoverLayer);
            roof.gameObject.AddComponent<MeshCollider>().sharedMesh = roof.GetComponent<MeshFilter>().sharedMesh;
            roof.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var hearth = new GameObject("Hearth Light").transform;
            hearth.SetParent(model, false);
            hearth.localPosition = spec.lightOffset;
            var light = hearth.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.22f, 0.06f);
            light.range = 8f;
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;

            var inside = new GameObject("Hut Inside").transform;
            inside.SetParent(model, false);
            inside.localPosition = new Vector3(-0.45f, 0f, 0.25f);
            inside.localRotation = Quaternion.Euler(0f, 180f, 0f);
            inside.gameObject.AddComponent<CaptureStop>().overrideCamera = false;
            inside.gameObject.AddComponent<LightCaptureStage>().energy = SoulEnergy.Max;
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
