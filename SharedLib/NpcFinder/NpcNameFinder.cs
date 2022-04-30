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

    public enum SearchMode
    {
        Simple = 0,
        Fuzzy = 1
    }

    public class NpcNameFinder
    {
        private const SearchMode searchMode = SearchMode.Simple;
        private NpcNames nameType = NpcNames.Enemy | NpcNames.Neutral;

        private readonly ILogger logger;
        private readonly IBitmapProvider bitmapProvider;
        private readonly AutoResetEvent autoResetEvent;

        public Rectangle Area { get; }

        private const float refWidth = 1920;
        private const float refHeight = 1080;

        public float ScaleToRefWidth { get; } = 1;
        public float ScaleToRefHeight { get; } = 1;

        public List<NpcPosition> Npcs { get; private set; } = new();
        public int NpcCount => Npcs.Count;
        public int AddCount { private set; get; }
        public int TargetCount { private set; get; }
        public bool MobsVisible => Npcs.Count > 0;
        public bool PotentialAddsExist { get; private set; }
        public DateTime LastPotentialAddsSeen { get; private set; }

        private Func<Color, bool> colorMatcher;

        #region variables

        public float colorFuzziness { get; set; } = 15f;

        public int topOffset { get; set; } = 110;

        public int npcPosYOffset { get; set; } = 0;
        public int npcPosYHeightMul { get; set; } = 10;

        public int npcNameMaxWidth { get; set; } = 250;

        public int LinesOfNpcMinLength { get; set; } = 22;

        public int LinesOfNpcLengthDiff { get; set; } = 4;

        public int DetermineNpcsHeightOffset1 { get; set; } = 10;

        public int DetermineNpcsHeightOffset2 { get; set; } = 2;

        public int incX { get; set; } = 1;

        public int incY { get; set; } = 1;

        #endregion

        #region Colors

        private readonly Color fEnemy = Color.FromArgb(0, 250, 5, 5);
        private readonly Color fFriendly = Color.FromArgb(0, 5, 250, 5);
        private readonly Color fNeutrual = Color.FromArgb(0, 250, 250, 5);
        private readonly Color fCorpse = Color.FromArgb(0, 128, 128, 128);

        private readonly Color sEnemy = Color.FromArgb(0, 240, 35, 35);
        private readonly Color sFriendly = Color.FromArgb(0, 0, 250, 0);
        private readonly Color sNeutrual = Color.FromArgb(0, 250, 250, 0);

        #endregion

        public NpcNameFinder(ILogger logger, IBitmapProvider bitmapProvider, AutoResetEvent autoResetEvent)
        {
            this.logger = logger;
            this.bitmapProvider = bitmapProvider;
            this.autoResetEvent = autoResetEvent;

            UpdateSearchMode();

            ScaleToRefWidth = ScaleWidth(1);
            ScaleToRefHeight = ScaleHeight(1);

            Area = new Rectangle(new Point(0, (int)ScaleHeight(topOffset)),
                new Size((int)(bitmapProvider.Bitmap.Width * 0.87f), (int)(bitmapProvider.Bitmap.Height * 0.6f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ScaleWidth(int value)
        {
            return value * (bitmapProvider.Bitmap.Width / refWidth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ScaleHeight(int value)
        {
            return value * (bitmapProvider.Bitmap.Height / refHeight);
        }

        public void ChangeNpcType(NpcNames type)
        {
            if (nameType == type)
                return;

            nameType = type;

            Npcs.Clear();

            if (nameType.HasFlag(NpcNames.Corpse))
            {
                npcPosYHeightMul = 15;
            }
            else
            {
                npcPosYHeightMul = 10;
            }

            UpdateSearchMode();

            logger.LogInformation($"{nameof(NpcNameFinder)}.{nameof(ChangeNpcType)} = {type} | searchMode = {searchMode}");
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
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SimpleColorEnemy(Color p)
        {
            return p.R > sEnemy.R && p.G <= sEnemy.G && p.B <= sEnemy.B;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SimpleColorFriendly(Color p)
        {
            return p.R == sFriendly.R && p.G > sFriendly.G && p.B == sFriendly.B;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SimpleColorNeutral(Color p)
        {
            return p.R > sNeutrual.R && p.G > sNeutrual.G && p.B == sNeutrual.B;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SimpleColorCorpse(Color p)
        {
            return p.R == fCorpse.R && p.G == fCorpse.G && p.B == fCorpse.B;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CombinedFriendlyNeutrual(Color c)
        {
            return SimpleColorFriendly(c) || SimpleColorNeutral(c);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CombinedEnemyNeutrual(Color c)
        {
            return SimpleColorEnemy(c) || SimpleColorNeutral(c);
        }

        #endregion


        #region Color Fuzziness matcher

        private void BakeFuzzyColorMatcher()
        {
            switch (nameType)
            {
                case NpcNames.Enemy | NpcNames.Neutral:
                    colorMatcher = (Color c) => FuzzyColor(fEnemy, c) || FuzzyColor(fNeutrual, c);
                    return;
                case NpcNames.Friendly | NpcNames.Neutral:
                    colorMatcher = (Color c) => FuzzyColor(fFriendly, c) || FuzzyColor(fNeutrual, c);
                    return;
                case NpcNames.Enemy:
                    colorMatcher = (Color c) => FuzzyColor(fEnemy, c);
                    return;
                case NpcNames.Friendly:
                    colorMatcher = (Color c) => FuzzyColor(fFriendly, c);
                    return;
                case NpcNames.Neutral:
                    colorMatcher = (Color c) => FuzzyColor(fNeutrual, c);
                    return;
                case NpcNames.Corpse:
                    colorMatcher = (Color c) => FuzzyColor(fCorpse, c);
                    return;
            }
        }

        private bool FuzzyColor(Color target, Color c)
        {
            return MathF.Sqrt(
                ((target.R - c.R) * (target.R - c.R)) +
                ((target.G - c.G) * (target.G - c.G)) +
                ((target.B - c.B) * (target.B - c.B)))
                <= colorFuzziness;
        }

        #endregion

        public void Update()
        {
            var npcNameLines = PopulateLinesOfNpcNames();
            var npcs = DetermineNpcs(npcNameLines);

            Npcs = npcs.
                Select(lineofNpcName =>
                    new NpcPosition(new Point(lineofNpcName.Min(x => x.XStart), lineofNpcName.Min(x => x.Y)),
                    new Point(lineofNpcName.Max(x => x.XEnd), lineofNpcName.Max(x => x.Y)),
                    bitmapProvider.Bitmap.Width,
                    ScaleHeight(npcPosYOffset), ScaleHeight(npcPosYHeightMul)))
                .Where(npcPos => npcPos.Width < ScaleWidth(npcNameMaxWidth))
                .Distinct(new OverlappingNames())
                .OrderBy(npcPos => RectangleExt.SqrDistance(Area.BottomCentre(), npcPos.ClickPoint))
                .ToList();

            UpdatePotentialAddsExist();

            autoResetEvent.Set();
        }

        public void FakeUpdate()
        {
            Npcs.Clear();
            autoResetEvent.Set();
        }

        public void UpdatePotentialAddsExist()
        {
            TargetCount = Npcs.Count(c => !c.IsAdd && Math.Abs(c.ClickPoint.X - c.screenMid) < c.screenTargetBuffer);
            AddCount = Npcs.Count(c => c.IsAdd);

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
        }

        private List<List<LineOfNpcName>> DetermineNpcs(List<LineOfNpcName> npcNameLine)
        {
            List<List<LineOfNpcName>> npcs = new();

            for (int i = 0; i < npcNameLine.Count; i++)
            {
                var npcLine = npcNameLine[i];
                var group = new List<LineOfNpcName>() { npcLine };
                var lastY = npcLine.Y;

                if (!npcLine.IsInAgroup)
                {
                    for (int j = i + 1; j < npcNameLine.Count; j++)
                    {
                        var laterNpcLine = npcNameLine[j];
                        if (laterNpcLine.Y > npcLine.Y + ScaleHeight(DetermineNpcsHeightOffset1)) { break; } // 10
                        if (laterNpcLine.Y > lastY + ScaleHeight(DetermineNpcsHeightOffset2)) { break; } // 5

                        if (laterNpcLine.XStart <= npcLine.X && laterNpcLine.XEnd >= npcLine.X && laterNpcLine.Y > lastY)
                        {
                            laterNpcLine.IsInAgroup = true;
                            group.Add(laterNpcLine);
                            lastY = laterNpcLine.Y;
                            npcNameLine[j] = laterNpcLine;
                        }
                    }
                    if (group.Count > 0) { npcs.Add(group); }
                }
            }

            return npcs;
        }

        private List<LineOfNpcName> PopulateLinesOfNpcNames()
        {
            List<LineOfNpcName> npcNameLine = new();

            float minLength = ScaleWidth(LinesOfNpcMinLength);
            float lengthDiff = ScaleWidth(LinesOfNpcLengthDiff);
            float minEndLength = minLength - lengthDiff;

            unsafe
            {
                BitmapData bitmapData = bitmapProvider.Bitmap.LockBits(new Rectangle(0, 0, bitmapProvider.Bitmap.Width, bitmapProvider.Bitmap.Height), ImageLockMode.ReadOnly, bitmapProvider.Bitmap.PixelFormat);
                int bytesPerPixel = Bitmap.GetPixelFormatSize(bitmapProvider.Bitmap.PixelFormat) / 8;

                //for (int y = Area.Top; y < Area.Height; y += incY)
                Parallel.For(Area.Top, Area.Height, y =>
                {
                    bool isEndOfSection;
                    var lengthStart = -1;
                    var lengthEnd = -1;

                    byte* currentLine = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);
                    for (int x = Area.Left; x < Area.Right; x += incX)
                    {
                        int xi = x * bytesPerPixel;

                        if (colorMatcher(Color.FromArgb(255, currentLine[xi + 2], currentLine[xi + 1], currentLine[xi])))
                        {
                            if (lengthStart > -1 && (x - lengthEnd) < minLength)
                            {
                                lengthEnd = x;
                            }
                            else
                            {
                                isEndOfSection = lengthStart > -1 && lengthEnd - lengthStart > minEndLength;
                                if (isEndOfSection)
                                {
                                    npcNameLine.Add(new LineOfNpcName(lengthStart, lengthEnd, y));
                                }

                                lengthStart = x;
                            }
                            lengthEnd = x;
                        }
                    }

                    isEndOfSection = lengthStart > -1 && lengthEnd - lengthStart > minEndLength;
                    if (isEndOfSection)
                    {
                        npcNameLine.Add(new LineOfNpcName(lengthStart, lengthEnd, y));
                    }
                });

                bitmapProvider.Bitmap.UnlockBits(bitmapData);
            }

            return npcNameLine;
        }

        public void WaitForNUpdate(int n)
        {
            while (n >= 0)
            {
                autoResetEvent.WaitOne();
                n--;
            }
        }

        public void ShowNames(Graphics gr)
        {
            if (Npcs.Count <= 0)
            {
                return;
            }

            using var whitePen = new Pen(Color.White, 3);
            using var greyPen = new Pen(Color.Gray, 3);

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

            Npcs.ForEach(n => gr.DrawRectangle(n.IsAdd ? greyPen : whitePen, new Rectangle(n.Min, new Size(n.Width, n.Height))));
        }

        public Point ToScreenCoordinates(int x, int y)
        {
            return new Point(bitmapProvider.Rect.X + x, bitmapProvider.Rect.Top + y);
        }
    }
}