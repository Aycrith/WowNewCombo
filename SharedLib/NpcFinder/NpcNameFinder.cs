using SharedLib.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

#pragma warning disable 162

namespace SharedLib.NpcFinder
{
    [Flags]
    public enum NpcNames
    {
        None = 0,
        Enemy = 1,
        Friendly = 2,
        Neutral = 4,
        Corpse = 8
    }

    public static class NpcNames_Extension
    {
        public static string ToStringF(this NpcNames value) => value switch
        {
            NpcNames.None => nameof(NpcNames.None),
            NpcNames.Enemy => nameof(NpcNames.Enemy),
            NpcNames.Friendly => nameof(NpcNames.Friendly),
            NpcNames.Neutral => nameof(NpcNames.Neutral),
            NpcNames.Corpse => nameof(NpcNames.Corpse),
            _ => nameof(NpcNames.None),
        };
    }

    public enum SearchMode
    {
        Simple = 0,
        Fuzzy = 1
    }

    public static class SearchMode_Extension
    {
        public static string ToStringF(this SearchMode value) => value switch
        {
            SearchMode.Simple => nameof(SearchMode.Simple),
            SearchMode.Fuzzy => nameof(SearchMode.Fuzzy),
            _ => nameof(SearchMode.Simple),
        };
    }

    public sealed partial class NpcNameFinder : IDisposable
    {
        private const SearchMode searchMode = SearchMode.Simple;
        public NpcNames nameType { get; private set; } = NpcNames.Enemy | NpcNames.Neutral;

        private readonly ILogger logger;
        private readonly IBitmapProvider bitmapProvider;
        private readonly PixelFormat pixelFormat;
        private readonly AutoResetEvent autoResetEvent;

        private readonly int bytesPerPixel;

        private readonly Pen whitePen;
        private readonly Pen greyPen;

        public readonly int screenMid;
        public readonly int screenTargetBuffer;
        public readonly int screenMidBuffer;
        public readonly int screenAddBuffer;

        public Rectangle Area;

        private const float refWidth = 1920;
        private const float refHeight = 1080;

        public float ScaleToRefWidth = 1;
        public float ScaleToRefHeight = 1;

        public NpcPosition[] Npcs { get; private set; } = Array.Empty<NpcPosition>();
        public int NpcCount => Npcs.Length;
        public int AddCount { private set; get; }
        public int TargetCount { private set; get; }
        public bool MobsVisible => NpcCount > 0;
        public bool PotentialAddsExist { get; private set; }
        public bool _PotentialAddsExist() => PotentialAddsExist;

        public DateTime LastPotentialAddsSeen { get; private set; }

        private Func<byte, byte, byte, bool> colorMatcher;

        #region variables

        public float colorFuzziness { get; set; } = 15f;

        private const float colorFuzz = 15;

        public int topOffset { get; set; } = 110;

        private float heightMul;
        public int HeightMulti { get; set; }

        public int MaxWidth { get; set; } = 250;

        public int MinHeight { get; set; } = 16;

        public int WidthDiff { get; set; } = 4;

        public int HeightOffset1 { get; set; } = 10;

        public int HeightOffset2 { get; set; } = 2;

        #endregion

        #region Colors

        public const byte fE_R = 250;
        public const byte fE_G = 5;
        public const byte fE_B = 5;

        public const byte fF_R = 5;
        public const byte fF_G = 250;
        public const byte fF_B = 5;

        public const byte fN_R = 250;
        public const byte fN_G = 250;
        public const byte fN_B = 5;

        public const byte fC_RGB = 128;

        public const byte sE_R = 240;
        public const byte sE_G = 35;
        public const byte sE_B = 35;

        public const byte sF_R = 0;
        public const byte sF_G = 250;
        public const byte sF_B = 0;

        public const byte sN_R = 250;
        public const byte sN_G = 250;
        public const byte sN_B = 0;

        #endregion

        public NpcNameFinder(ILogger logger, IBitmapProvider bitmapProvider, AutoResetEvent autoResetEvent)
        {
            this.logger = logger;
            this.bitmapProvider = bitmapProvider;
            this.pixelFormat = bitmapProvider.Bitmap.PixelFormat;
            this.autoResetEvent = autoResetEvent;
            this.bytesPerPixel = Bitmap.GetPixelFormatSize(pixelFormat) / 8;

            UpdateSearchMode();
            logger.LogInformation($"[NpcNameFinder] searchMode = {searchMode.ToStringF()}");

            ScaleToRefWidth = ScaleWidth(1);
            ScaleToRefHeight = ScaleHeight(1);

            CalculateHeightMultipiler();

            Area = new Rectangle(new Point(0, (int)ScaleHeight(topOffset)),
                new Size((int)(bitmapProvider.Bitmap.Width * 0.87f), (int)(bitmapProvider.Bitmap.Height * 0.6f)));

            int screenWidth = bitmapProvider.Rect.Width;
            screenMid = screenWidth / 2;
            screenMidBuffer = screenWidth / 15;
            screenTargetBuffer = screenMidBuffer / 2;
            screenAddBuffer = screenMidBuffer * 3;

            whitePen = new(Color.White, 3);
            greyPen = new(Color.Gray, 3);
        }

        public void Dispose()
        {
            whitePen.Dispose();
            greyPen.Dispose();
        }

        private float ScaleWidth(int value)
        {
            return value * (bitmapProvider.Rect.Width / refWidth);
        }

        private float ScaleHeight(int value)
        {
            return value * (bitmapProvider.Rect.Height / refHeight);
        }

        private void CalculateHeightMultipiler()
        {
            HeightMulti = nameType == NpcNames.Corpse ? 10 : 4;
            heightMul = ScaleHeight(HeightMulti);
        }

        public bool ChangeNpcType(NpcNames type)
        {
            if (nameType == type)
                return false;

            nameType = type;

            CalculateHeightMultipiler();

            TargetCount = 0;
            AddCount = 0;
            Npcs = Array.Empty<NpcPosition>();

            UpdateSearchMode();

            LogTypeChanged(logger, type.ToStringF());

            return true;
        }

        private void UpdateSearchMode()
        {
            switch (searchMode)
            {
                case SearchMode.Simple:
                    BakeSimpleColorMatcher();
                    break;
                case SearchMode.Fuzzy:
                    BakeFuzzyColorMatcher();
                    break;
            }
        }


        #region Simple Color matcher

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BakeSimpleColorMatcher()
        {
            switch (nameType)
            {
                case NpcNames.Enemy | NpcNames.Neutral:
                    colorMatcher = CombinedEnemyNeutrual;
                    return;
                case NpcNames.Friendly | NpcNames.Neutral:
                    colorMatcher = CombinedFriendlyNeutrual;
                    return;
                case NpcNames.Enemy:
                    colorMatcher = SimpleColorEnemy;
                    return;
                case NpcNames.Friendly:
                    colorMatcher = SimpleColorFriendly;
                    return;
                case NpcNames.Neutral:
                    colorMatcher = SimpleColorNeutral;
                    return;
                case NpcNames.Corpse:
                    colorMatcher = SimpleColorCorpse;
                    return;
                case NpcNames.None:
                    colorMatcher = NoMatch;
                    return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SimpleColorEnemy(byte r, byte g, byte b)
        {
            return r > sE_R && g <= sE_G && b <= sE_B;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SimpleColorFriendly(byte r, byte g, byte b)
        {
            return r == sF_R && g > sF_G && b == sF_B;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SimpleColorNeutral(byte r, byte g, byte b)
        {
            return r > sN_R && g > sN_G && b == sN_B;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SimpleColorCorpse(byte r, byte g, byte b)
        {
            return r == fC_RGB && g == fC_RGB && b == fC_RGB;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CombinedFriendlyNeutrual(byte r, byte g, byte b)
        {
            return SimpleColorFriendly(r, g, b) || SimpleColorNeutral(r, g, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CombinedEnemyNeutrual(byte r, byte g, byte b)
        {
            return SimpleColorEnemy(r, g, b) || SimpleColorNeutral(r, g, b);
        }

        private static bool NoMatch(byte r, byte g, byte b)
        {
            return false;
        }

        #endregion


        #region Color Fuzziness matcher

        private void BakeFuzzyColorMatcher()
        {
            switch (nameType)
            {
                case NpcNames.Enemy | NpcNames.Neutral:
                    colorMatcher = FuzzyEnemyOrNeutral;
                    return;
                case NpcNames.Friendly | NpcNames.Neutral:
                    colorMatcher = FuzzyFriendlyOrNeutral;
                    return;
                case NpcNames.Enemy:
                    colorMatcher = FuzzyEnemy;
                    return;
                case NpcNames.Friendly:
                    colorMatcher = FuzzyFriendly;
                    return;
                case NpcNames.Neutral:
                    colorMatcher = FuzzyNeutral;
                    return;
                case NpcNames.Corpse:
                    colorMatcher = FuzzyCorpse;
                    return;
            }
        }

        private static bool FuzzyColor(byte rr, byte gg, byte bb, byte r, byte g, byte b, float fuzzy)
        {
            return MathF.Sqrt(
                ((rr - r) * (rr - r)) +
                ((gg - g) * (rr - g)) +
                ((bb - b) * (bb - b)))
                <= fuzzy;
        }

        private static bool FuzzyEnemyOrNeutral(byte r, byte g, byte b)
            => FuzzyColor(fE_R, fE_G, fE_B, r, g, b, colorFuzz) || FuzzyColor(fN_R, fN_G, fN_B, r, g, b, colorFuzz);

        private static bool FuzzyFriendlyOrNeutral(byte r, byte g, byte b)
            => FuzzyColor(fF_R, fF_G, fF_B, r, g, b, colorFuzz) || FuzzyColor(fN_R, fN_G, fN_B, r, g, b, colorFuzz);

        private static bool FuzzyEnemy(byte r, byte g, byte b)
            => FuzzyColor(fE_R, fE_G, fE_B, r, g, b, colorFuzz);

        private static bool FuzzyFriendly(byte r, byte g, byte b)
            => FuzzyColor(fF_R, fF_G, fF_B, r, g, b, colorFuzz);

        private static bool FuzzyNeutral(byte r, byte g, byte b)
            => FuzzyColor(fN_R, fN_G, fN_B, r, g, b, colorFuzz);

        private static bool FuzzyCorpse(byte r, byte g, byte b)
            => FuzzyColor(fC_RGB, fC_RGB, fC_RGB, r, g, b, colorFuzz);

        #endregion

        public void WaitForUpdate()
        {
            autoResetEvent.WaitOne();
        }

        public void Update()
        {
            Span<LineSegment> lineSegments = PopulateLines(bitmapProvider.Bitmap, bitmapProvider.Rect);
            Npcs = DetermineNpcs(lineSegments);

            TargetCount = Npcs.Count(TargetsCount);
            AddCount = Npcs.Count(IsAdd);

            if (AddCount > 0 && TargetCount >= 1)
            {
                PotentialAddsExist = true;
                LastPotentialAddsSeen = DateTime.UtcNow;
            }
            else
            {
                if (PotentialAddsExist && (DateTime.UtcNow - LastPotentialAddsSeen).TotalSeconds > 1)
                {
                    PotentialAddsExist = false;
                    AddCount = 0;
                }
            }

            autoResetEvent.Set();
        }

        private NpcPosition[] DetermineNpcs(Span<LineSegment> data)
        {
            int c = 0;
            NpcPosition[] npcs = new NpcPosition[data.Length];
            bool[] inGroup = new bool[data.Length];

            float offset1 = ScaleHeight(HeightOffset1);
            float offset2 = ScaleHeight(HeightOffset2);

            const int MAX_GROUP = 32;
            LineSegment[] group = new LineSegment[MAX_GROUP];

            for (int i = 0; i < data.Length; i++)
            {
                if (inGroup[i])
                    continue;

                LineSegment npcLine = data[i];

                int gc = 0;
                group[gc++] = npcLine;
                int lastY = npcLine.Y;

                for (int j = i + 1; j < data.Length; j++)
                {
                    if (gc + 1 >= MAX_GROUP) break;

                    LineSegment laterNpcLine = data[j];
                    if (laterNpcLine.Y > npcLine.Y + offset1) break;
                    if (laterNpcLine.Y > lastY + offset2) break;

                    if (laterNpcLine.XStart > npcLine.XCenter || laterNpcLine.XEnd < npcLine.XCenter || laterNpcLine.Y <= lastY)
                        continue;

                    lastY = laterNpcLine.Y;

                    inGroup[j] = true;
                    group[gc++] = laterNpcLine;
                }

                if (gc > 0)
                {
                    LineSegment n = group[0];
                    Rectangle rect = new(n.XStart, n.Y, n.XEnd - n.XStart, 1);

                    for (int g = 1; g < gc; g++)
                    {
                        n = group[g];

                        rect.X = Math.Min(rect.X, n.XStart);
                        rect.Y = Math.Min(rect.Y, n.Y);

                        if (rect.Right < n.XEnd)
                            rect.Width = n.XEnd - n.XStart;

                        if (rect.Bottom < n.Y)
                            rect.Height = n.Y - rect.Y;
                    }
                    int yOffset = YOffset(Area, rect);
                    npcs[c++] = new NpcPosition(rect.Location, rect.Max(), yOffset, heightMul);
                }
            }

            int lineHeight = 2 * (int)ScaleHeight(MinHeight);

            for (int i = 0; i < c; i++)
            {
                NpcPosition ii = npcs[i];

                if (ii.Equals(NpcPosition.Empty))
                    continue;

                for (int j = 0; j < c; j++)
                {
                    NpcPosition jj = npcs[j];
                    if (i == j || jj.Equals(NpcPosition.Empty))
                        continue;

                    Point pi = ii.Rect.Centre();
                    Point pj = jj.Rect.Centre();
                    float midDistance = PointExt.SqrDistance(pi, pj);

                    if (ii.Rect.IntersectsWith(jj.Rect) || midDistance <= lineHeight)
                    {
                        Rectangle unionRect = Rectangle.Union(ii.Rect, jj.Rect);

                        int yOffset = YOffset(Area, unionRect);
                        npcs[i] = new(unionRect, yOffset, heightMul);
                        npcs[j] = NpcPosition.Empty;
                    }
                }
            }

            npcs = npcs.Where(NonEmpty).ToArray();
            Array.Sort(npcs, OrderBy);
            return npcs;
        }

        private bool TargetsCount(NpcPosition c)
        {
            return !IsAdd(c) && Math.Abs(c.ClickPoint.X - screenMid) < screenTargetBuffer;
        }

        private bool IsAdd(NpcPosition c)
        {
            return (c.ClickPoint.X < screenMid - screenTargetBuffer && c.ClickPoint.X > screenMid - screenAddBuffer) ||
                (c.ClickPoint.X > screenMid + screenTargetBuffer && c.ClickPoint.X < screenMid + screenAddBuffer);
        }

        private static bool NonEmpty(NpcPosition x)
        {
            return !x.Equals(NpcPosition.Empty);
        }

        private int OrderBy(NpcPosition x, NpcPosition y)
        {
            Point origin = bitmapProvider.Rect.Centre();
            float dx = PointExt.SqrDistance(origin, x.ClickPoint);
            float dy = PointExt.SqrDistance(origin, y.ClickPoint);

            return dx > dy ? 1 : 0;
        }

        private int YOffset(Rectangle area, Rectangle npc)
        {
            return npc.Top / area.Top * MinHeight / 4;
        }

        private Span<LineSegment> PopulateLines(Bitmap bitmap, Rectangle rect)
        {
            Rectangle area = this.Area;
            int bytesPerPixel = this.bytesPerPixel;

            int width = (area.Right - area.Left) / 64;
            int height = (area.Bottom - area.Top) / 64;
            int size = width * height;
            LineSegment[] segments = new LineSegment[size];
            int i = 0;

            Func<byte, byte, byte, bool> colorMatcher = this.colorMatcher;
            float minLength = ScaleWidth(MinHeight);
            float lengthDiff = ScaleWidth(WidthDiff);
            float minEndLength = minLength - lengthDiff;

            BitmapData bitmapData = bitmap.LockBits(new Rectangle(Point.Empty, rect.Size), ImageLockMode.ReadOnly, pixelFormat);

            unsafe void body(int y)
            {
                int xStart = -1;
                int xEnd = -1;

                byte* currentLine = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);
                for (int x = area.Left; x < area.Right; x++)
                {
                    int xi = x * bytesPerPixel;

                    if (!colorMatcher(currentLine[xi + 2], currentLine[xi + 1], currentLine[xi]))
                        continue;

                    if (xStart > -1 && (x - xEnd) < minLength)
                    {
                        xEnd = x;
                    }
                    else
                    {
                        if (xStart > -1 && xEnd - xStart > minEndLength)
                        {
                            if (i + 1 >= size)
                                return;

                            segments[i++] = new LineSegment(xStart, xEnd, y);
                        }

                        xStart = x;
                    }
                    xEnd = x;
                }

                if (i < size && xStart > -1 && xEnd - xStart > minEndLength)
                {
                    segments[i++] = new LineSegment(xStart, xEnd, y);
                }
            }
            _ = Parallel.For(area.Top, area.Height, body);

            bitmap.UnlockBits(bitmapData);

            return segments.AsSpan(0, i);
        }

        public void ShowNames(Graphics gr)
        {
            /*
            if (Npcs.Any())
            {
                // target area
                gr.DrawLine(whitePen, new Point(Npcs[0].screenMid - Npcs[0].screenTargetBuffer, Area.Top), new Point(Npcs[0].screenMid - Npcs[0].screenTargetBuffer, Area.Bottom));
                gr.DrawLine(whitePen, new Point(Npcs[0].screenMid + Npcs[0].screenTargetBuffer, Area.Top), new Point(Npcs[0].screenMid + Npcs[0].screenTargetBuffer, Area.Bottom));

                // adds area
                gr.DrawLine(greyPen, new Point(Npcs[0].screenMid - Npcs[0].screenAddBuffer, Area.Top), new Point(Npcs[0].screenMid - Npcs[0].screenAddBuffer, Area.Bottom));
                gr.DrawLine(greyPen, new Point(Npcs[0].screenMid + Npcs[0].screenAddBuffer, Area.Top), new Point(Npcs[0].screenMid + Npcs[0].screenAddBuffer, Area.Bottom));
            }
            */

            foreach (var n in Npcs)
            {
                gr.DrawRectangle(IsAdd(n) ? greyPen : whitePen, n.Rect);
            }
        }

        public Point ToScreenCoordinates()
        {
            return bitmapProvider.Rect.Location;
        }


        #region Logging

        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "[NpcNameFinder] type = {type}")]
        static partial void LogTypeChanged(ILogger logger, string type);

        #endregion
    }
}