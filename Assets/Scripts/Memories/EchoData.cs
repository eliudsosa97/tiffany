using System;
using UnityEngine;

namespace TiffanyGame
{
    [Serializable]
    public sealed class EchoData
    {
        public int id;
        public float duration;
        public EchoTrack girl;
        public EchoTrack tyler;
        public EchoSubtitle[] subtitles;
        public EchoSound[] sounds;
        public EchoProp[] props;
        public EchoBlackout blackout;

        public static bool TryLoad(string json, out EchoData data, out string error)
        {
            data = null;
            try
            {
                data = JsonUtility.FromJson<EchoData>(json);
            }
            catch (Exception exception)
            {
                error = $"JSON inválido: {exception.Message}";
                return false;
            }
            if (data == null)
            {
                error = "el JSON está vacío";
                return false;
            }
            return EchoValidation.Check(data, out error);
        }
    }

    [Serializable]
    public class EchoTrack
    {
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
        public float appear;
        public float disappear;
        public EchoPoint[] points = Array.Empty<EchoPoint>();
    }

    [Serializable]
    public sealed class EchoPoint
    {
        public float time;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
    }

    [Serializable]
    public sealed class EchoProp : EchoTrack
    {
        public string type;
        public string parent;
    }

    [Serializable]
    public sealed class EchoSubtitle
    {
        public float time;
        public float duration;
        public string text;
    }

    [Serializable]
    public sealed class EchoSound
    {
        public string clip;
        public float time;
        public float duration;
        public float pitch;
        public float volume;
        public float panStereo;
        public float fadeIn;
        public float fadeOut;
        public EchoSoundCopy[] copies = Array.Empty<EchoSoundCopy>();
    }

    [Serializable]
    public sealed class EchoSoundCopy
    {
        public float delay;
        public float volume;
        public float panStereo;
        public float pitch;
    }

    [Serializable]
    public sealed class EchoBlackout
    {
        public float time;
        public float duration;
    }
}
