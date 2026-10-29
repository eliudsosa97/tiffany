using UnityEngine;

namespace TiffanyGame
{
    public struct FootContacts
    {
        float previousTime;
        int previousState;

        public int Advance(int state, float normalizedTime, Vector2 contacts)
        {
            if (state != previousState)
            {
                previousState = state;
                previousTime = 0f;
            }
            int count = normalizedTime >= previousTime
                ? Crossings(previousTime, normalizedTime, contacts.x) + Crossings(previousTime, normalizedTime, contacts.y)
                : 0;
            previousTime = normalizedTime;
            return count;
        }

        static int Crossings(float from, float to, float contact) =>
            Mathf.FloorToInt(to - contact) - Mathf.FloorToInt(from - contact);
    }
}
