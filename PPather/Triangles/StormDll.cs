/*
  This file is part of ppather.

    PPather is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    PPather is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with ppather.  If not, see <http://www.gnu.org/licenses/>.

    Copyright Pontus Borg 2008

 */

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace StormDll
{
    internal unsafe class StormDllx64
    {

        [DllImport("MPQ\\StormLib_x64.dll")]
        public static extern bool SFileOpenArchive(
            [MarshalAs(UnmanagedType.LPWStr)] string szMpqName,
            uint dwPriority,
            [MarshalAs(UnmanagedType.U4)] OpenArchiveFlags dwFlags,
            out IntPtr phMpq);

        [DllImport("MPQ\\StormLib_x64.dll")]
        public static extern bool SFileCloseArchive(IntPtr hMpq);

        [DllImport("MPQ\\StormLib_x64.dll")]
        public static extern bool SFileExtractFile(
            IntPtr hMpq,
            [MarshalAs(UnmanagedType.LPStr)] string szToExtract,
            [MarshalAs(UnmanagedType.LPWStr)] string szExtracted,
            [MarshalAs(UnmanagedType.U4)] OpenFile dwSearchScope);

        [DllImport("MPQ\\StormLib_x64.dll")]
        public static extern bool SFileOpenPatchArchive(
            IntPtr hMpq,
            [MarshalAs(UnmanagedType.LPStr)] string szMpqName,
            [MarshalAs(UnmanagedType.LPStr)] string szPatchPathPrefix,
            uint dwFlags);

        [DllImport("MPQ\\StormLib_x64.dll")]
        public static extern bool SFileHasFile(IntPtr hMpq,
            [MarshalAs(UnmanagedType.LPStr)] string szFileName);

    }

    internal unsafe class StormDllx86
    {

        [DllImport("MPQ\\StormLib_x86.dll")]
        public static extern bool SFileOpenArchive(
            [MarshalAs(UnmanagedType.LPWStr)] string szMpqName,
            uint dwPriority,
            [MarshalAs(UnmanagedType.U4)] OpenArchiveFlags dwFlags,
            out IntPtr phMpq);

        [DllImport("MPQ\\StormLib_x86.dll")]
        public static extern bool SFileCloseArchive(IntPtr hMpq);

        [DllImport("MPQ\\StormLib_x86.dll")]
        public static extern bool SFileExtractFile(
            IntPtr hMpq,
            [MarshalAs(UnmanagedType.LPStr)] string szToExtract,
            [MarshalAs(UnmanagedType.LPWStr)] string szExtracted,
            [MarshalAs(UnmanagedType.U4)] OpenFile dwSearchScope);

        [DllImport("MPQ\\StormLib_x86.dll")]
        public static extern bool SFileOpenPatchArchive(
            IntPtr hMpq,
            [MarshalAs(UnmanagedType.LPStr)] string szMpqName,
            [MarshalAs(UnmanagedType.LPStr)] string szPatchPathPrefix,
            uint dwFlags);

        [DllImport("MPQ\\StormLib_x86.dll")]
        public static extern bool SFileHasFile(IntPtr hMpq,
            [MarshalAs(UnmanagedType.LPStr)] string szFileName);

    }

    // Flags for SFileOpenArchive
    public enum OpenArchiveFlags : uint
    {
        NO_LISTFILE = 0x0010,   // Don't load the internal listfile
        NO_ATTRIBUTES = 0x0020,   // Don't open the attributes
        MFORCE_MPQ_V1 = 0x0040,   // Always open the archive as MPQ v 1.00, ignore the "wFormatVersion" variable in the header
        MCHECK_SECTOR_CRC = 0x0080,   // On files with MPQ_FILE_SECTOR_CRC, the CRC will be checked when reading file
        READ_ONLY = 0x0100,   // Open the archive for read-only access
        ENCRYPTED = 0x0200,   // Opens an encrypted MPQ archive (Example: Starcraft II installation)
    };

    // Values for SFileExtractFile
    public enum OpenFile : uint
    {
        FROM_MPQ = 0x00000000,   // Open the file from the MPQ archive
        PATCHED_FILE = 0x00000001,   // Open the file from the MPQ archive
        BY_INDEX = 0x00000002,   // The 'szFileName' parameter is actually the file index
        ANY_LOCALE = 0xFFFFFFFE,   // Reserved for StormLib internal use
        LOCAL_FILE = 0xFFFFFFFF,   // Open the file from the MPQ archive
    };

    public unsafe class ArchiveSet
    {
        private readonly ILogger logger;
        private readonly HashSet<Archive> archives;

        public ArchiveSet(ILogger logger, string[] files)
        {
            this.logger = logger;

            archives = new();

            for (int i = 0; i < files.Length; i++)
            {
                Archive a = new(files[i], 0, 0, logger);
                if (a.IsOpen())
                {
                    archives.Add(a);

                    if (logger.IsEnabled(LogLevel.Trace))
                        logger.LogTrace($"Add archive {files[i]}");
                }

                if (logger.IsEnabled(LogLevel.Trace))
                    logger.LogTrace($"Failed archive {files[i]}");
            }
        }

        public bool ExtractFile(string from, string to, OpenFile dwSearchScope = OpenFile.FROM_MPQ)
        {
            foreach (Archive a in archives)
            {
                if (a.HasFile(from))
                {
                    bool ok = a.ExtractFile(from, to, dwSearchScope);
                    if (!ok)
                    {
                        if (logger.IsEnabled(LogLevel.Trace))
                            logger.LogTrace("  result: " + ok);
                    }
                    return ok;
                }
            }
            return false;
        }

        public void Close()
        {
            foreach (Archive a in archives)
            {
                a.Close();
            }
            archives.Clear();
        }
    }

    public unsafe class Archive
    {
        #region "Processor check"
        static bool is64BitProcess = (IntPtr.Size == 8);
        #endregion

        private IntPtr handle = IntPtr.Zero;

        public Archive(string file, uint Prio, OpenArchiveFlags Flags, ILogger logger)
        {
            bool r = false;
            if (is64BitProcess)
            {
                //64 bit 
                r = StormDllx64.SFileOpenArchive(file, Prio, Flags, out handle);
            }
            else
            {
                //32 bit
                r = StormDllx86.SFileOpenArchive(file, Prio, Flags, out handle);
            }
            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace($"Archive open ? {r} -> {file}");
        }

        public bool IsOpen()
        {
            return handle != IntPtr.Zero;
        }

        public bool Close()
        {
            bool r = false;
            if (is64BitProcess)
            {
                //64 bit 
                r = StormDllx64.SFileCloseArchive(handle);
            }
            else
            {
                //32 bit
                r = StormDllx86.SFileCloseArchive(handle);
            }
            if (r)
                handle = IntPtr.Zero;
            return r;
        }

        public bool HasFile(string name)
        {
            bool r = false;
            if (is64BitProcess)
            {
                //64 bit 
                r = StormDllx64.SFileHasFile(handle, name);
            }
            else
            {
                //32 bit
                r = StormDllx86.SFileHasFile(handle, name);
            }

            return r;
        }

        public bool ExtractFile(string from, string to, OpenFile dwSearchScope)
        {
            bool r = false;
            if (is64BitProcess)
            {
                //64 bit 
                r = StormDllx64.SFileExtractFile(handle, from, to, dwSearchScope);
            }
            else
            {
                //32 bit
                r = StormDllx86.SFileExtractFile(handle, from, to, dwSearchScope);
            }

            return r;

        }
    }
}