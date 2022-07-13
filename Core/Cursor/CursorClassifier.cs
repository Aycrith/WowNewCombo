using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using static WinAPI.NativeMethods;

namespace Core
{
    public static class CursorClassifier
    {
        // index matches CursorType order
        public static readonly ulong[][] imageHashes =
        {
            new ulong[] { 4645529528554094592, 4665762466636896256, 6376251547633783040, 6376251547633783552 },
            new ulong[] { 9286546093378506253 },
            new ulong[] { 16205332705670085656 },
            new ulong[] { 13901748381153107456 },
            new ulong[] { 4669700909741929478, 4669700909674820614 },
            new ulong[] { 4683320813727784960, 4669700909741929478, 4683461550142398464 },
            new ulong[] { 17940331276560775168, 17940331276594329600, 17940331276594460672 },
            new ulong[] { 16207573517913036808, 4669140166357294088 },
            new ulong[] { 4667452417086599168, 4676529985085517824 },
            new ulong[] { 4682718988357606424, 4682718988358655000 }
        };

        public static void Classify(out CursorType classification)
        {
            Size size = GetCursorSize();
            Bitmap bitmap = new(size.Width, size.Height);

            CURSORINFO cursorInfo = new();
            cursorInfo.cbSize = Marshal.SizeOf(cursorInfo);
            if (GetCursorInfo(ref cursorInfo) &&
                cursorInfo.flags == CURSOR_SHOWING)
            {
                Graphics g = Graphics.FromImage(bitmap);
                DrawIcon(g.GetHdc(), 0, 0, cursorInfo.hCursor);
                g.Dispose();
            }

            ulong cursorHash = ImageHashing.AverageHash(bitmap);
            //var filename = hash + ".bmp";
            //var path = System.IO.Path.Join("../Cursors/", filename);
            //if (!System.IO.File.Exists(path))
            //{
            //    bitmap.Save(path);
            //}

            var matching = imageHashes
                .SelectMany((a, i) => a.Select(v => (similarity: ImageHashing.Similarity(cursorHash, v), index: i, imagehash: a)))
                .Where(t => t.similarity > 80)
                .OrderByDescending(t => t.similarity)
                .FirstOrDefault();

            classification = (CursorType)matching.index;
            Debug.WriteLine($"[CursorClassifier.Classify] {classification.ToStringF()} - {matching.similarity}");
        }
    }
}