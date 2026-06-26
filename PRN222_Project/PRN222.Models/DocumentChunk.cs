namespace PRN222.Models
{
    public class DocumentChunk
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string Content { get; set; }
        public string VectorData { get; set; } // NVARCHAR(MAX) for vector embeddings
        public int PageNumber { get; set; }
        public int ChunkIndex { get; set; }

        // Navigation property
        public Document Document { get; set; }
    }
}
