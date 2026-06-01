namespace PRN222.Models
{
    public class Course
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public DateTime CreatedDate { get; set; }

        // Navigation properties
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<TestQuestion> TestQuestions { get; set; } = new List<TestQuestion>();
    }
}
