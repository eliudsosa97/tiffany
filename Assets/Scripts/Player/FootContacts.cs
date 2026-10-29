using UnityEngine;

namespace TiffanyGame
{
    public struct FootContacts
    {
        float previousTime;
        int previousState;

        public static Vector2 Reversed(Vector2 contacts) =>
            new Vector2(Mathf.Repeat(1f - contacts.x, 1f), Mathf.Repeat(1f - contacts.y, 1f));

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
