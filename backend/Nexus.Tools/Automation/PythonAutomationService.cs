using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nexus.Tools.Automation;

public interface IPythonAutomationService
{
    Task<string> ExecuteOperationAsync(string operation, string jsonArgs, CancellationToken cancellationToken = default);
}

public class PythonAutomationService : IPythonAutomationService
{
    private readonly ILogger<PythonAutomationService> _logger;
    private readonly string _pythonExecutable;
    private readonly string _runnerScriptPath;

    public PythonAutomationService(ILogger<PythonAutomationService> logger)
    {
        _logger = logger;

        // Auto-detect python executable (py or python)
        _pythonExecutable = "py";

        // Find automation/runner.py absolute path traversing parent directories
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        string? foundPath = null;
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "automation", "runner.py");
            if (File.Exists(candidate))
            {
                foundPath = candidate;
                break;
            }
            current = current.Parent;
        }

        _runnerScriptPath = foundPath ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "automation", "runner.py"));
    }

    public async Task<string> ExecuteOperationAsync(string operation, string jsonArgs, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new ArgumentException("Operation name cannot be empty.", nameof(operation));
        }

        // Security Guard: Sanitize operation to prevent arbitrary shell injection
        var sanitizedOperation = operation.Replace(" ", "").Replace(";", "").Replace("&", "").Replace("|", "");

        var psi = new ProcessStartInfo
        {
            FileName = _pythonExecutable,
            Arguments = $"\"{_runnerScriptPath}\" {sanitizedOperation} \"{jsonArgs.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        _logger.LogInformation("Executing Python automation operation '{Operation}' via {Executable}...", sanitizedOperation, _pythonExecutable);

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // 10s process timeout limit

            await process.WaitForExitAsync(cts.Token);
            process.WaitForExit(); // Flush redirected stdout/stderr buffers

            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            if (!string.IsNullOrWhiteSpace(output))
            {
                return output;
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError("Python script execution failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                return $"{{\"status\": \"error\", \"message\": \"Python process failed: {error.Replace("\"", "'")}\"}}";
            }

            return $"{{\"status\": \"success\", \"operation\": \"{sanitizedOperation}\"}}";
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            _logger.LogWarning("Python automation operation '{Operation}' timed out after 10 seconds.", sanitizedOperation);
            return $"{{\"status\": \"error\", \"message\": \"Python execution timed out after 10 seconds.\"}}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Python automation operation '{Operation}'", sanitizedOperation);
            return $"{{\"status\": \"error\", \"message\": \"Failed to execute Python process: {ex.Message.Replace("\"", "'")}\"}}";
        }
    }
}
