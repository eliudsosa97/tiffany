using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public sealed partial class WitchDemo
    {
        private WitchController demoWitch;
        private WitchChapterTracker demoTracker;
        private bool trackerWasEnabled;
        private WitchProfile originalProfile;
        private LayerMask originalCoverMask;

        private IEnumerator RunBehavior(PlayerController player, WitchController witch, Vector3 stop)
        {
            if (!IsMode(mode))
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo: modo desconocido {mode}.");
                yield break;
            }
            var soul = player.GetComponent<PlayerSoul>();
            var light = player.GetComponent<PlayerLight>();
            var character = player.GetComponent<CharacterController>();
            if (soul == null || light == null || character == null)
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo necesita alma, luz y movimiento de Tiffany.");
                yield break;
            }
            CandleSpot candle = mode == "candle" ? NearestFamilyCandle(stop) : null;
            if (mode == "candle" && candle == null)
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo candle necesita una veladora real de la familia encendida.");
                yield break;
            }
            Vector3 origin = candle != null
                ? candle.transform.position + Vector3.back * (candle.radius + 3f) : stop + Vector3.back * 12f;
            float encounterDistance = mode == "harass" ? witch.Profile.HarassMinRadius : 15f;
            Vector3 witchOrigin = candle != null
                ? candle.transform.position + Vector3.back * (candle.radius + 15f) : origin + Vector3.right * encounterDistance;
            if (!NavMeshPlacement.TrySample(origin, 4f, NavMesh.AllAreas, out var playerPoint)
                || !NavMeshPlacement.TrySample(witchOrigin, 4f, NavMesh.AllAreas, out var witchPoint)
                || SafeZones.Contains(playerPoint.position) || SafeZones.Contains(witchPoint.position))
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo no encontró NavMesh fuera de veladoras para la conducta.");
                yield break;
            }
            storm = FindAnyObjectByType<Storm>();
            if (storm != null)
            {
                stormWasEnabled = storm.enabled;
                storm.enabled = false;
            }
            demoTracker = FindAnyObjectByType<WitchChapterTracker>();
            if (demoTracker != null)
            {
                trackerWasEnabled = demoTracker.enabled;
                demoTracker.enabled = false;
            }
            soul.Powers.Unlock(Power.Light);
            if (light.State.IsOn) light.Toggle();
            PlacePlayer(player, character, playerPoint.position);
            witch.chapter = Chapter.Stable;
            witch.ResetBrain();
            demoWitch = witch;
            originalProfile = witch.Profile;
            originalCoverMask = witch.coverMask;
            witch.coverMask = 0;
            var profile = originalProfile.Copy();
            profile.SearchEnabled = mode == "search" || mode == "lightsoff";
            profile.LastSeenEnabled = mode == "lightsoff";
            profile.AmbushEnabled = mode == "ambush";
            profile.CandleWatchEnabled = mode == "candle";
            profile.BarkLureEnabled = mode == "bark";
            profile.UsesVoiceLure = mode == "bark";
            profile.CanSprint = false;
            profile.LandingDuration = profile.TakeOffDuration = 1f;
            profile.StalkSpeed = 3f;
            profile.FollowSpeed = 4f;
            profile.LoseTrackTime = 30f;
            profile.SuspicionDuration = profile.HideInspectionDuration = 0.5f;
            profile.SearchDuration = 12f;
            profile.AmbushMinDelay = profile.AmbushMaxDelay = 1f;
            profile.AmbushMinPlayerDistance = 12f;
            profile.AmbushEdgeOffset = 4f;
            profile.AmbushWait = 8f;
            profile.CandleWatchLimit = 6f;
            profile.CandleWatchRetreatDuration = 3f;
            profile.BarkLureMinDelay = profile.BarkLureMaxDelay = 0.5f;
            profile.BarkLureTravelLimit = 5f;
            if (mode == "harass")
            {
                profile.CanSprint = true;
                profile.StalkSpeed = originalProfile.StalkSpeed;
                profile.FollowSpeed = originalProfile.FollowSpeed;
                profile.HarassMinLookDuration = profile.HarassMaxLookDuration = 2f;
                profile.HarassMinArc = profile.HarassMaxArc = 90f;
                profile.HarassMinRadius = profile.HarassMaxRadius = 10f;
                profile.HarassAngleStep = 10f;
            }
            if (mode == "lightsoff") profile.LandingDistance = 1f;
            witch.SetProfile(profile);
            bool airborne = mode == "ambush" || mode == "lightsoff";
            witch.transform.SetPositionAndRotation(witchPoint.position + (airborne ? Vector3.up * 8f : Vector3.zero),
                Quaternion.LookRotation(playerPoint.position - witchPoint.position));
            witch.playerLightOn = witch.playerHidden = witch.playerVanished = witch.hearingMuted = false;
            witch.raining = !airborne;
            if (mode == "ambush") witch.NextClearingOverride = witchPoint.position + Vector3.right * profile.AmbushEdgeOffset;
            if (mode == "bark")
            {
                // La bruja queda detrás de la niña: ningún lado del ladrido la hace pasar junto a ella.
                Vector3 ahead = playerPoint.position - witchPoint.position;
                ahead.y = 0f;
                witch.NextClearingOverride = playerPoint.position + ahead.normalized * 10f;
            }
            FrameBehavior(playerPoint.position, witchPoint.position, candle);
            Physics.SyncTransforms();
            Debug.Log($"{GameInfo.LogPrefix} WitchDemo {mode}: encuentro preparado con perfil de demostración.");
            if (!airborne)
            {
                float deadline = Time.time + 4f;
                while (witch.Decision.State != WitchState.Stalking && Time.time < deadline) yield return null;
                witch.raining = false;
                if (witch.Decision.State != WitchState.Stalking)
                {
                    Debug.LogError($"{GameInfo.LogPrefix} WitchDemo {mode}: no pudo aterrizar.");
                    yield break;
                }
                Debug.Log($"{GameInfo.LogPrefix} WitchDemo {mode}: bruja en el suelo, sin lluvia.");
            }
            if (mode == "harass")
            {
                yield return RunHarass(player, witch, light);
                yield break;
            }
            if (mode == "search")
            {
                NoiseBus.Publish(new StepNoise(player.transform.position, 30f));
                Debug.Log($"{GameInfo.LogPrefix} WitchDemo search: Tiffany hace ruido y se aleja del origen.");
                yield return null;
                yield return null;
                if (!NavMesh.SamplePosition(playerPoint.position + Vector3.left * 10f, out var exit, 4f, NavMesh.AllAreas))
                {
                    Debug.LogError($"{GameInfo.LogPrefix} WitchDemo search: no encontró salida para Tiffany.");
                    yield break;
                }
                PlacePlayer(player, character, exit.position);
            }
            else if (mode == "candle" || mode == "lightsoff")
            {
                light.Toggle();
                Debug.Log($"{GameInfo.LogPrefix} WitchDemo {mode}: Tiffany enciende su luz.");
                float deadline = Time.time + 3f;
                while (!witch.Perception.SeesPlayer && !witch.Perception.SeesPlayerLight && Time.time < deadline) yield return null;
                if (!witch.Perception.SeesPlayer && !witch.Perception.SeesPlayerLight)
                {
                    Debug.LogError($"{GameInfo.LogPrefix} WitchDemo {mode}: la bruja no vio la luz de Tiffany.");
                    yield break;
                }
                light.Toggle();
                if (candle != null)
                {
                    PlacePlayer(player, character, candle.transform.position + Vector3.back * candle.radius * 0.5f);
                    soul.Respawn.Touch(candle.Candle, candle.radius);
                    Debug.Log($"{GameInfo.LogPrefix} WitchDemo candle: Tiffany entra a su veladora de la familia.");
                }
                else
                {
                    if (!NavMesh.SamplePosition(playerPoint.position + Vector3.left * 12f, out var exit, 4f, NavMesh.AllAreas))
                    {
                        Debug.LogError($"{GameInfo.LogPrefix} WitchDemo lightsoff: no encontró salida para Tiffany.");
                        yield break;
                    }
                    PlacePlayer(player, character, exit.position);
                    Debug.Log($"{GameInfo.LogPrefix} WitchDemo lightsoff: Tiffany apaga la luz en pleno vuelo.");
                }
            }
            WitchIntent expected = mode switch
            {
                "search" => WitchIntent.Search,
                "ambush" => WitchIntent.Ambush,
                "candle" => WitchIntent.CandleWatch,
                "bark" => WitchIntent.BarkLure,
                _ => WitchIntent.LastSeen
            };
            float limit = Time.time + 15f;
            while (witch.Decision.Intent != expected && Time.time < limit) yield return null;
            if (witch.Decision.Intent != expected)
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo {mode}: no comenzó la conducta esperada.");
                yield break;
            }
            Debug.Log($"{GameInfo.LogPrefix} WitchDemo {mode}: conducta en marcha.");
            if (mode == "bark")
            {
                limit = Time.time + profile.BarkLureTravelLimit + 1f;
                while (!witch.Decision.VoiceLure && Time.time < limit) yield return null;
                if (witch.Decision.VoiceLure) Debug.Log($"{GameInfo.LogPrefix} WitchDemo bark: imita a Tyler quieta entre los árboles.");
                else Debug.LogError($"{GameInfo.LogPrefix} WitchDemo bark: no llegó a imitar a Tyler.");
                yield break;
            }
            WitchState phase = mode == "ambush" ? WitchState.Stalking
                : mode == "lightsoff" ? WitchState.Landing : WitchState.TakingOff;
            limit = Time.time + (mode == "search" ? profile.SearchDuration + 1f : 12f);
            while (witch.Decision.State != phase && Time.time < limit) yield return null;
            if (witch.Decision.State != phase)
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo {mode}: no completó la siguiente etapa de la conducta.");
                yield break;
            }
            if (mode == "search") Debug.Log($"{GameInfo.LogPrefix} WitchDemo search: termina la búsqueda y despega.");
            if (mode == "candle") Debug.Log($"{GameInfo.LogPrefix} WitchDemo candle: se aleja de la veladora y despega.");
            if (mode == "lightsoff")
            {
                Debug.Log($"{GameInfo.LogPrefix} WitchDemo lightsoff: aterriza en el último punto iluminado.");
                limit = Time.time + profile.LandingDuration + 2f;
                while (witch.Decision.Intent != WitchIntent.Search && Time.time < limit) yield return null;
                if (witch.Decision.Intent == WitchIntent.Search)
                    Debug.Log($"{GameInfo.LogPrefix} WitchDemo lightsoff: busca en espiral desde el punto que recordó.");
                else Debug.LogError($"{GameInfo.LogPrefix} WitchDemo lightsoff: no inició la búsqueda tras aterrizar.");
            }
            if (mode == "ambush")
            {
                Debug.Log($"{GameInfo.LogPrefix} WitchDemo ambush: espera inmóvil, sin fuego ni voz.");
                limit = Time.time + profile.AmbushWait + 1f;
                while (witch.Decision.State != WitchState.TakingOff && Time.time < limit) yield return null;
                if (witch.Decision.State == WitchState.TakingOff)
                    Debug.Log($"{GameInfo.LogPrefix} WitchDemo ambush: Tiffany no llegó y la bruja despega.");
                else Debug.LogError($"{GameInfo.LogPrefix} WitchDemo ambush: no abandonó la espera.");
            }
        }

        private IEnumerator RunHarass(PlayerController player, WitchController witch, PlayerLight light)
        {
            float deadline = Time.time + 19f;
            NoiseBus.Publish(new StepNoise(player.transform.position, 30f));
            yield return null;
            NoiseBus.Publish(new StepNoise(player.transform.position, 30f));
            while (witch.Decision.Intent != WitchIntent.Harass && Time.time < deadline) yield return null;
            if (witch.Decision.Intent != WitchIntent.Harass)
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo harass: no comenzó el acoso.");
                yield break;
            }
            Debug.Log($"{GameInfo.LogPrefix} WitchDemo harass: se detiene y mira a Tiffany.");
            while (witch.Decision.Intent == WitchIntent.Harass && witch.Decision.Speed == 0f && Time.time < deadline)
                yield return null;
            if (witch.Decision.Intent != WitchIntent.Harass || witch.Decision.Speed <= 0f)
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo harass: no empezó a rodear a Tiffany.");
                yield break;
            }
            Debug.Log($"{GameInfo.LogPrefix} WitchDemo harass: empieza a rodearla.");
            yield return new WaitForSeconds(0.5f);
            if (!light.State.IsOn) light.Toggle();
            bool lit = false, hid = false;
            while (witch.Decision.Intent == WitchIntent.Harass && Time.time < deadline)
            {
                Vector3 facing = witch.transform.position - player.transform.position;
                facing.y = 0f;
                GameInput.AimOverride = PlaythroughNavigation.CameraInput(facing, view.transform.eulerAngles.y);
                lit |= witch.Perception.PlayerLightTouchesWitch;
                if (!hid && lit && witch.Decision.Speed == 0f)
                {
                    hid = true;
                    light.Toggle();
                    Debug.Log($"{GameInfo.LogPrefix} WitchDemo harass: se esconde y espera sin moverse.");
                }
                yield return null;
            }
            if (!hid || witch.Decision.State != WitchState.WindUp)
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo harass: no mostró escondite y aviso.");
                yield break;
            }
            Debug.Log($"{GameInfo.LogPrefix} WitchDemo harass: avisa antes de embestir.");
            while (witch.Decision.State == WitchState.WindUp && Time.time < deadline) yield return null;
            if (witch.Decision.State == WitchState.Sprinting)
                Debug.Log($"{GameInfo.LogPrefix} WitchDemo harass: embiste después del acoso.");
            else Debug.LogError($"{GameInfo.LogPrefix} WitchDemo harass: no llegó a la embestida a tiempo.");
        }

        private static CandleSpot NearestFamilyCandle(Vector3 origin)
        {
            CandleSpot nearest = null;
            float distance = float.PositiveInfinity;
            foreach (var candle in FindObjectsByType<CandleSpot>(FindObjectsSortMode.None))
            {
                float candidate = (candle.transform.position - origin).sqrMagnitude;
                if (candle.kind != CandleKind.Family || !candle.Candle.IsLit || candidate >= distance) continue;
                nearest = candle;
                distance = candidate;
            }
            return nearest;
        }

        private static void PlacePlayer(PlayerController player, CharacterController character, Vector3 position)
        {
            character.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.forward));
            character.enabled = true;
            Physics.SyncTransforms();
        }

        private void FrameBehavior(Vector3 player, Vector3 witch, CandleSpot candle)
        {
            follow = view.GetComponent<FollowCamera>();
            if (follow != null)
            {
                followWasEnabled = follow.enabled;
                follow.enabled = false;
            }
            Vector3 midpoint = candle != null ? candle.transform.position : (player + witch) * 0.5f;
            Vector3 cameraPosition = midpoint + new Vector3(0f, 16f, -38f);
            previousFieldOfView = view.fieldOfView;
            view.fieldOfView = 50f;
            view.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(midpoint - cameraPosition));
            fill = new GameObject("Witch Demo Light").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.transform.position = midpoint + new Vector3(0f, 8f, -4f);
            fill.type = LightType.Point;
            fill.range = 45f;
            fill.intensity = 80f;
            fill.color = new Color(0.65f, 0.8f, 1f);
            fill.shadows = LightShadows.None;
        }

        private void RestoreBehavior()
        {
            if (demoTracker != null) demoTracker.enabled = trackerWasEnabled;
            if (demoWitch == null) return;
            demoWitch.NextClearingOverride = null;
            demoWitch.coverMask = originalCoverMask;
            demoWitch.SetProfile(originalProfile);
        }
    }
}
