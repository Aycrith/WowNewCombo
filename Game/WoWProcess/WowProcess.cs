using System;
using System.Collections.Generic;
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

        private static readonly List<string> defaultProcessNames = new()
            { "Wow", "WowClassic", "WowClassicT", "Wow-64", "WowClassicB" };

        public WowProcess()
        {
            var process = Get();
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
                if (defaultProcessNames.Contains(p.ProcessName))
                {
                    return p;
                }
            }

            //logger.Error($"Failed to find the wow process, tried: {string.Join(", ", names)}");

            return null;
        }
    }
}