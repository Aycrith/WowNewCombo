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

    public partial class NpcNameFinder : IDisposable
    {
        private const SearchMode searchMode = SearchMode.Simple;
        private NpcNames nameType = NpcNames.Enemy | NpcNames.Neutral;

        private readonly ILogger logger;
        private readonly IBitmapProvider bitmapProvider;
        private readonly PixelFormat pixelFormat;
        private readonly AutoResetEvent autoResetEvent;
        private readonly OverlappingNames comparer;

        private readonly int bytesPerPixel;

        public Rectangle Area { get; }

        private const float refWidth = 1920;
        private const float refHeight = 1080;

        public float ScaleToRefWidth { get; } = 1;
        public float ScaleToRefHeight { get; } = 1;

        private float heightMul;

        public IEnumerable<NpcPosition> Npcs { get; private set; } = Enumerable.Empty<NpcPosition>();
        public int NpcCount => Npcs.Count();
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

        public int npcPosYHeightMul { get; set; } = 1;

        public int npcNameMaxWidth { get; set; } = 250;

        public int LinesOfNpcMinLength { get; set; } = 16;

        public int LinesOfNpcLengthDiff { get; set; } = 4;

        public int DetermineNpcsHeightOffset1 { get; set; } = 10;

        public int DetermineNpcsHeightOffset2 { get; set; } = 2;

        #endregion

        #region Colors

        private const byte fE_R = 250;
        private const byte fE_G = 5;
        private const byte fE_B = 5;

        private const byte fF_R = 5;
        private const byte fF_G = 250;
        private const byte fF_B = 5;

        private const byte fN_R = 250;
        private const byte fN_G = 250;
        private const byte fN_B = 5;

        private const byte fC_RGB = 128;

        private const byte sE_R = 240;
        private const byte sE_G = 35;
        private const byte sE_B = 35;

        private const byte sF_R = 0;
        private const byte sF_G = 250;
        private const byte sF_B = 0;

        private const byte sN_R = 250;
        private const byte sN_G = 250;
        private const byte sN_B = 0;

        #endregion

        private readonly Pen whitePen;
        private readonly Pen greyPen;

        public NpcNameFinder(ILogger logger, IBitmapProvider bitmapProvider, AutoResetEvent autoResetEvent)
        {
            this.logger = logger;
            this.bitmapProvider = bitmapProvider;
            this.pixelFormat = bitmapProvider.Bitmap.PixelFormat;
            this.autoResetEvent = autoResetEvent;
            this.comparer = new((int)ScaleWidth(LinesOfNpcMinLength), (int)ScaleHeight(DetermineNpcsHeightOffset1));

            this.bytesPerPixel = Bitmap.GetPixelFormatSize(pixelFormat) / 8;

            UpdateSearchMode();
            logger.LogInformation($"[NpcNameFinder] searchMode = {searchMode.ToStringF()}");

            ScaleToRefWidth = ScaleWidth(1);
            ScaleToRefHeight = ScaleHeight(1);

            heightMul = ScaleHeight(npcPosYHeightMul);

            Area = new Rectangle(new Point(0, (int)ScaleHeight(topOffset)),
                new Size((int)(bitmapProvider.Bitmap.Width * 0.87f), (int)(bitmapProvider.Bitmap.Height * 0.6f)));

            whitePen = new Pen(Color.White, 3);
            greyPen = new Pen(Color.Gray, 3);
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

        public bool ChangeNpcType(NpcNames type)
        {
            if (nameType == type)
                return false;

            nameType = type;

            npcPosYHeightMul = nameType == NpcNames.Corpse ? 15 : 1;
            heightMul = ScaleHeight(npcPosYHeightMul);

            TargetCount = 0;
            AddCount = 0;
            Npcs = Enumerable.Empty<NpcPosition>();

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

        static bool TargetsCount(NpcPosition c) => !c.IsAdd && Math.Abs(c.ClickPoint.X - c.screenMid) < c.screenTargetBuffer;
        static bool IsAdd(NpcPosition c) => c.IsAdd;

        static double PointDistance(int x1, int y1, int x2, int y2)
        {
            return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        }

        public void Update()
        {
            (LineOfNpcName[] names, int count) npcNameLines = PopulateLinesOfNpcNames(bitmapProvider.Bitmap, bitmapProvider.Rect);
            Npcs = DetermineNpcs(npcNameLines);

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

        private NpcPosition[] DetermineNpcs((LineOfNpcName[] names, int count) data)
        {
            NpcPosition[] npcs = new NpcPosition[data.count];
            int c = 0;

            float offset1 = ScaleHeight(DetermineNpcsHeightOffset1);
            float offset2 = ScaleHeight(DetermineNpcsHeightOffset2);

            for (int i = 0; i < data.count; i++)
            {
                LineOfNpcName npcLine = data.names[i];
                List<LineOfNpcName> group = new() { npcLine };

                int lastY = npcLine.Y;

                if (npcLine.IsInAgroup)
                    continue;

                for (int j = i + 1; j < data.count; j++)
                {
                    LineOfNpcName laterNpcLine = data.names[j];
                    if (laterNpcLine.Y > npcLine.Y + offset1) break; // 10
                    if (laterNpcLine.Y > lastY + offset2) break; // 5

                    if (laterNpcLine.XStart <= npcLine.X && laterNpcLine.XEnd >= npcLine.X && laterNpcLine.Y > lastY)
                    {
                        laterNpcLine.IsInAgroup = true;

                        group.Add(laterNpcLine);
                        lastY = laterNpcLine.Y;

                        data.names[j] = laterNpcLine;
                    }
                }

                if (group.Count > 0)
                {
                    LineOfNpcName n = group[0];
                    Rectangle rect = new(n.XStart, n.Y, n.XEnd - n.XStart, 1);
                    for (int g = 1; g < group.Count; g++)
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
                    npcs[c++] = new NpcPosition(rect.Location, rect.Max(), bitmapProvider.Rect.Width, yOffset, heightMul);
                }
            }

            int lineHeight = 2 * (int)ScaleWidth(LinesOfNpcMinLength);

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
                    double midDistance = PointDistance(pi.X, pi.Y, pj.X, pj.Y);

                    if (ii.Rect.IntersectsWith(jj.Rect) || midDistance <= lineHeight)
                    {
                        Rectangle unionRect = Rectangle.Union(ii.Rect, jj.Rect);

                        int yOffset = YOffset(Area, unionRect);
                        npcs[i] = new(unionRect, ii, yOffset, heightMul);
                        npcs[j] = NpcPosition.Empty;
                    }
                }
            }

            var array = npcs.Where(x => !x.Equals(NpcPosition.Empty)).ToArray();
            Array.Sort(array, Order);
            return array;
        }

        private int Order(NpcPosition x, NpcPosition y)
        {
            float dx = RectangleExt.SqrDistance(Area.BottomCentre(), x.ClickPoint);
            float dy = RectangleExt.SqrDistance(Area.BottomCentre(), y.ClickPoint);

            return dx > dy ? 1 : 0;
        }

        private int YOffset(Rectangle area, Rectangle npc)
        {
            return (int)((float)area.Height / npc.Top * ScaleHeight(10));
        }

        private (LineOfNpcName[], int) PopulateLinesOfNpcNames(Bitmap bitmap, Rectangle rect)
        {
            Rectangle Area = this.Area;
            int bytesPerPixel = this.bytesPerPixel;

            int widthCount = (Area.Right - Area.Left) / 64;
            int heightCount = (Area.Bottom - Area.Top) / 64;
            LineOfNpcName[] npcNameLine = new LineOfNpcName[widthCount * heightCount];
            int i = 0;

            Func<byte, byte, byte, bool> colorMatcher = this.colorMatcher;
            float minLength = ScaleWidth(LinesOfNpcMinLength);
            float lengthDiff = ScaleWidth(LinesOfNpcLengthDiff);
            float minEndLength = minLength - lengthDiff;

            unsafe
            {
                BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, rect.Width, rect.Height), ImageLockMode.ReadOnly, pixelFormat);

                void body(int y)
                {
                    int xStart = -1;
                    int xEnd = -1;

                    byte* currentLine = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);
                    for (int x = Area.Left; x < Area.Right; x++)
                    {
                        int xi = x * bytesPerPixel;

                        if (colorMatcher(currentLine[xi + 2], currentLine[xi + 1], currentLine[xi]))
                        {
                            if (xStart > -1 && (x - xEnd) < minLength)
                            {
                                xEnd = x;
                            }
                            else
                            {
                                if (xStart > -1 && xEnd - xStart > minEndLength)
                                {
                                    npcNameLine[i++] = new LineOfNpcName(xStart, xEnd, y);
                                }

                                xStart = x;
                            }
                            xEnd = x;
                        }
                    }

                    if (xStart > -1 && xEnd - xStart > minEndLength)
                    {
                        npcNameLine[i++] = new LineOfNpcName(xStart, xEnd, y);
                    }
                }
                _ = Parallel.For(Area.Top, Area.Height, body);

                bitmap.UnlockBits(bitmapData);
            }

            return (npcNameLine, i);
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
                gr.DrawRectangle(n.IsAdd ? greyPen : whitePen, n.Rect);
            }
        }

        public Point ToScreenCoordinates(int x, int y)
        {
            return new Point(bitmapProvider.Rect.X + x, bitmapProvider.Rect.Top + y);
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