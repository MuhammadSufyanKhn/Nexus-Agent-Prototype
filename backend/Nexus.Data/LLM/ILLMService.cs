using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Data.LLM;

public interface ILLMService
{
    Task<LLMResponse> GenerateCompletionAsync(string prompt, string? systemPrompt = null, bool requestJson = false, CancellationToken cancellationToken = default);
    Task<T?> GenerateJsonAsync<T>(string prompt, string? systemPrompt = null, CancellationToken cancellationToken = default);
    Task<LLMHealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default);
}
