using System;

namespace ElVelador
{
    // Los campos van en minúscula porque JsonUtility empareja por nombre con el JSON.
    [Serializable]
    public sealed class MemoryData
    {
        public int id;
        public string title;
        public string place;
        public string power;
        public MemoryDrawing[] drawings;
    }

    [Serializable]
    public sealed class MemoryDrawing
    {
        public string image;
        public string phrase;
    }
}
