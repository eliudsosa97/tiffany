using System;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class PowerBuilder
    {
        public static void BuildPowers(PlayerLight playerLight, Transform zoneRoot)
        {
            var soul = playerLight.GetComponent<PlayerSoul>();
            if (Array.IndexOf(soul.startingPowers, Power.Poltergeist) < 0)
            {
                int count = soul.startingPowers.Length;
                Array.Resize(ref soul.startingPowers, count + 1);
                soul.startingPowers[count] = Power.Poltergeist;
            }
            playerLight.gameObject.AddComponent<Poltergeist>().handLight = playerLight.spot;

            Vector3 origin = playerLight.transform.position + Vector3.forward * 4.5f;
            BuildObject(zoneRoot, ThrowableKind.Bottle, origin + Vector3.left * 2.4f,
                0.16f, 0.32f, 0.4f, new Color(0.08f, 0.24f, 0.13f));
            BuildObject(zoneRoot, ThrowableKind.Can, origin + Vector3.left * 0.8f,
                0.14f, 0.2f, 0.15f, new Color(0.55f, 0.58f, 0.6f));
            BuildObject(zoneRoot, ThrowableKind.Pot, origin + Vector3.right * 0.8f,
                0.3f, 0.3f, 2f, new Color(0.56f, 0.25f, 0.14f));
            BuildObject(zoneRoot, ThrowableKind.Bell, origin + Vector3.right * 2.4f,
                0.18f, 0.18f, 0.3f, new Color(0.7f, 0.53f, 0.2f));

            var names = new[] { "Power 1 Before", "Power 2 After" };
            for (int i = 0; i < names.Length; i++)
            {
                var stop = new GameObject(names[i]);
                stop.transform.SetParent(zoneRoot, false);
                stop.transform.SetPositionAndRotation(playerLight.transform.position, playerLight.transform.rotation);
                var capture = stop.AddComponent<CaptureStop>();
                capture.overrideCamera = false;
                capture.wait = 1.5f;
                var stage = stop.AddComponent<LightCaptureStage>();
                stage.energy = SoulEnergy.Max;
                stage.hideWitch = true;
                if (i == 1) stop.AddComponent<PowerCaptureStage>();
            }
        }

        static void BuildObject(Transform parent, ThrowableKind kind, Vector3 position,
            float width, float height, float mass, Color color)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(position + Vector3.up * 20f, Vector3.down, out var ground,
                Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) position.y = ground.point.y;
            position.y += height * 0.5f;

            bool sphere = kind == ThrowableKind.Bell;
            var instance = GameObject.CreatePrimitive(sphere ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
            instance.name = kind.ToString();
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
            instance.transform.localScale = new Vector3(width, sphere ? height : height * 0.5f, width);
            instance.GetComponent<Renderer>().sharedMaterial = GeneratedMaterials.Get($"Movable{kind}", color);
            var body = instance.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            instance.AddComponent<MovableObject>().kind = kind;
        }
    }
}
