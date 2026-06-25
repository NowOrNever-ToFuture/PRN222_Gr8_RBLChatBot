using System.Text.Json.Serialization;

namespace PRN222.Services.DTOs
{
    /// <summary>
    /// DTO map với JSON response từ Python Microservice endpoint /api/parse-document.
    /// Cấu trúc: { "filename": "...", "total_chunks": N, "chunks": [...] }
    /// </summary>
    public class PythonParseResponseDto
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonPropertyName("markdown")]
        public string Markdown { get; set; } = string.Empty;

        [JsonPropertyName("total_chunks")]
        public int TotalChunks { get; set; }

        [JsonPropertyName("chunks")]
        public List<PythonChunkDto> Chunks { get; set; } = new();
    }
}
