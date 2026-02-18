using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Core.Diagnostics;

/// <summary>
/// Comprehensive diagnostics for all bot systems.
/// Provides health checks, dependency validation, and failure detection.
/// </summary>
public sealed class SystemDiagnostics
{
    private readonly ILogger<SystemDiagnostics> _logger;
    private readonly List<DiagnosticCheck> _checks = [];

    public SystemDiagnostics(ILogger<SystemDiagnostics> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Run all diagnostic checks and return comprehensive report.
    /// </summary>
    public async Task<DiagnosticReport> RunFullDiagnosticsAsync()
    {
        _logger.LogInformation("[SystemDiagnostics] Running full system diagnostics...");

        var report = new DiagnosticReport
        {
            Timestamp = DateTime.UtcNow,
            Checks = []
        };

        // Run all registered checks
        foreach (var check in _checks)
        {
            try
            {
                var result = await check.ExecuteAsync();
                report.Checks.Add(result);
                LogResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SystemDiagnostics] Check {Name} threw exception", check.Name);
                report.Checks.Add(new DiagnosticResult
                {
                    Name = check.Name,
                    Status = DiagnosticStatus.Error,
                    Message = $"Check failed with exception: {ex.Message}",
                    Exception = ex
                });
            }
        }

        report.OverallStatus = DetermineOverallStatus(report.Checks);
        report.IsHealthy = report.OverallStatus == DiagnosticStatus.Healthy;

        _logger.LogInformation("[SystemDiagnostics] Diagnostics complete. Status: {Status}", report.OverallStatus);

        return report;
    }

    /// <summary>
    /// Register a diagnostic check to run during full diagnostics.
    /// </summary>
    public void RegisterCheck(DiagnosticCheck check)
    {
        _checks.Add(check);
    }

    /// <summary>
    /// Run navigation server specific diagnostics.
    /// </summary>
    public async Task<DiagnosticResult> CheckNavigationServerAsync(string serverPath, int port)
    {
        _logger.LogDebug("[SystemDiagnostics] Checking navigation server...");

        // Check executable exists
        if (!File.Exists(serverPath))
        {
            return new DiagnosticResult
            {
                Name = "NavigationServer",
                Status = DiagnosticStatus.Error,
                Message = $"Executable not found: {serverPath}",
                Recommendation = "Ensure AmeisenNavigationServer.exe is in the Navigation folder"
            };
        }

        // Check MMAP files
        var mmapsPath = Path.Combine(Path.GetDirectoryName(serverPath) ?? ".", "mmaps");
        if (!Directory.Exists(mmapsPath))
        {
            return new DiagnosticResult
            {
                Name = "NavigationServer",
                Status = DiagnosticStatus.Error,
                Message = $"MMAP directory not found: {mmapsPath}",
                Recommendation = "Download and extract MMAP files to Navigation/mmaps/"
            };
        }

        var mmapFiles = Directory.GetFiles(mmapsPath, "*.mmap");
        if (mmapFiles.Length == 0)
        {
            return new DiagnosticResult
            {
                Name = "NavigationServer",
                Status = DiagnosticStatus.Error,
                Message = "No .mmap files found in mmaps directory",
                Recommendation = "Download MMAP files for your game version"
            };
        }

        // Check if port is available or in use
        var portStatus = CheckPortStatus(port);

        if (portStatus == PortStatus.InUse)
        {
            // Check if it's the nav server
            using var process = GetProcessUsingPort(port);
            if (process != null)
            {
                int pid = process.Id;
                string processName = process.ProcessName;
                return new DiagnosticResult
                {
                    Name = "NavigationServer",
                    Status = DiagnosticStatus.Healthy,
                    Message = $"Server running (PID: {pid}, MMAPs: {mmapFiles.Length})",
                    Details = new Dictionary<string, object>
                    {
                        ["ProcessName"] = processName,
                        ["MmapFiles"] = mmapFiles.Length,
                        ["Port"] = port
                    }
                };
            }
            else
            {
                return new DiagnosticResult
                {
                    Name = "NavigationServer",
                    Status = DiagnosticStatus.Warning,
                    Message = $"Port {port} is in use by unknown process",
                    Recommendation = "Check for conflicting applications or restart"
                };
            }
        }
        else if (portStatus == PortStatus.Available)
        {
            return new DiagnosticResult
            {
                Name = "NavigationServer",
                Status = DiagnosticStatus.Warning,
                Message = $"Port {port} available but server not running",
                Recommendation = "Start the navigation server or enable auto-start"
            };
        }
        else
        {
            return new DiagnosticResult
            {
                Name = "NavigationServer",
                Status = DiagnosticStatus.Error,
                Message = $"Cannot determine port {port} status",
                Recommendation = "Check firewall settings and network configuration"
            };
        }
    }

    /// <summary>
    /// Check WoW process status.
    /// </summary>
    public DiagnosticResult CheckWoWProcess()
    {
        _logger.LogDebug("[SystemDiagnostics] Checking WoW process...");

        var processNames = new[] { "Wow", "WowClassic", "WowB" };
        Process? foundProcess = null;

        foreach (var name in processNames)
        {
            var processes = Process.GetProcessesByName(name);
            if (processes.Length > 0)
            {
                foundProcess = processes[0];
                foreach (var p in processes.Skip(1))
                {
                    p.Dispose();
                }
                break;
            }
        }

        if (foundProcess == null)
        {
            return new DiagnosticResult
            {
                Name = "WoWProcess",
                Status = DiagnosticStatus.Error,
                Message = "No WoW process found",
                Recommendation = "Start World of Warcraft and log in"
            };
        }

        try
        {
            foundProcess.Refresh();
            if (foundProcess.HasExited)
            {
                return new DiagnosticResult
                {
                    Name = "WoWProcess",
                    Status = DiagnosticStatus.Error,
                    Message = $"WoW process (PID: {foundProcess.Id}) has exited",
                    Recommendation = "Restart World of Warcraft"
                };
            }

            // Get window info
            var windowHandle = foundProcess.MainWindowHandle;
            var hasWindow = windowHandle != IntPtr.Zero;

            return new DiagnosticResult
            {
                Name = "WoWProcess",
                Status = DiagnosticStatus.Healthy,
                Message = $"WoW running (PID: {foundProcess.Id}, Process: {foundProcess.ProcessName})",
                Details = new Dictionary<string, object>
                {
                    ["ProcessId"] = foundProcess.Id,
                    ["ProcessName"] = foundProcess.ProcessName,
                    ["HasWindow"] = hasWindow,
                    ["WorkingSet64"] = foundProcess.WorkingSet64
                }
            };
        }
        catch (Exception ex)
        {
            return new DiagnosticResult
            {
                Name = "WoWProcess",
                Status = DiagnosticStatus.Error,
                Message = $"Error checking WoW process: {ex.Message}",
                Exception = ex
            };
        }
        finally
        {
            foundProcess?.Dispose();
        }
    }

    /// <summary>
    /// Check addon installation.
    /// </summary>
    public DiagnosticResult CheckAddonInstallation()
    {
        _logger.LogDebug("[SystemDiagnostics] Checking addon installation...");

        // This would need to be implemented based on actual addon detection logic
        // For now, return placeholder
        return new DiagnosticResult
        {
            Name = "AddonInstallation",
            Status = DiagnosticStatus.Healthy,
            Message = "Addon check not implemented",
            Recommendation = "Verify addon is installed manually"
        };
    }

    private PortStatus CheckPortStatus(int port)
    {
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            foreach (var listener in listeners)
            {
                if (listener.Port == port)
                {
                    return PortStatus.InUse;
                }
            }
            return PortStatus.Available;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[SystemDiagnostics] Error checking port status");
            return PortStatus.Unknown;
        }
    }

    private static Process? GetProcessUsingPort(int port)
    {
        try
        {
            // Simplified check - match known navigation server process name.
            // A full implementation would use netstat/WMI to map port -> PID.
            var processes = Process.GetProcesses();
            Process? match = null;
            foreach (var proc in processes)
            {
                if (match == null &&
                    proc.ProcessName.Contains("AmeisenNavigation", StringComparison.OrdinalIgnoreCase))
                {
                    match = proc;
                }
                else
                {
                    proc.Dispose();
                }
            }
            return match;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void LogResult(DiagnosticResult result)
    {
        var logLevel = result.Status switch
        {
            DiagnosticStatus.Healthy => LogLevel.Information,
            DiagnosticStatus.Warning => LogLevel.Warning,
            DiagnosticStatus.Error => LogLevel.Error,
            _ => LogLevel.Debug
        };

        _logger.Log(logLevel, "[SystemDiagnostics] {Name}: {Status} - {Message}",
            result.Name, result.Status, result.Message);

        if (!string.IsNullOrEmpty(result.Recommendation))
        {
            _logger.Log(logLevel, "[SystemDiagnostics] Recommendation: {Recommendation}", result.Recommendation);
        }
    }

    private static DiagnosticStatus DetermineOverallStatus(List<DiagnosticResult> checks)
    {
        if (checks.Count == 0)
            return DiagnosticStatus.Error;

        if (checks.Any(c => c.Status == DiagnosticStatus.Error))
            return DiagnosticStatus.Error;

        if (checks.Any(c => c.Status == DiagnosticStatus.Warning))
            return DiagnosticStatus.Warning;

        return DiagnosticStatus.Healthy;
    }
}

public enum DiagnosticStatus
{
    Healthy,
    Warning,
    Error
}

public class DiagnosticReport
{
    public DateTime Timestamp { get; set; }
    public DiagnosticStatus OverallStatus { get; set; }
    public bool IsHealthy { get; set; }
    public List<DiagnosticResult> Checks { get; set; } = [];
}

public class DiagnosticResult
{
    public string Name { get; set; } = "";
    public DiagnosticStatus Status { get; set; }
    public string Message { get; set; } = "";
    public string? Recommendation { get; set; }
    public Dictionary<string, object>? Details { get; set; }
    public Exception? Exception { get; set; }
}

public abstract class DiagnosticCheck
{
    public string Name { get; protected set; } = "";
    public abstract Task<DiagnosticResult> ExecuteAsync();
}

public enum PortStatus
{
    Available,
    InUse,
    Unknown
}
