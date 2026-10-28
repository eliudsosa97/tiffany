using System;
using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class NotebookPagesTests
    {
        NotebookPages pages;
        [SetUp] public void SetUp() => pages = new NotebookPages();

        [Test]
        public void EmptyNotebookRejectsCurrentAndIgnoresNavigation()
        {
            AssertEmpty();
            Assert.That(pages.Add(null), Is.False);
            Assert.Throws<InvalidOperationException>(() => { var page = pages.Current; });
            pages.Next();
            pages.Previous();
            pages.ShowFirstOf(1);
            AssertEmpty();
        }

        [Test]
        public void OrdersByMemoryIdThenDrawingOrder()
        {
            Assert.That(pages.Add(Memory(2)), Is.True);
            Assert.That(pages.Add(Memory(1)), Is.True);
            pages.ShowFirstOf(1);
            Assert.That((pages.Count, pages.IsEmpty), Is.EqualTo((4, false)));
            for (int i = 0; i < 4; i++)
            {
                AssertPage(i / 2 + 1, i % 2 + 1, i);
                pages.Next();
            }
        }

        [TestCase(true), TestCase(false)]
        public void RejectedMemoryDoesNotAddOrReplacePages(bool nullMemory)
        {
            pages.Add(Memory(1));
            pages.Next();
            var duplicate = Memory(1);
            duplicate.title = "Duplicado de prueba";
            Assert.That(pages.Add(nullMemory ? null : duplicate), Is.False);
            Assert.That(pages.Count, Is.EqualTo(2));
            AssertPage(1, 2, 1);
        }

        [TestCase(true), TestCase(false)]
        public void MemoryWithoutDrawingsIsAddedOnlyOnce(bool nullDrawings)
        {
            var memory = Memory(1);
            memory.drawings = nullDrawings ? null : Array.Empty<MemoryDrawing>();
            Assert.That(pages.Add(memory), Is.True);
            Assert.That(pages.Add(Memory(1)), Is.False);
            AssertEmpty();
        }

        [TestCase(true), TestCase(false)]
        public void NavigationStopsAtBothEnds(bool forward)
        {
            pages.Add(Memory(1));
            pages.Next();
            Action navigate = forward ? pages.Next : pages.Previous;
            navigate();
            navigate();
            AssertPage(1, forward ? 2 : 1, forward ? 1 : 0);
        }

        [TestCase(2, 1, 0, 2), TestCase(2, 1, 1, 2), TestCase(1, 2, 1, 0)]
        public void AddingMemoryKeepsCurrentDrawing(int currentId, int addedId, int index, int offset)
        {
            pages.Add(Memory(currentId));
            if (index == 1) pages.Next();
            pages.Add(Memory(addedId));
            AssertPage(currentId, index + 1, index + offset);
        }

        [TestCase(2, 0, 2), TestCase(2, 3, 2), TestCase(1, 2, 0)]
        public void ShowFirstOfSelectsFirstDrawing(int memoryId, int startIndex, int expectedIndex)
        {
            pages.Add(Memory(1));
            pages.Add(Memory(2));
            for (int i = 0; i < startIndex; i++) pages.Next();
            pages.ShowFirstOf(memoryId);
            AssertPage(memoryId, 1, expectedIndex);
        }

        [TestCase(1), TestCase(99)]
        public void MissingOrEmptyMemoryKeepsCurrentPage(int memoryId)
        {
            pages.Add(Memory(2));
            pages.Next();
            pages.Add(new MemoryData { id = 1, title = "Sin dibujos" });
            pages.ShowFirstOf(memoryId);
            Assert.That(pages.Count, Is.EqualTo(2));
            AssertPage(2, 2, 1);
        }

        void AssertEmpty() => Assert.That((pages.Count, pages.IsEmpty, pages.Index), Is.EqualTo((0, true, 0)));
        void AssertPage(int id, int drawing, int index)
        {
            Assert.That(pages.Index, Is.EqualTo(index));
            Assert.That(pages.Current, Is.EqualTo(new NotebookPage(id, $"Recuerdo de prueba {id}",
                $"memory-test-{id}-{drawing}", $"Dibujo de prueba {drawing}", index + 1)));
        }
        static MemoryData Memory(int id) => new MemoryData
        {
            id = id, title = $"Recuerdo de prueba {id}",
            drawings = new[]
            {
                new MemoryDrawing { image = $"memory-test-{id}-1", phrase = "Dibujo de prueba 1" },
                new MemoryDrawing { image = $"memory-test-{id}-2", phrase = "Dibujo de prueba 2" }
            }
        };
    }
}
