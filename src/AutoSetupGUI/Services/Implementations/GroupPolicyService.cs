using AutoSetupGUI.Infrastructure;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaskStatus = AutoSetupGUI.Models.TaskStatus;

namespace AutoSetupGUI.Services.Implementations;

/// <summary>
/// Service for Group Policy update operations.
/// </summary>
public class GroupPolicyService : IGroupPolicyService
{
    private readonly ILogger<GroupPolicyService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ProcessRunner _processRunner;

    public GroupPolicyService(
        ILogger<GroupPolicyService> logger,
        IConfiguration configuration,
        ProcessRunner processRunner)
    {
        _logger = logger;
        _configuration = configuration;
        _processRunner = processRunner;
    }

    public async Task<TaskResult> UpdateGroupPolicyAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Group Policy update...");

        var result = new TaskResult
        {
            TaskId = "group_policy",
            TaskName = "Group Policy Update",
            StartTime = DateTime.Now,
            Status = TaskStatus.Running
        };

        var maxRetries = _configuration.GetValue("GroupPolicy:MaxRetries", 2);
        var retryDelay = _configuration.GetValue("GroupPolicy:RetryDelaySeconds", 10);
        var forceArg = _configuration.GetValue("GroupPolicy:ForceArgument", "/force");

        for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.Status = TaskStatus.Error;
                result.Message = "Operation cancelled";
                break;
            }

            try
            {
                progress?.Report(attempt == 1
                    ? "Updating Group Policy..."
                    : $"Retrying Group Policy update (attempt {attempt})...");

                _logger.LogInformation("Running gpupdate {ForceArg} (attempt {Attempt}/{MaxAttempts})",
                    forceArg, attempt, maxRetries + 1);

                var processResult = await _processRunner.RunAsync(
                    "gpupdate.exe",
                    forceArg,
                    timeoutMs: 120000,
                    cancellationToken: cancellationToken);

                result.ExitCode = processResult.ExitCode;
                result.DetailedOutput = processResult.CombinedOutput;

                if (processResult.Success)
                {
                    result.Status = TaskStatus.Success;
                    result.Message = "Group Policy update completed successfully";
                    result.EndTime = DateTime.Now;

                    _logger.LogInformation("Group Policy update completed successfully");
                    progress?.Report("Group Policy update completed successfully");
                    return result;
                }
                else if (processResult.TimedOut)
                {
                    _logger.LogWarning("Group Policy update timed out on attempt {Attempt}", attempt);
                }
                else
                {
                    _logger.LogWarning("Group Policy update failed with exit code {ExitCode} on attempt {Attempt}",
                        processResult.ExitCode, attempt);
                }

                // Wait before retrying
                if (attempt <= maxRetries)
                {
                    _logger.LogInformation("Waiting {Delay} seconds before retry...", retryDelay);
                    progress?.Report($"Waiting {retryDelay} seconds before retry...");
                    await Task.Delay(retryDelay * 1000, cancellationToken);

                    // Exponential backoff
                    retryDelay = Math.Min(60, retryDelay * 2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Group Policy update attempt {Attempt}", attempt);

                if (attempt > maxRetries)
                {
                    result.Status = TaskStatus.Error;
                    result.Message = $"Group Policy update failed: {ex.Message}";
                }
            }
        }

        // If we get here, all retries failed
        if (result.Status != TaskStatus.Success)
        {
            result.Status = TaskStatus.Error;
            result.Message = $"Group Policy update failed after {maxRetries + 1} attempts";
            _logger.LogError("Group Policy update failed after all retry attempts");
        }

        result.EndTime = DateTime.Now;
        return result;
    }
}
