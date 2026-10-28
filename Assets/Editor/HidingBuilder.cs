using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class HidingBuilder
    {
        public static HideVolume BuildHideTest(Transform zone, PlayerController player)
        {
            Vector3 position = player.transform.position + Vector3.right * 4f;
            Physics.SyncTransforms();
            if (Physics.Raycast(position + Vector3.up * 30f, Vector3.down, out var hit,
                60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) position = hit.point;
            var bush = new GameObject("Hide Test");
            bush.transform.SetParent(zone, false);
            bush.transform.position = position;
            bush.layer = LayerMask.NameToLayer("Ignore Raycast");
            var collider = bush.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.6f, 1.8f, 1.6f);
            collider.center = Vector3.up * 0.9f;
            collider.isTrigger = true;
            var volume = bush.AddComponent<HideVolume>();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Bush";
            visual.transform.SetParent(bush.transform, false);
            visual.transform.localPosition = Vector3.up * 0.7f;
            visual.transform.localScale = new Vector3(1.8f, 1.4f, 1.8f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = GeneratedMaterials.Get("HideBush", new Color(0.08f, 0.18f, 0.09f));
            var hiding = player.gameObject.AddComponent<PlayerHiding>();
            hiding.body = player.GetComponentsInChildren<Renderer>();
            var stop = new GameObject("Stop 10 Hidden");
            stop.transform.SetParent(zone, false);
            stop.transform.SetPositionAndRotation(position, player.transform.rotation);
            stop.AddComponent<CaptureStop>();
            var stage = stop.AddComponent<HideCaptureStage>();
            stage.player = hiding;
            stage.volume = volume;
            return volume;
        }
    }
}
