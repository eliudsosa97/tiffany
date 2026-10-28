using System.Collections.Generic;

namespace TiffanyGame
{
    public sealed class TimedLine
    {
        readonly struct Line
        {
            public readonly string Text;
            public readonly float Seconds;

            public Line(string text, float seconds)
            {
                Text = text;
                Seconds = seconds;
            }
        }

        readonly Queue<Line> queue;
        float remaining;

        public string Text { get; private set; }
        public bool IsVisible => Text != null;
        public int Version { get; private set; }

        public TimedLine()
        {
            queue = new Queue<Line>();
        }

        public void Enqueue(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text) || seconds <= 0f) return;
            if (IsVisible)
            {
                queue.Enqueue(new Line(text, seconds));
                return;
            }
            SetText(text);
            remaining = seconds;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            while (IsVisible && dt >= remaining)
            {
                dt -= remaining;
                if (queue.Count == 0)
                {
                    remaining = 0f;
                    SetText(null);
                    return;
                }
                var next = queue.Dequeue();
                remaining = next.Seconds;
                SetText(next.Text);
            }
            if (IsVisible) remaining -= dt;
        }

        public void Clear()
        {
            queue.Clear();
            remaining = 0f;
            SetText(null);
        }

        void SetText(string text)
        {
            if (Text == text) return;
            Text = text;
            Version++;
        }
    }
}
