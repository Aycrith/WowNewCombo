using System;
using System.Diagnostics;
using System.Threading;

#nullable enable

namespace Game
{
    public class WowProcess : IDisposable
    {
        private static readonly string[] defaultProcessNames = new string[] {
            "Wow",
            "WowClassic",
            "WowClassicT",
            "Wow-64",
            "WowClassicB"
        };

        private readonly Thread thread;
        private readonly CancellationTokenSource cts;

        public Process Process { get; private set; }

        private int processId = -1;
        public int ProcessId
        {
            get => processId;
            set
            {
                processId = value;
                Process = Process.GetProcessById(processId);
            }
        }

        public bool IsRunning { get; private set; }

        public WowProcess(int pid = -1)
        {
            Process? p = Get(pid);
            if (p == null)
                throw new NullReferenceException("Unable to find running World of Warcraft process!");

            Process = p;
            processId = Process.Id;
            IsRunning = true;

            cts = new();
            thread = new(PollProcessExited);
            thread.Start();
        }

        public void Dispose()
        {
            cts.Cancel();
        }

        private void PollProcessExited()
        {
            while (!cts.IsCancellationRequested)
            {
                Process.Refresh();
                if (Process.HasExited)
                {
                    IsRunning = false;

                    Process? p = Get();
                    if (p != null)
                    {
                        Process = p;
                        processId = Process.Id;
                        IsRunning = true;
                    }
                }

                cts.Token.WaitHandle.WaitOne(5000);
            }
        }

        public static Process? Get(int processId = -1)
        {
            if (processId != -1)
            {
                return Process.GetProcessById(processId);
            }

            Process[] processList = Process.GetProcesses();
            for (int i = 0; i < processList.Length; i++)
            {
                Process p = processList[i];
                for (int j = 0; j < defaultProcessNames.Length; j++)
                {
                    if (defaultProcessNames[j].Contains(p.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        return p;
                    }
                }
            }

            return null;
        }
    }
}