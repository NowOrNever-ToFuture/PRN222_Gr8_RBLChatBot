namespace PRN222.Models
{
    public class CourseLecturer
    {
        public Guid Id { get; set; }
        public Guid CourseId { get; set; }
        public Guid LecturerId { get; set; }

        public Course Course { get; set; }
        public User Lecturer { get; set; }
    }
}
