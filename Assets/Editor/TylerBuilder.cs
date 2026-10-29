using System;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class TylerBuilder
    {
        public static Light BuildShareLight(Transform parent)
        {
            var light = CreateRoot("Tyler Share Light", parent, Vector3.up * 0.8f).AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.65f, 0.8f, 1f);
            light.range = 4f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
            // En WebGL, Tyler tiene menor prioridad que veladoras y fuego de la bruja al exceder cuatro luces por objeto.
            light.renderMode = LightRenderMode.ForceVertex;
            light.enabled = false;
            return light;
        }

        public static GameObject BuildFigure(Transform parent, out Transform head)
        {
            var figure = ModelLibrary.Instantiate("tyler", parent);
            head = Array.Find(figure.GetComponentsInChildren<Transform>(true), bone => bone.name == "Head");
            if (head == null) throw new InvalidOperationException($"{GameInfo.LogPrefix} El modelo de Tyler no tiene el hueso Head.");
            return figure;
        }

        static GameObject CreateRoot(string name, Transform parent, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            return root;
        }
    }
}
