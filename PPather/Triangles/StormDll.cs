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
using System.IO;
using System.Runtime.InteropServices;

namespace StormDll
{
    internal sealed class StormDllx64
    {
        [DllImport("MPQ\\StormLib_x64.dll")]
        public static extern bool SFileOpenArchive(
            [MarshalAs(UnmanagedType.LPWStr)] string szMpqName,
            uint dwPriority,
            [MarshalAs(UnmanagedType.U4)] OpenArchive dwFlags,
            out IntPtr phMpq);

        [DllImport("MPQ\\StormLib_x64.dll")]
        public static extern bool SFileCloseArchive(IntPtr hMpq);

        [DllImport("MPQ\\StormLib_x64.dll")]
        public static extern bool SFileExtractFile(
            IntPtr hMpq,
            [MarshalAs(UnmanagedType.LPStr)] string szToExtract,
            [MarshalAs(UnmanagedType.LPWStr)] string szExtracted,
            [MarshalAs(UnmanagedType.U4)] OpenFile dwSearchScope);
    }

    internal sealed class StormDllx86
    {
        [DllImport("MPQ\\StormLib_x86.dll")]
        public static extern bool SFileOpenArchive(
            [MarshalAs(UnmanagedType.LPWStr)] string szMpqName,
            uint dwPriority,
            [MarshalAs(UnmanagedType.U4)] OpenArchive dwFlags,
            out IntPtr phMpq);

        [DllImport("MPQ\\StormLib_x86.dll")]
        public static extern bool SFileCloseArchive(IntPtr hMpq);

        [DllImport("MPQ\\StormLib_x86.dll")]
        public static extern bool SFileExtractFile(
            IntPtr hMpq,
            [MarshalAs(UnmanagedType.LPStr)] string szToExtract,
            [MarshalAs(UnmanagedType.LPWStr)] string szExtracted,
            [MarshalAs(UnmanagedType.U4)] OpenFile dwSearchScope);
    }

    // Flags for SFileOpenArchive
    [Flags]
    public enum OpenArchive : uint
    {
        BASE_PROVIDER_FILE = 0x00000000,  // Base data source is a file
        BASE_PROVIDER_MAP = 0x00000001,  // Base data source is memory-mapped file
        BASE_PROVIDER_HTTP = 0x00000002,  // Base data source is a file on web server
        BASE_PROVIDER_MASK = 0x0000000F,  // Mask for base provider value
        STREAM_PROVIDER_FLAT = 0x00000000,  // Stream is linear with no offset mapping
        STREAM_PROVIDER_PARTIAL = 0x00000010,  // Stream is partial file (.part)
        STREAM_PROVIDER_MPQE = 0x00000020,  // Stream is an encrypted MPQ
        STREAM_PROVIDER_BLOCK4 = 0x00000030,  // = 0x4000 per block, text MD5 after each block, max = 0x2000 blocks per file
        STREAM_PROVIDER_MASK = 0x000000F0,  // Mask for stream provider value
        STREAM_FLAG_READ_ONLY = 0x00000100,  // Stream is read only
        STREAM_FLAG_WRITE_SHARE = 0x00000200,  // Allow write sharing when open for write
        STREAM_FLAG_USE_BITMAP = 0x00000400,  // If the file has a file bitmap, load it and use it
        STREAM_OPTIONS_MASK = 0x0000FF00,  // Mask for stream options
        STREAM_PROVIDERS_MASK = 0x000000FF,  // Mask to get stream providers
        STREAM_FLAGS_MASK = 0x0000FFFF,  // Mask for all stream flags (providers+options)
        MPQ_OPEN_NO_LISTFILE = 0x00010000,  // Don't load the internal listfile
        MPQ_OPEN_NO_ATTRIBUTES = 0x00020000,  // Don't open the attributes
        MPQ_OPEN_NO_HEADER_SEARCH = 0x00040000,  // Don't search for the MPQ header past the begin of the file
        MPQ_OPEN_FORCE_MPQ_V1 = 0x00080000,  // Always open the archive as MPQ v 1.00, ignore the "wFormatVersion" variable in the header
        MPQ_OPEN_CHECK_SECTOR_CRC = 0x00100000,  // On files with MPQ_FILE_SECTOR_CRC, the CRC will be checked when reading file
        MPQ_OPEN_FORCE_LISTFILE = 0x00400000,  // Force add listfile even if there is none at the moment of opening
        MPQ_OPEN_READ_ONLY = STREAM_FLAG_READ_ONLY
    };

    // Values for SFileExtractFile
    public enum OpenFile : uint
    {
        SFILE_OPEN_FROM_MPQ = 0x00000000,  // Open the file from the MPQ archive
        SFILE_OPEN_CHECK_EXISTS = 0xFFFFFFFC,  // Only check whether the file exists
        SFILE_OPEN_LOCAL_FILE = 0xFFFFFFFF  // Open a local file
    };

    public sealed class ArchiveSet
    {
        private Archive[] archives;

        public ArchiveSet(ILogger logger, string[] files)
        {
            archives = new Archive[files.Length];

            for (int i = 0; i < files.Length; i++)
            {
                Archive a = new(files[i], out bool open, 0,
                    OpenArchive.MPQ_OPEN_NO_LISTFILE |
                    OpenArchive.MPQ_OPEN_NO_ATTRIBUTES |
                    OpenArchive.MPQ_OPEN_NO_HEADER_SEARCH |
                    OpenArchive.MPQ_OPEN_READ_ONLY);

                if (open && a.IsOpen())
                {
                    archives[i] = a;

                    if (logger.IsEnabled(LogLevel.Trace))
                        logger.LogTrace($"Archive[{i}] open {files[i]}");
                }
                else if (logger.IsEnabled(LogLevel.Trace))
                    logger.LogTrace($"Archive[{i}] openfail {files[i]}");
            }
        }

        public bool SFileExtractFile(string from, string to, OpenFile dwSearchScope = OpenFile.SFILE_OPEN_FROM_MPQ)
        {
            for (int i = 0; i < archives.Length; i++)
            {
                Archive a = archives[i];
                if (a.HasFile(from))
                {
                    return a.SFileExtractFile(from, to, dwSearchScope);
                }
            }

            return false;
        }

        public void Close()
        {
            for (int i = 0; i < archives.Length; i++)
                archives[i].SFileCloseArchive();

            archives = Array.Empty<Archive>();
        }
    }

    internal sealed class Archive
    {
        private readonly IntPtr handle;

        private readonly System.Collections.Generic.HashSet<string> fileList = new(StringComparer.InvariantCultureIgnoreCase);

        public Archive(string file, out bool open, uint Prio, OpenArchive Flags)
        {
            open = Environment.Is64BitProcess
                ? StormDllx64.SFileOpenArchive(file, Prio, Flags, out handle)
                : StormDllx86.SFileOpenArchive(file, Prio, Flags, out handle);

            if (open)
            {
                string temp = Path.GetTempFileName();

                bool extracted = Environment.Is64BitProcess
                ? StormDllx64.SFileExtractFile(handle, "(listfile)", temp, OpenFile.SFILE_OPEN_FROM_MPQ)
                : StormDllx86.SFileExtractFile(handle, "(listfile)", temp, OpenFile.SFILE_OPEN_FROM_MPQ);

                if (extracted && File.Exists(temp))
                {
                    foreach (string line in File.ReadLines(temp))
                    {
                        fileList.Add(line);
                    }
                }
            }
        }

        public bool IsOpen()
        {
            return handle != IntPtr.Zero;
        }

        public bool HasFile(string name)
        {
            return fileList.Contains(name);
        }

        public bool SFileCloseArchive()
        {
            fileList.Clear();
            return Environment.Is64BitProcess
                ? StormDllx64.SFileCloseArchive(handle)
                : StormDllx86.SFileCloseArchive(handle);
        }

        public bool SFileExtractFile(string from, string to, OpenFile dwSearchScope)
        {
            return Environment.Is64BitProcess
                ? StormDllx64.SFileExtractFile(handle, from, to, dwSearchScope)
                : StormDllx86.SFileExtractFile(handle, from, to, dwSearchScope);
        }
    }
}