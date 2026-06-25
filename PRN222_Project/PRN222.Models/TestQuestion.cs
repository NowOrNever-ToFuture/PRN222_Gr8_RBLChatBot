namespace PRN222.Models
{
    public class TestQuestion
    {
        public Guid Id { get; set; }
        public Guid CourseId { get; set; }
        public string QuestionText { get; set; }
        public string AnswerOptions { get; set; } // JSON format: {"A": "option1", "B": "option2", ...}
        public string GroundTruth { get; set; }
        public string Explanation { get; set; }
        public int Difficulty { get; set; } // 1: Easy, 2: Medium, 3: Hard
        public DateTime CreatedDate { get; set; }

        // Navigation properties
        public Course Course { get; set; }
        public ICollection<BenchmarkResult> BenchmarkResults { get; set; } = new List<BenchmarkResult>();
    }
}
