using System;
using System.Diagnostics;

#nullable enable

namespace Game
{
    public class WowProcess
    {
        private Process process;
        public Process WarcraftProcess
        {
            get
            {
                if (process == null)
                {
                    Process? process = Get();
                    if (process == null)
                    {
                        throw new ArgumentOutOfRangeException("Unable to find the Wow process!");
                    }

                    if (process.MainWindowHandle == IntPtr.Zero)
                    {
                        throw new NullReferenceException($"Unable read {nameof(process.MainWindowHandle)} {process.ProcessName} - {process.Id} - {process.Handle}");
                    }

                    this.process = process;
                }

                return process;
            }
        }

        private static readonly string[] defaultProcessNames = new string[] {
            "Wow",
            "WowClassic",
            "WowClassicT",
            "Wow-64",
            "WowClassicB"
        };

        public WowProcess()
        {
            Process? process = Get();
            if (process == null)
            {
                throw new ArgumentOutOfRangeException("Unable to find the Wow process");
            }

            if (process.MainWindowHandle == IntPtr.Zero)
            {
                throw new NullReferenceException($"Unable read {nameof(process.MainWindowHandle)} {process.ProcessName} - {process.Id} - {process.Handle}");
            }

            this.process = process;
        }

        //Get the wow-process, if success returns the process else null
        public static Process? Get()
        {
            Process[] processList = Process.GetProcesses();
            for (int i = 0; i < processList.Length; i++)
            {
                Process p = processList[i];
                for (int j = 0; j < defaultProcessNames.Length; j++)
                {
                    if (defaultProcessNames[j].Contains(p.ProcessName))
                    {
                        return p;
                    }
                }
            }

            return null;
        }
    }
}