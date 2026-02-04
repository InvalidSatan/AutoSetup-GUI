using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AutoSetupGUI.Infrastructure;

/// <summary>
/// Helper class for running external processes with output capture.
/// </summary>
public class ProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Runs a process and returns the result.
    /// </summary>
    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments = "",
        string? workingDirectory = null,
        int timeoutMs = 120000,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting process: {FileName} {Arguments}", fileName, arguments);

        var result = new ProcessResult
        {
            FileName = fileName,
            Arguments = arguments,
            StartTime = DateTime.Now
        };

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                result.TimedOut = true;
                _logger.LogWarning("Process timed out after {Timeout}ms: {FileName}", timeoutMs, fileName);
            }

            result.EndTime = DateTime.Now;
            result.ExitCode = process.ExitCode;
            result.StandardOutput = outputBuilder.ToString();
            result.StandardError = errorBuilder.ToString();
            result.Success = result.ExitCode == 0 && !result.TimedOut;

            _logger.LogDebug("Process completed: {FileName} with exit code {ExitCode}", fileName, result.ExitCode);

            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.Now;
            result.Exception = ex;
            result.Success = false;

            _logger.LogError(ex, "Error running process: {FileName}", fileName);

            return result;
        }
    }

    /// <summary>
    /// Runs a process with elevated privileges (UAC prompt may appear).
    /// </summary>
    public async Task<ProcessResult> RunElevatedAsync(
        string fileName,
        string arguments = "",
        int timeoutMs = 120000)
    {
        _logger.LogDebug("Starting elevated process: {FileName} {Arguments}", fileName, arguments);

        var result = new ProcessResult
        {
            FileName = fileName,
            Arguments = arguments,
            StartTime = DateTime.Now
        };

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas"
            };

            process.Start();

            // When using ShellExecute, we can't capture output
            // and WaitForExitAsync doesn't work the same way
            var completed = await Task.Run(() => process.WaitForExit(timeoutMs));

            result.EndTime = DateTime.Now;

            if (!completed)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                result.TimedOut = true;
            }

            result.ExitCode = process.ExitCode;
            result.Success = result.ExitCode == 0 && !result.TimedOut;

            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.Now;
            result.Exception = ex;
            result.Success = false;

            _logger.LogError(ex, "Error running elevated process: {FileName}", fileName);

            return result;
        }
    }

    /// <summary>
    /// Runs a command via cmd.exe.
    /// </summary>
    public Task<ProcessResult> RunCommandAsync(
        string command,
        int timeoutMs = 120000,
        CancellationToken cancellationToken = default)
    {
        return RunAsync("cmd.exe", $"/c {command}", timeoutMs: timeoutMs, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Runs a process with real-time output callback for streaming progress updates.
    /// </summary>
    public async Task<ProcessResult> RunWithRealtimeOutputAsync(
        string fileName,
        string arguments = "",
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        string? workingDirectory = null,
        int timeoutMs = 120000,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting process with realtime output: {FileName} {Arguments}", fileName, arguments);

        var result = new ProcessResult
        {
            FileName = fileName,
            Arguments = arguments,
            StartTime = DateTime.Now
        };

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    try { onOutputLine?.Invoke(e.Data); }
                    catch { /* Ignore callback errors */ }
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                    try { onErrorLine?.Invoke(e.Data); }
                    catch { /* Ignore callback errors */ }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                result.TimedOut = true;
                _logger.LogWarning("Process timed out after {Timeout}ms: {FileName}", timeoutMs, fileName);
            }

            result.EndTime = DateTime.Now;
            result.ExitCode = process.ExitCode;
            result.StandardOutput = outputBuilder.ToString();
            result.StandardError = errorBuilder.ToString();
            result.Success = result.ExitCode == 0 && !result.TimedOut;

            _logger.LogDebug("Process completed: {FileName} with exit code {ExitCode}", fileName, result.ExitCode);

            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.Now;
            result.Exception = ex;
            result.Success = false;

            _logger.LogError(ex, "Error running process: {FileName}", fileName);

            return result;
        }
    }
}

/// <summary>
/// Result of running a process.
/// </summary>
public class ProcessResult
{
    public string FileName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool TimedOut { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public Exception? Exception { get; set; }

    public string CombinedOutput => string.IsNullOrEmpty(StandardError)
        ? StandardOutput
        : $"{StandardOutput}\n[STDERR]:\n{StandardError}";
}
