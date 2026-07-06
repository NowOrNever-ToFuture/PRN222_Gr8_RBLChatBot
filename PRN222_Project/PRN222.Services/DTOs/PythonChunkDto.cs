using System.Text.Json.Serialization;

namespace PRN222.Services.DTOs
{
    /// <summary>
    /// DTO map với từng chunk trong mảng "chunks" của Python response.
    /// Cấu trúc: { "chunk_index": 0, "content": "text markdown", "vector": [0.1, 0.2, ...] }
    /// </summary>
    public class PythonChunkDto
    {
        [JsonPropertyName("chunk_index")]
        public int ChunkIndex { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("vector")]
        public float[] Vector { get; set; } = Array.Empty<float>();
    }
}
