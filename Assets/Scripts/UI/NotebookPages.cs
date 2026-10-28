using System;
using System.Collections.Generic;

namespace TiffanyGame
{
    public sealed class NotebookPages
    {
        readonly List<NotebookPage> pages = new List<NotebookPage>();
        readonly HashSet<int> memoryIds = new HashSet<int>();

        public int Count => pages.Count;
        public bool IsEmpty => Count == 0;
        public int Index { get; private set; }
        public NotebookPage Current => IsEmpty
            ? throw new InvalidOperationException("El cuaderno está vacío.")
            : pages[Index];

        public bool Add(MemoryData memory)
        {
            if (memory == null || !memoryIds.Add(memory.id)) return false;
            if (memory.drawings == null || memory.drawings.Length == 0) return true;

            int insertion = 0;
            while (insertion < Count && pages[insertion].MemoryId < memory.id) insertion++;
            if (!IsEmpty && insertion <= Index) Index += memory.drawings.Length;

            for (int i = 0; i < memory.drawings.Length; i++)
            {
                var drawing = memory.drawings[i];
                pages.Insert(insertion + i, new NotebookPage(memory.id, memory.title,
                    drawing?.image, drawing?.phrase, insertion + i + 1));
            }

            for (int i = insertion + memory.drawings.Length; i < Count; i++)
            {
                var page = pages[i];
                pages[i] = new NotebookPage(page.MemoryId, page.Title, page.Image, page.Phrase, i + 1);
            }
            return true;
        }

        public void Next()
        {
            if (Index + 1 < Count) Index++;
        }

        public void Previous()
        {
            if (Index > 0) Index--;
        }

        public void ShowFirstOf(int memoryId)
        {
            for (int i = 0; i < Count; i++)
            {
                if (pages[i].MemoryId != memoryId) continue;
                Index = i;
                return;
            }
        }
    }
}
