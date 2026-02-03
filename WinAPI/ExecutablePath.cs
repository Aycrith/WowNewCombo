using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WinAPI;

/// <summary>
/// Provides reliable process executable path detection using Win32 API.
/// Replaces WMI-based detection which fails intermittently.
/// </summary>
public static partial class ExecutablePath
{
    private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const int MAX_PATH = 260;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint OpenProcess(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint hObject);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageNameW(
        nint hProcess,
        int dwFlags,
        Span<char> lpExeName,
        ref int lpdwSize);

    /// <summary>
    /// Gets the directory path of the executable for the given process.
    /// Uses QueryFullProcessImageName which is more reliable than WMI.
    /// </summary>
    /// <param name="process">The process to get the executable path for.</param>
    /// <returns>The directory containing the executable, or empty string on failure.</returns>
    public static string Get(Process process)
    {
        if (process == null)
            return string.Empty;

        // Try Method 1: Open process with limited info rights and query
        string path = GetViaOpenProcess(process.Id);
        if (!string.IsNullOrEmpty(path))
            return path;

        // Try Method 2: Process.MainModule (may throw for access-restricted processes)
        path = GetViaMainModule(process);
        if (!string.IsNullOrEmpty(path))
            return path;

        return string.Empty;
    }

    private static string GetViaOpenProcess(int processId)
    {
        nint hProcess = nint.Zero;
        try
        {
            hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (hProcess == nint.Zero)
                return string.Empty;

            Span<char> buffer = stackalloc char[1024];
            int size = buffer.Length;

            if (QueryFullProcessImageNameW(hProcess, 0, buffer, ref size))
            {
                string fullPath = new string(buffer[..size]);
                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                {
                    return Path.GetDirectoryName(fullPath) ?? string.Empty;
                }
            }
        }
        catch
        {
            // Silently fail - calling code handles empty string
        }
        finally
        {
            if (hProcess != nint.Zero)
            {
                CloseHandle(hProcess);
            }
        }

        return string.Empty;
    }

    private static string GetViaMainModule(Process process)
    {
        try
        {
            var mainModule = process.MainModule;
            if (mainModule != null && !string.IsNullOrEmpty(mainModule.FileName))
            {
                if (File.Exists(mainModule.FileName))
                {
                    return Path.GetDirectoryName(mainModule.FileName) ?? string.Empty;
                }
            }
        }
        catch (Win32Exception)
        {
            // Access denied (32-bit process querying 64-bit, or elevated process)
        }
        catch (InvalidOperationException)
        {
            // Process has exited
        }

        return string.Empty;
    }
}
