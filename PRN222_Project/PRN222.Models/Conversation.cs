namespace PRN222.Models
{
    public class Conversation
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        // Mỗi hội thoại gắn với MỘT môn học: history/retrieval không trộn môn.
        public Guid? CourseId { get; set; }
        public string Title { get; set; }
        // Hội thoại được ghim luôn hiển thị đầu danh sách sidebar
        public bool IsPinned { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastModifiedDate { get; set; }

        // Navigation properties
        public User User { get; set; }
        public Course? Course { get; set; }
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
