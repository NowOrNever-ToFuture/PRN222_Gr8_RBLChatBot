namespace PRN222.Models
{
    public class Message
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public string Role { get; set; } // "User" or "Assistant"
        public string Content { get; set; }
        public string CitedChunkIds { get; set; } // Comma-separated list of Guid strings
        public DateTime CreatedDate { get; set; }

        // Navigation property
        public Conversation Conversation { get; set; }
    }
}
