using Microsoft.AspNetCore.Http;

namespace PRN222.Services.DTOs
{
    public class UploadDocumentDTO
    {
        public Guid CourseId { get; set; }
        public IFormFile File { get; set; }
    }
}

