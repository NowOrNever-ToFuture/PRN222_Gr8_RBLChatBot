namespace PRN222.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; }

        // Navigation properties
        public ICollection<CourseLecturer> TeachingAssignments { get; set; } = new List<CourseLecturer>();
        public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
        public ICollection<Document> UploadedDocuments { get; set; } = new List<Document>();
    }
}
