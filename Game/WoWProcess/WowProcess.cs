using Microsoft.Extensions.Options;

using SharedLib;

using System;
using System.Diagnostics;
using System.Threading;

#nullable enable

namespace Game;

public sealed class WowProcess
{
    private static readonly string[] defaultProcessNames = [
        "Wow",
        "WowClassic",
        "WowClassicT",
        "Wow-64",
        "WowClassicB"
    ];

    private readonly Thread? thread;
    private readonly CancellationToken token;

    public Version FileVersion { get; private set; } = new Version(0, 0, 0, 0);

    public string Path { get; private set; } = string.Empty;

    private Process? process;

    private int id = -1;
    public int Id
    {
        get => id;
        set
        {
            id = value;
            process = Process.GetProcessById(id);
        }
    }

    public string ProcessName => process?.ProcessName ?? "Unknown";

    public IntPtr MainWindowHandle => process?.MainWindowHandle ?? IntPtr.Zero;

    public bool IsRunning { get; private set; }
    
    /// <summary>
    /// Indicates that this WowProcess was created in configuration mode without a running WoW process.
    /// The polling thread will try to find WoW and update state when it becomes available.
    /// </summary>
    public bool IsConfigurationMode { get; private set; }

    /// <summary>
    /// Event fired when WoW process is found (useful in configuration mode)
    /// </summary>
    public event Action? OnProcessFound;

    private WowProcess(CancellationTokenSource cts, int pid = -1, bool allowConfigurationMode = false)
    {
        token = cts.Token;

        Process? p = Get(pid);
        
        if (p == null)
        {
            if (allowConfigurationMode)
            {
                // Configuration mode - WoW not running but we'll poll for it
                Console.WriteLine("[WowProcess] WoW not found. Starting in configuration mode - will poll for WoW process.");
                IsRunning = false;
                IsConfigurationMode = true;
                process = null;
                
                // Start polling thread to find WoW when it starts
                thread = new(PollForProcess);
                thread.Start();
                return;
            }
            else
            {
                throw new NullReferenceException(
                    $"Unable to find {(pid == -1 ? "any" : $"pid={pid}")} " +
                    $"running World of Warcraft process!");
            }
        }

        process = p;
        id = process.Id;
        IsRunning = true;
        IsConfigurationMode = false;
        (Path, FileVersion) = GetProcessInfo();

        thread = new(PollProcessExited);
        thread.Start();
    }

    public WowProcess(CancellationTokenSource cts, IOptions<StartupConfigPid> options) : this(cts, options.Value.Id, allowConfigurationMode: true) { }
    
    /// <summary>
    /// Creates a WowProcess, optionally allowing configuration mode if WoW is not running.
    /// </summary>
    public static WowProcess Create(CancellationTokenSource cts, int pid = -1, bool allowConfigurationMode = true)
    {
        return new WowProcess(cts, pid, allowConfigurationMode);
    }
    
    /// <summary>
    /// Poll for WoW process when it's not initially running (configuration mode)
    /// </summary>
    private void PollForProcess()
    {
        while (!token.IsCancellationRequested && !IsRunning)
        {
            Process? p = Get();
            if (p != null)
            {
                Console.WriteLine($"[WowProcess] Found WoW process! PID: {p.Id}, Name: {p.ProcessName}");
                process = p;
                id = process.Id;
                IsRunning = true;
                IsConfigurationMode = false;
                
                try
                {
                    (Path, FileVersion) = GetProcessInfo();
                    Console.WriteLine($"[WowProcess] WoW path: {Path}, Version: {FileVersion}");
                    OnProcessFound?.Invoke();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WowProcess] Error getting process info: {ex.Message}");
                    // Continue with the process even if we can't get version info
                }
                
                // Switch to the normal polling loop
                PollProcessExited();
                return;
            }
            
            token.WaitHandle.WaitOne(2000); // Poll every 2 seconds
        }
    }

    private void PollProcessExited()
    {
        while (!token.IsCancellationRequested)
        {
            if (process == null)
            {
                // Process was never found, shouldn't happen in this code path
                token.WaitHandle.WaitOne(5000);
                continue;
            }
            
            process.Refresh();
            if (process.HasExited)
            {
                IsRunning = false;

                Process? p = Get();
                if (p != null)
                {
                    process = p;
                    id = process.Id;
                    IsRunning = true;
                    try
                    {
                        (Path, FileVersion) = GetProcessInfo();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WowProcess] Error getting process info after reconnect: {ex.Message}");
                    }
                }
            }

            token.WaitHandle.WaitOne(5000);
        }
    }

    public static Process? Get(int processId = -1)
    {
        if (processId != -1)
        {
            try
            {
                return Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                // Process doesn't exist or access denied
                return null;
            }
        }

        Process[] processList;
        try
        {
            processList = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WowProcess] Failed to enumerate processes: {ex.Message}");
            return null;
        }

        Console.WriteLine($"[WowProcess] Searching {processList.Length} processes for WoW...");
        
        for (int i = 0; i < processList.Length; i++)
        {
            Process p = processList[i];
            string? procName = null;
            
            try
            {
                procName = p.ProcessName;
            }
            catch (Exception)
            {
                // Skip processes we can't access
                continue;
            }
            
            if (string.IsNullOrEmpty(procName))
                continue;
                
            for (int j = 0; j < defaultProcessNames.Length; j++)
            {
                // Check if process name matches any of our default names
                if (procName.Equals(defaultProcessNames[j], StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[WowProcess] Found WoW process: {procName} (PID: {p.Id})");
                    return p;
                }
            }
        }

        // Log what we were looking for vs what we found
        Console.WriteLine($"[WowProcess] WoW not found. Looking for: {string.Join(", ", defaultProcessNames)}");
        
        return null;
    }

    private (string path, Version version) GetProcessInfo()
    {
        // Try WinAPI first
        string? path = WinAPI.ExecutablePath.Get(process);
        
        // If WinAPI fails or returns empty, try MainModule
        if (string.IsNullOrEmpty(path))
        {
            try
            {
                string? moduleFileName = process.MainModule?.FileName;
                if (!string.IsNullOrEmpty(moduleFileName))
                {
                    path = System.IO.Path.GetDirectoryName(moduleFileName);
                }
            }
            catch
            {
                // MainModule access can fail due to permissions
            }
        }
        
        // If we still don't have a path, throw
        if (string.IsNullOrEmpty(path))
        {
            throw new NullReferenceException(
                $"Unable to identify World of Warcraft process path! " +
                $"Process ID: {process.Id}, Process Name: {process.ProcessName}. " +
                $"Try running the bot as Administrator.");
        }

        // Construct full executable path
        var exePath = System.IO.Path.Join(path, process.ProcessName + ".exe");
        
        // Verify the file exists
        if (!System.IO.File.Exists(exePath))
        {
            throw new System.IO.FileNotFoundException(
                $"WoW executable not found at expected location: {exePath}. " +
                $"Process Name: {process.ProcessName}, Path: {path}");
        }
        
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);

        if (info.FileMajorPart > 0)
        {
            Version v = new(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
            return (path, v);
        }

        return (path, new Version());
    }
}