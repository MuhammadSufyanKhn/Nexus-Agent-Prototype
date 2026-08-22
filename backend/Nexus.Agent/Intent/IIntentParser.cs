using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Agent.Intent;

public interface IIntentParser
{
    Task<ParsedIntentResult> ParseIntentAsync(string userPrompt, CancellationToken cancellationToken = default);
}
