using System;
using UnityEngine;

namespace TiffanyGame
{
    public static class EchoValidation
    {
        public static bool Check(EchoData data, out string error)
        {
            error = null;
            if (data == null) return Fail("falta el eco", out error);
            if (data.id < 1 || data.id > 5) return Fail("id fuera de 1 a 5", out error);
            if (!Range(data.duration, 10f, 15f)) return Fail("duración fuera de 10 a 15 segundos", out error);
            if (!Track(data.girl, data.duration, "niña", out error) ||
                !Track(data.tyler, data.duration, "Tyler", out error)) return false;
            data.subtitles ??= Array.Empty<EchoSubtitle>();
            data.sounds ??= Array.Empty<EchoSound>();
            data.props ??= Array.Empty<EchoProp>();
            float previous = 0f;
            for (int i = 0; i < data.subtitles.Length; i++)
            {
                var line = data.subtitles[i];
                if (line == null || !Range(line.time, previous, data.duration) ||
                    !Range(line.duration, 0.001f, data.duration - line.time) || string.IsNullOrWhiteSpace(line.text))
                    return Fail($"subtítulo {i}: texto vacío, tiempos desordenados o ventana inválida", out error);
                previous = line.time + line.duration;
            }
            previous = 0f;
            for (int i = 0; i < data.sounds.Length; i++)
            {
                var sound = data.sounds[i];
                if (sound == null || !ClipName(sound.clip)) return Fail($"sonido {i}: falta el nombre del clip sin extensión", out error);
                if (!Range(sound.time, previous, data.duration) || !Range(sound.duration, 0.001f, data.duration - sound.time))
                    return Fail($"sonido {i}: tiempos desordenados o duración inválida", out error);
                if (!Voice(sound.pitch, sound.volume, sound.panStereo) ||
                    !Range(sound.fadeIn, 0f, sound.duration) || !Range(sound.fadeOut, 0f, sound.duration))
                    return Fail($"sonido {i}: pitch, volumen, paneo o fundido inválido", out error);
                previous = sound.time;
                sound.copies ??= Array.Empty<EchoSoundCopy>();
                float delay = 0f;
                for (int j = 0; j < sound.copies.Length; j++)
                {
                    var copy = sound.copies[j];
                    if (copy == null || !Range(copy.delay, delay, data.duration - sound.time - sound.duration) ||
                        !Voice(copy.pitch, copy.volume, copy.panStereo))
                        return Fail($"sonido {i}, copia {j}: retrasos desordenados o parámetros inválidos", out error);
                    delay = copy.delay;
                }
            }
            for (int i = 0; i < data.props.Length; i++)
            {
                var prop = data.props[i];
                if (prop == null || !PropType(prop.type)) return Fail($"utilería {i}: tipo desconocido", out error);
                if (!string.IsNullOrEmpty(prop.parent) && prop.parent != "girl" && prop.parent != "tyler")
                    return Fail($"utilería {i}: parent debe ser girl, tyler o vacío", out error);
                if (!Track(prop, data.duration, $"utilería {i}", out error)) return false;
            }
            if (data.blackout != null && (!Range(data.blackout.time, 0f, data.duration) ||
                !Range(data.blackout.duration, 0f, data.duration - data.blackout.time)))
                return Fail("corte a negro fuera de la duración del eco", out error);
            return true;
        }

        static bool Track(EchoTrack track, float duration, string name, out string error)
        {
            error = null;
            if (track == null) return Fail($"falta la pista de {name}", out error);
            if (!Range(track.appear, 0f, duration) || !Range(track.disappear, track.appear, duration))
                return Fail($"{name}: ventana de aparición inválida", out error);
            if (!Transform(track.position, track.rotation, track.scale)) return Fail($"{name}: transformación inválida", out error);
            track.points ??= Array.Empty<EchoPoint>();
            float previous = -1f;
            for (int i = 0; i < track.points.Length; i++)
            {
                var point = track.points[i];
                if (point == null || !Range(point.time, 0f, duration) || point.time <= previous)
                    return Fail($"{name}: tiempos desordenados en el punto {i}", out error);
                if (!Transform(point.position, point.rotation, point.scale)) return Fail($"{name}: punto {i} inválido", out error);
                previous = point.time;
            }
            return true;
        }

        static bool Transform(Vector3 position, Vector3 rotation, Vector3 scale) =>
            Finite(position) && Finite(rotation) && Finite(scale) && scale.x > 0f && scale.y > 0f && scale.z > 0f;

        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Range(float value, float min, float max) => Finite(value) && value >= min && value <= max;
        static bool Voice(float pitch, float volume, float pan) => Range(pitch, 0.01f, 3f) && Range(volume, 0f, 1f) && Range(pan, -1f, 1f);

        static bool ClipName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int i = 0; i < value.Length; i++)
                if (!char.IsLetterOrDigit(value[i]) && value[i] != '_' && value[i] != '-') return false;
            return true;
        }

        static bool PropType(string type) => type == "grave" || type == "flowers" || type == "ball" ||
            type == "gate" || type == "ribbon" || type == "rail" || type == "doll" || type == "mud" ||
            type == "door" || type == "shoe" || type == "hut" || type == "bracelet" || type == "beads" || type == "hand" || type == "arm";

        static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
