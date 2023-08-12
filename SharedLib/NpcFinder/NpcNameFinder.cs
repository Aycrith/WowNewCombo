using SharedLib.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.Linq;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Buffers;
using System.Collections.Generic;

#pragma warning disable 162

namespace SharedLib.NpcFinder;

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

    public static bool HasFlagF(this NpcNames value, NpcNames flag)
    {
        return (value & flag) != 0;
    }
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
    private readonly ILogger logger;
    private readonly IBitmapProvider bitmapProvider;
    private readonly PixelFormat pixelFormat;
    private readonly INpcResetEvent resetEvent;

    private readonly int bytesPerPixel;

    private readonly Pen whitePen;
    private readonly Pen greyPen;

    public readonly int screenMid;
    public readonly int screenTargetBuffer;
    public readonly int screenMidBuffer;
    public readonly int screenAddBuffer;

    public readonly Rectangle Area;

    private const float refWidth = 1920;
    private const float refHeight = 1080;

    public readonly float ScaleToRefWidth = 1;
    public readonly float ScaleToRefHeight = 1;

    private SearchMode searchMode = SearchMode.Fuzzy;
    public NpcNames nameType { private set; get; } =
        NpcNames.Enemy | NpcNames.Neutral;

    public ArraySegment<NpcPosition> Npcs { get; private set; } =
        Array.Empty<NpcPosition>();

    public int NpcCount => Npcs.Count;
    public int AddCount { private set; get; }
    public int TargetCount { private set; get; }
    public bool MobsVisible => NpcCount > 0;
    public bool PotentialAddsExist { get; private set; }
    public bool _PotentialAddsExist() => PotentialAddsExist;

    public DateTime LastPotentialAddsSeen { get; private set; }

    private Func<byte, byte, byte, bool> colorMatcher;

    private readonly NpcPositionComparer npcPosComparer;

    #region variables

    public float colorFuzziness { get; set; } = 15f;

    private const int colorFuzz = 40;

    public int topOffset { get; set; } = 117;

    private float heightMul;
    public int HeightMulti { get; set; }

    public int MaxWidth { get; set; } = 250;

    public int MinHeight { get; set; } = 16;

    public int WidthDiff { get; set; } = 4;

    public int HeightOffset1 { get; set; } = 10;

    public int HeightOffset2 { get; set; } = 2;

    #endregion

    #region Colors

    public const byte fBase = 230;

    public const byte fE_R = fBase;
    public const byte fE_G = 0;
    public const byte fE_B = 0;

    public const byte fF_R = 0;
    public const byte fF_G = fBase;
    public const byte fF_B = 0;

    public const byte fN_R = fBase;
    public const byte fN_G = fBase;
    public const byte fN_B = 0;

    public const byte fuzzCorpse = 18;
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

    public NpcNameFinder(ILogger logger, IBitmapProvider bitmapProvider,
        INpcResetEvent resetEvent)
    {
        this.logger = logger;
        this.bitmapProvider = bitmapProvider;
        this.pixelFormat = bitmapProvider.Bitmap.PixelFormat;
        this.resetEvent = resetEvent;
        this.bytesPerPixel = Bitmap.GetPixelFormatSize(pixelFormat) / 8;

        UpdateSearchMode();

        npcPosComparer = new(bitmapProvider);

        ScaleToRefWidth = ScaleWidth(1);
        ScaleToRefHeight = ScaleHeight(1);

        CalculateHeightMultipiler();

        Area = new Rectangle(new Point(0, (int)ScaleHeight(topOffset)),
            new Size(
                (int)(bitmapProvider.Bitmap.Width * 0.87f),
                (int)(bitmapProvider.Bitmap.Height * 0.6f)));

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

        resetEvent.ChangeSet();

        TargetCount = 0;
        AddCount = 0;
        Npcs = Array.Empty<NpcPosition>();

        nameType = type;

        switch (type)
        {
            case NpcNames.Corpse:
                searchMode = SearchMode.Simple;
                break;
            default:
                searchMode = SearchMode.Fuzzy;
                break;
        }

        CalculateHeightMultipiler();
        UpdateSearchMode();

        LogTypeChanged(logger, type.ToStringF(), searchMode.ToStringF());

        if (nameType == NpcNames.None)
            resetEvent.ChangeReset();

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

    [SkipLocalsInit]
    private static bool FuzzyColor(
        byte rr, byte gg, byte bb,
        byte r, byte g, byte b,
        int fuzzy)
    {
        unchecked
        {
            int sqrDistance =
                ((rr - r) * (rr - r)) +
                ((gg - g) * (gg - g)) +
                ((bb - b) * (bb - b));
            return sqrDistance <= fuzzy * fuzzy;
        }
    }

    private static bool FuzzyEnemyOrNeutral(byte r, byte g, byte b)
        => FuzzyColor(fE_R, fE_G, fE_B, r, g, b, colorFuzz) ||
            FuzzyColor(fN_R, fN_G, fN_B, r, g, b, colorFuzz);

    private static bool FuzzyFriendlyOrNeutral(byte r, byte g, byte b)
        => FuzzyColor(fF_R, fF_G, fF_B, r, g, b, colorFuzz) ||
            FuzzyColor(fN_R, fN_G, fN_B, r, g, b, colorFuzz);

    private static bool FuzzyEnemy(byte r, byte g, byte b)
        => FuzzyColor(fE_R, fE_G, fE_B, r, g, b, colorFuzz);

    private static bool FuzzyFriendly(byte r, byte g, byte b)
        => FuzzyColor(fF_R, fF_G, fF_B, r, g, b, colorFuzz);

    private static bool FuzzyNeutral(byte r, byte g, byte b)
        => FuzzyColor(fN_R, fN_G, fN_B, r, g, b, colorFuzz);

    private static bool FuzzyCorpse(byte r, byte g, byte b)
        => FuzzyColor(fC_RGB, fC_RGB, fC_RGB, r, g, b, fuzzCorpse);

    #endregion

    public void WaitForUpdate()
    {
        resetEvent.Wait();
    }

    public void Update()
    {
        resetEvent.ChangeReset();
        resetEvent.Reset();

        ReadOnlySpan<LineSegment> lineSegments =
            PopulateLines(bitmapProvider.Bitmap, Area);
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
            if (PotentialAddsExist &&
                (DateTime.UtcNow - LastPotentialAddsSeen).TotalSeconds > 1)
            {
                PotentialAddsExist = false;
                AddCount = 0;
            }
        }

        resetEvent.Set();
    }

    private ArraySegment<NpcPosition> DetermineNpcs(ReadOnlySpan<LineSegment> data)
    {
        int c = 0;

        var pool = ArrayPool<NpcPosition>.Shared;
        NpcPosition[] npcs = pool.Rent(data.Length);

        float offset1 = ScaleHeight(HeightOffset1);
        float offset2 = ScaleHeight(HeightOffset2);

        const int MAX_GROUP = 64;
        Span<bool> inGroup = stackalloc bool[data.Length];
        Span<LineSegment> group = stackalloc LineSegment[MAX_GROUP];

        for (int i = 0; i < data.Length; i++)
        {
            if (inGroup[i])
                continue;

            ref readonly LineSegment npcLine = ref data[i];

            int gc = 0;
            group[gc++] = npcLine;
            int lastY = npcLine.Y;

            for (int j = i + 1; j < data.Length; j++)
            {
                if (gc + 1 >= MAX_GROUP) break;

                ref readonly LineSegment laterNpcLine = ref data[j];
                if (laterNpcLine.Y > npcLine.Y + offset1) break;
                if (laterNpcLine.Y > lastY + offset2) break;

                if (laterNpcLine.XStart > npcLine.XCenter ||
                    laterNpcLine.XEnd < npcLine.XCenter ||
                    laterNpcLine.Y <= lastY)
                    continue;

                lastY = laterNpcLine.Y;

                inGroup[j] = true;
                group[gc++] = laterNpcLine;
            }

            if (gc > 0)
            {
                ref LineSegment n = ref group[0];
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
                npcs[c++] = new NpcPosition(
                    rect.Location, rect.Max(), yOffset, heightMul);
            }
        }

        int lineHeight = 2 * (int)ScaleHeight(MinHeight);

        for (int i = 0; i < c; i++)
        {
            ref readonly NpcPosition ii = ref npcs[i];
            if (ii.Equals(NpcPosition.Empty))
                continue;

            for (int j = 0; j < c; j++)
            {
                ref readonly NpcPosition jj = ref npcs[j];
                if (i == j || jj.Equals(NpcPosition.Empty))
                    continue;

                Point pi = ii.Rect.Centre();
                Point pj = jj.Rect.Centre();
                float midDistance = PointExt.SqrDistance(pi, pj);

                if (ii.Rect.IntersectsWith(jj.Rect) ||
                    midDistance <= lineHeight * lineHeight)
                {
                    Rectangle unionRect = Rectangle.Union(ii.Rect, jj.Rect);

                    int yOffset = YOffset(Area, unionRect);
                    npcs[i] = new(unionRect, yOffset, heightMul);
                    npcs[j] = NpcPosition.Empty;
                }
            }
        }

        int length = MoveEmptyToEnd(npcs, NpcPosition.Empty);
        Array.Sort(npcs, 0, length, npcPosComparer);

        pool.Return(npcs);

        return new ArraySegment<NpcPosition>(npcs, 0, length);
    }

    private static int MoveEmptyToEnd<T>(Span<T> span, in T empty)
    {
        int emptyIndex = -span.Length;
        for (int i = 0; i < span.Length; i++)
        {
            if (EqualityComparer<T>.Default.Equals(span[i], empty))
            {
                if (emptyIndex == -span.Length)
                    emptyIndex = i;
            }
            else if (emptyIndex != -span.Length)
            {
                Swap(span, emptyIndex, i);
                emptyIndex++;
            }
        }

        return emptyIndex;

        static void Swap(Span<T> span, int i, int j)
        {
            (span[j], span[i]) = (span[i], span[j]);
        }
    }

    private bool TargetsCount(NpcPosition c)
    {
        return !IsAdd(c) &&
            Math.Abs(c.ClickPoint.X - screenMid) < screenTargetBuffer;
    }

    private bool IsAdd(NpcPosition c)
    {
        return
            (c.ClickPoint.X < screenMid - screenTargetBuffer &&
             c.ClickPoint.X > screenMid - screenAddBuffer) ||
            (c.ClickPoint.X > screenMid + screenTargetBuffer &&
             c.ClickPoint.X < screenMid + screenAddBuffer);
    }

    private int YOffset(Rectangle area, Rectangle npc)
    {
        return npc.Top / area.Top * MinHeight / 4;
    }

    [SkipLocalsInit]
    private ReadOnlySpan<LineSegment> PopulateLines(Bitmap bitmap, Rectangle rect)
    {
        Rectangle area = this.Area;
        int bytesPerPixel = this.bytesPerPixel;

        int width = (area.Right - area.Left) / 32;
        int height = (area.Bottom - area.Top) / 32;
        int size = width * height;
        var pooler = ArrayPool<LineSegment>.Shared;
        LineSegment[] segments = pooler.Rent(size);
        int i = 0;

        Func<byte, byte, byte, bool> colorMatcher = this.colorMatcher;
        float minLength = ScaleWidth(MinHeight);
        float lengthDiff = ScaleWidth(WidthDiff);
        float minEndLength = minLength - lengthDiff;

        BitmapData bitmapData =
            bitmap.LockBits(new Rectangle(Point.Empty, rect.Size),
                ImageLockMode.ReadOnly, pixelFormat);

        [SkipLocalsInit]
        unsafe void body(int y)
        {
            int xStart = -1;
            int xEnd = -1;
            int end = area.Right;

            byte* currentLine = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);
            for (int x = area.Left; x < end; x++)
            {
                int xi = x * bytesPerPixel;

                if (!colorMatcher(
                    currentLine[xi + 2],
                    currentLine[xi + 1],
                    currentLine[xi]))
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

        pooler.Return(segments);
        return new(segments, 0, i);
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

        // TODO: Overflow error. ''\_(o.o)_/''
        /*
        System.OverflowException: Overflow error.
        at System.Drawing.Graphics.CheckErrorStatus(Int32 status)
        at System.Drawing.Graphics.DrawRectangle(Pen pen, Int32 x, Int32 y, Int32 width, Int32 height)
        */
        for (int i = 0; i < Npcs.Count; i++)
        {
            NpcPosition n = Npcs[i];
            gr.DrawRectangle(IsAdd(n) ? greyPen : whitePen, n.Rect);
        }
    }

    public Point ToScreenCoordinates()
    {
        return bitmapProvider.Rect.Location;
    }


    #region Logging

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "[NpcNameFinder] type = {type} | mode = {mode}")]
    static partial void LogTypeChanged(ILogger logger, string type, string mode);

    #endregion
}