using AutoSetupGUI.Models;

namespace AutoSetupGUI.Services.Interfaces;

/// <summary>
/// Service for Group Policy operations.
/// </summary>
public interface IGroupPolicyService
{
    /// <summary>
    /// Runs gpupdate /force with retry logic.
    /// </summary>
    Task<TaskResult> UpdateGroupPolicyAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
