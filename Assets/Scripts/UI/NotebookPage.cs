namespace TiffanyGame
{
    public readonly struct NotebookPage
    {
        public int MemoryId { get; }
        public string Title { get; }
        public string Image { get; }
        public string Phrase { get; }
        public int Number { get; }

        public NotebookPage(int memoryId, string title, string image, string phrase, int number)
        {
            MemoryId = memoryId;
            Title = title;
            Image = image;
            Phrase = phrase;
            Number = number;
        }
    }
}
