namespace PRN222.Models
{
    // Subject has been renamed to Course
    // This file is kept for backward compatibility
    [Obsolete("Use Course instead of Subject")]
    public class Subject
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public DateTime CreatedDate { get; set; }

        // Navigation properties
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}

