namespace PRN222.Models
{
    public class Document
    {
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadDate { get; set; }
        public Guid CourseId { get; set; }
        public Guid OwnerId { get; set; } // User who uploaded
        public string Status { get; set; } = "Pending";
        public bool IsIndexed { get; set; } = false;

        // Navigation properties
        public Course Course { get; set; }
        public User Owner { get; set; }
        public ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();
    }
}
