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
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace StormDll
{
    internal unsafe class StormDll
    {
        /*
		 *
typedef unsigned long       DWORD;
typedef DWORD LCID;

// Archive opening/closing
LCID  WINAPI SFileSetLocale(LCID lcNewLocale);
LCID  WINAPI SFileGetLocale();
BOOL  WINAPI SFileOpenArchive(const char * szMpqName, DWORD dwPriority, DWORD dwFlags, HANDLE * phMpq);
BOOL  WINAPI SFileCloseArchive(HANDLE hMpq);

// File opening/closing
BOOL  WINAPI SFileOpenFileEx(HANDLE hMpq, const char * szFileName, DWORD dwSearchScope, HANDLE * phFile);
BOOL  WINAPI SFileCloseFile(HANDLE hFile);

// File I/O
DWORD WINAPI SFileGetFilePos(HANDLE hFile, DWORD * pdwFilePosHigh = NULL);
DWORD WINAPI SFileGetFileSize(HANDLE hFile, DWORD * pdwFileSizeHigh = NULL);
DWORD WINAPI SFileSetFilePointer(HANDLE hFile, LONG lFilePos, LONG * pdwFilePosHigh, DWORD dwMethod);
BOOL  WINAPI SFileReadFile(HANDLE hFile, VOID * lpBuffer, DWORD dwToRead,
						   DWORD * pdwRead = NULL, LPOVERLAPPED lpOverlapped = NULL);

// Adds another listfile into MPQ. The currently added listfile(s) remain,
// so you can use this API to combining more listfiles.
// Note that this function is internally called by SFileFindFirstFile
int   WINAPI SFileAddListFile(HANDLE hMpq, const char * szListFile);

		 */

        [DllImport("MPQ\\StormLib.dll")]
        public static extern uint SFileGetLocale();

        [DllImport("MPQ\\StormLib.dll")]
        public static extern bool SFileOpenArchive([MarshalAs(UnmanagedType.LPStr)] string szMpqName,
                              uint dwPriority, uint dwFlags,
                              void** phMpq);

        [DllImport("MPQ\\StormLib.dll")]
        public static extern bool SFileCloseArchive(void* hMpq);

        [DllImport("MPQ\\StormLib.dll")]
        public static extern bool SFileOpenFileEx(void* hMpq,
                                [MarshalAs(UnmanagedType.LPStr)] string szFileName,
                                uint dwSearchScope,
                                void** phFile);

        [DllImport("MPQ\\StormLib.dll")]
        public static extern bool SFileCloseFile(void* hFile);

        [DllImport("MPQ\\StormLib.dll")]
        public static extern uint SFileGetFilePos(void* hFile, uint* pdwFilePosHigh);

        [DllImport("MPQ\\StormLib.dll")]
        public static extern uint SFileGetFileSize(void* hFile, uint* pdwFileSizeHigh);

        [DllImport("MPQ\\StormLib.dll")]
        public static extern uint SFileSetFilePointer(void* hFile,
                    int lFilePos, int* pdwFilePosHigh, uint dwMethod);

        [DllImport("MPQ\\StormLib.dll")]
        public static extern bool SFileReadFile(void* hFile, void* lpBuffer, uint dwToRead,
                           uint* pdwRead, void* lpOverlapped);

        [DllImport("MPQ\\StormLib.dll")]
        public static extern bool SFileExtractFile(void* hMpq,
                    [MarshalAs(UnmanagedType.LPStr)] string szToExtract,
                    [MarshalAs(UnmanagedType.LPStr)] string szExtracted);

        [DllImport("MPQ\\StormLib.dll")]
        public static extern bool SFileHasFile(void* hMpq,
                    [MarshalAs(UnmanagedType.LPStr)] string szFileName);

        /*
		[DllImport("user32.dll")]
		public static extern int MessageBoxA(int h,
					[MarshalAs(UnmanagedType.LPStr)]string m,
					[MarshalAs(UnmanagedType.LPStr)]string c,
					int type);
		 * */

        public static uint GetLocale()
        {
            return SFileGetLocale();
        }
    }

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

        public bool ExtractFile(string from, string to)
        {
            foreach (Archive a in archives)
            {
                if (a.HasFile(from))
                {
                    bool ok = a.ExtractFile(from, to);
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
        private void* handle;

        public Archive(string file, uint Prio, uint Flags, ILogger logger)
        {
            void* h;
            void** hp = &h;
            bool r = StormDll.SFileOpenArchive(file, Prio, Flags, hp);
            handle = h;

            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace($"Archive open ? {r} -> {file}");
        }

        public bool IsOpen()
        {
            return handle != null;
        }

        public bool Close()
        {
            bool r = StormDll.SFileCloseArchive(handle);
            if (r)
                handle = null;
            return r;
        }

        public bool HasFile(string name)
        {
            return StormDll.SFileHasFile(handle, name);
        }

        public bool ExtractFile(string from, string to)
        {
            return StormDll.SFileExtractFile(handle, from, to);
        }
    }
}