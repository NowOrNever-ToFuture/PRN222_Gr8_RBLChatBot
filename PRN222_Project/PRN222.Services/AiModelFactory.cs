using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class AiModelFactory
    {
        private readonly IEnumerable<IEmbeddingService> _embeddingServices;

        public AiModelFactory(IEnumerable<IEmbeddingService> embeddingServices)
        {
            _embeddingServices = embeddingServices;
        }

        public IEmbeddingService GetEmbeddingService(string targetModelName)
        {
            var service = _embeddingServices.FirstOrDefault(s => s.ProviderName.Equals(targetModelName, StringComparison.OrdinalIgnoreCase));
            
            if (service == null)
                throw new Exception($"Không tìm thấy cấu hình cho model: {targetModelName}");
                
            return service;
        }
    }
}
