using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BlazorServer;

public static class CrashReporter
{
    public static void TryWrite(Exception ex, bool shutdownRequested)
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string logsDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logsDir);

            string ts = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            string filePath = Path.Combine(logsDir, $"blazor-crash-{ts}.txt");

            using var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            sw.WriteLine($"TimestampUtc: {DateTimeOffset.UtcNow:o}");
            sw.WriteLine($"ShutdownRequested: {shutdownRequested}");
            sw.WriteLine($"BaseDirectory: {baseDir}");
            sw.WriteLine($"CurrentDirectory: {Environment.CurrentDirectory}");
            sw.WriteLine($".NET: {RuntimeInformation.FrameworkDescription}");
            sw.WriteLine($"OS: {RuntimeInformation.OSDescription}");
            sw.WriteLine($"Process: {Process.GetCurrentProcess().ProcessName} (PID {Environment.ProcessId})");

            sw.WriteLine();
            sw.WriteLine("Args:");
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                sw.WriteLine($"  {arg}");
            }

            sw.WriteLine();
            sw.WriteLine("Environment (selected):");
            foreach (string key in GetInterestingEnvKeys())
            {
                string? value = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    sw.WriteLine($"  {key}={value}");
                }
            }

            sw.WriteLine();
            sw.WriteLine("Exception:");
            sw.WriteLine(ex.ToString());
        }
        catch
        {
            // Never allow crash reporting to crash the process.
        }
    }

    private static IEnumerable<string> GetInterestingEnvKeys() =>
    [
        "ASPNETCORE_ENVIRONMENT",
        "ASPNETCORE_URLS",
        "DOTNET_ENVIRONMENT",
        "Startup__WebUIPort",
        "Startup__WoWPath",
        "Pathing__Mode",
        "Pathing__hostv1",
        "Pathing__hostv3",
        "Pathing__portv1",
        "Pathing__portv3"
    ];
}

