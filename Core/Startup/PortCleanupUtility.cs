using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Core.Startup;

/// <summary>
/// Shared utility for finding and terminating processes holding a specific TCP port.
/// Used by both <see cref="NavigationServerManager"/> and <see cref="StartupOrchestrator"/>.
/// </summary>
public static class PortCleanupUtility
{
    /// <summary>
    /// Attempts to terminate processes holding the specified TCP port.
    /// Only kills processes matching the expected process name.
    /// </summary>
    public static void TryTerminateProcessHoldingPort(int port, string expectedProcessName, ILogger logger)
    {
        try
        {
            HashSet<int> processIds = GetListeningProcessIds(port);
            foreach (int processId in processIds)
            {
                try
                {
                    Process existing = Process.GetProcessById(processId);
                    string processName = existing.ProcessName;

                    if (!processName.Equals(expectedProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning(
                            "Port {Port} is held by PID {PID} ({Name}), expected {Expected}. Skipping termination.",
                            port, processId, processName, expectedProcessName);
                        continue;
                    }

                    logger.LogWarning(
                        "Terminating stale {Name} process on port {Port} (PID: {PID})",
                        processName, port, processId);

                    existing.Kill(true);
                    if (!existing.WaitForExit(5000))
                    {
                        logger.LogWarning(
                            "Process PID {PID} did not exit after termination request",
                            processId);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to terminate process PID {PID} holding port {Port}",
                        processId, port);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to inspect port ownership for {Port}", port);
        }
    }

    /// <summary>
    /// Returns the set of process IDs that are in LISTENING state on the specified TCP port.
    /// Uses <c>netstat -ano -p tcp</c> on Windows.
    /// </summary>
    public static HashSet<int> GetListeningProcessIds(int port)
    {
        HashSet<int> processIds = [];

        ProcessStartInfo psi = new()
        {
            FileName = "netstat",
            Arguments = "-ano -p tcp",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? process = Process.Start(psi);
        if (process == null)
        {
            return processIds;
        }

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        string[] lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string portNeedle = ":" + port.ToString();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase) ||
                !line.Contains(portNeedle, StringComparison.Ordinal))
            {
                continue;
            }

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                continue;
            }

            string localAddress = parts[1];
            if (!localAddress.EndsWith(portNeedle, StringComparison.Ordinal))
            {
                continue;
            }

            if (int.TryParse(parts[^1], out int processId))
            {
                processIds.Add(processId);
            }
        }

        return processIds;
    }
}
