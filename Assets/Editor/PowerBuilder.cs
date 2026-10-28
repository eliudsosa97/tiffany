using System;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class PowerBuilder
    {
        public static void BuildPowers(PlayerLight playerLight, Transform zoneRoot, Storm storm)
        {
            var soul = playerLight.GetComponent<PlayerSoul>();
            var powers = new[] { Power.Candles, Power.Poltergeist, Power.Vanish };
            for (int i = 0; i < powers.Length; i++)
            {
                if (Array.IndexOf(soul.startingPowers, powers[i]) >= 0) continue;
                int count = soul.startingPowers.Length;
                Array.Resize(ref soul.startingPowers, count + 1);
                soul.startingPowers[count] = powers[i];
            }
            playerLight.gameObject.AddComponent<Poltergeist>().handLight = playerLight.spot;
            var vanish = playerLight.gameObject.AddComponent<VanishPower>();
            vanish.playerLight = playerLight;
            vanish.witch = playerLight.witch;
            var renderers = playerLight.GetComponentsInChildren<Renderer>(true);
            int figureCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Transform candidate = renderers[i].transform;
                if (candidate.IsChildOf(playerLight.glow) || candidate.IsChildOf(playerLight.spot.transform)) continue;
                renderers[figureCount++] = renderers[i];
            }
            Array.Resize(ref renderers, figureCount);
            vanish.figure = renderers;

            var candles = new CandleSpot[2];
            Physics.SyncTransforms();
            for (int i = 0; i < candles.Length; i++)
            {
                Vector3 position = playerLight.transform.position
                    + playerLight.transform.right * (i == 0 ? -1.5f : 1.5f) - playerLight.transform.forward;
                if (Physics.Raycast(position + Vector3.up * 20f, Vector3.down, out var ground,
                    Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) position.y = ground.point.y;
                candles[i] = SceneBuilder.CreateCandle(zoneRoot, $"Kindled Candle {i + 1}", position,
                    CandleKind.Kindled, false, soul, storm);
            }
            playerLight.gameObject.AddComponent<CandleKindler>().candles = candles;

            Vector3 origin = playerLight.transform.position + Vector3.forward * 3.5f;
            BuildObject(zoneRoot, ThrowableKind.Bottle, origin + Vector3.left * 2.4f,
                0.16f, 0.32f, 0.4f, new Color(0.08f, 0.24f, 0.13f));
            BuildObject(zoneRoot, ThrowableKind.Can, origin + Vector3.left * 0.8f,
                0.14f, 0.2f, 0.15f, new Color(0.55f, 0.58f, 0.6f));
            BuildObject(zoneRoot, ThrowableKind.Pot, origin + Vector3.right * 0.8f,
                0.3f, 0.3f, 2f, new Color(0.56f, 0.25f, 0.14f));
            BuildObject(zoneRoot, ThrowableKind.Bell, origin + Vector3.right * 2.4f,
                0.18f, 0.18f, 0.3f, new Color(0.7f, 0.53f, 0.2f));

            var names = new[]
            {
                "Power 1 Before", "Power 2 After", "Power 3 Vanish Fading", "Power 4 Vanish Hidden",
                "Power 5 Candle Before", "Power 6 Candle Lit"
            };
            for (int i = 0; i < names.Length; i++)
            {
                var stop = new GameObject(names[i]);
                stop.transform.SetParent(zoneRoot, false);
                stop.transform.SetPositionAndRotation(playerLight.transform.position, playerLight.transform.rotation);
                var capture = stop.AddComponent<CaptureStop>();
                capture.overrideCamera = false;
                capture.wait = i == 2 ? 0.2f : 1.5f;
                var stage = stop.AddComponent<LightCaptureStage>();
                stage.energy = SoulEnergy.Max;
                stage.hideWitch = true;
                if (i > 0 && i != 4)
                {
                    var powerStage = stop.AddComponent<PowerCaptureStage>();
                    powerStage.action = i == 5 ? PowerCaptureAction.Kindle
                        : i >= 2 ? PowerCaptureAction.Vanish : PowerCaptureAction.MoveObject;
                }
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
