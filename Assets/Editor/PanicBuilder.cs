using UnityEngine;
using static TiffanyGame.EditorTools.AudioBuilder;

namespace TiffanyGame.EditorTools
{
    public static class PanicBuilder
    {
        public static void BuildPanic(Transform spawn, PlayerLight playerLight, TylerController tyler)
        {
            var panic = playerLight.gameObject.AddComponent<PanicController>();
            panic.playerLight = playerLight;
            panic.hiding = playerLight.GetComponent<PlayerHiding>();
            panic.witch = playerLight.witch;
            panic.tyler = tyler;
            panic.cueSource = playerLight.gameObject.AddComponent<AudioSource>();
            panic.cueSource.spatialBlend = 0f;
            panic.cueSource.playOnAwake = false;
            panic.fakeSteps = new[] { LoadClip("step_1"), LoadClip("step_2"), LoadClip("step_3"), LoadClip("step_4") };
            panic.whispers = new[] { LoadClip("whisper_1"), LoadClip("whisper_2"), LoadClip("whisper_3") };
            panic.sobs = new[] { LoadClip("sob_1"), LoadClip("sob_2") };
            var names = new[] { "Panic 1 Steps", "Panic 2 Whispers", "Panic 3 Sob" };
            var levels = new[] { 0.55f, 0.8f, 0.95f };
            for (int i = 0; i < names.Length; i++)
            {
                var stop = new GameObject(names[i]);
                stop.transform.SetParent(spawn.parent, false);
                stop.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                stop.AddComponent<CaptureStop>().wait = i == 1 ? 10.5f : 9f;
                stop.AddComponent<LightCaptureStage>();
                stop.AddComponent<PanicCaptureStage>().level = levels[i];
            }
        }
    }
}
