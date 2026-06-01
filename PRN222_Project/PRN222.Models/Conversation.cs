namespace PRN222.Models
{
    public class Conversation
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastModifiedDate { get; set; }

        // Navigation properties
        public User User { get; set; }
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
