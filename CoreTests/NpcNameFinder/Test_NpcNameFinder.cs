using System.Drawing;
using Core.Goals;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using SharedLib;
using System.Threading;
using System.Text;
using System.Diagnostics;
using System.Linq;

#pragma warning disable 0162

namespace CoreTests
{
    public class Test_NpcNameFinder
    {
        private const bool saveImage = true;
        private const bool LogEachUpdate = true;

        private readonly ILogger logger;
        private readonly NpcNameFinder npcNameFinder;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly RectProvider rectProvider;
        private readonly BitmapCapturer capturer;

        private readonly Stopwatch stopwatch = new();
        private readonly StringBuilder stringBuilder = new();

        private readonly Graphics paint;
        private readonly Bitmap paintBitmap;
        private readonly Font font = new("Arial", 10);
        private readonly SolidBrush brush = new(Color.White);
        private readonly Pen whitePen = new(Color.White, 1);

        public Test_NpcNameFinder(ILogger logger, NpcNames types)
        {
            this.logger = logger;

            rectProvider = new();
            rectProvider.GetRectangle(out Rectangle rect);
            capturer = new(rect);

            npcNameFinder = new(logger, capturer, new AutoResetEvent(false));

            MockWoWProcess mockWoWProcess = new();
            MockWoWScreen mockWoWScreen = new();
            npcNameTargeting = new(logger, new(), mockWoWScreen, npcNameFinder, mockWoWProcess);

            npcNameFinder.ChangeNpcType(types);

            if (saveImage)
            {
                paintBitmap = capturer.Bitmap;
                paint = Graphics.FromImage(paintBitmap);
            }
        }

        public void Execute()
        {
            if (LogEachUpdate)
                stopwatch.Restart();

            capturer.Capture();

            if (LogEachUpdate)
                logger.LogInformation($"Capture: {stopwatch.ElapsedMilliseconds}ms");

            if (LogEachUpdate)
                stopwatch.Restart();

            npcNameFinder.Update();

            if (LogEachUpdate)
                logger.LogInformation($"Update: {stopwatch.ElapsedMilliseconds}ms");

            if (saveImage)
            {
                SaveImage();
            }

            if (LogEachUpdate)
            {
                stringBuilder.Length = 0;

                if (npcNameFinder.Npcs.Any())
                    stringBuilder.AppendLine();

                int i = 0;
                foreach (NpcPosition n in npcNameFinder.Npcs)
                {
                    stringBuilder.Append($"{i,2}");
                    stringBuilder.Append(" -> rect=");
                    stringBuilder.Append(n.Rect);
                    stringBuilder.Append(" ClickPoint=");
                    stringBuilder.AppendLine($"{{{n.ClickPoint.X,4},{n.ClickPoint.Y,4}}}");
                    i++;
                }

                logger.LogInformation(stringBuilder.ToString());
            }
        }

        private void SaveImage()
        {
            if (npcNameFinder.Npcs.Any())
            {
                paint.DrawRectangle(whitePen, npcNameFinder.Area);

                int i = 0;
                foreach (var n in npcNameFinder.Npcs)
                {
                    foreach (var l in npcNameTargeting.locTargetingAndClickNpc)
                    {
                        paint.DrawEllipse(whitePen, l.X + n.ClickPoint.X, l.Y + n.ClickPoint.Y, 5, 5);
                    }

                    paint.DrawRectangle(whitePen, n.Rect);
                    paint.DrawString(i.ToString(), font, brush, new PointF(n.Left - 20f, n.Top));
                    i++;
                }
            }

            paintBitmap.Save("target_names.png");
        }
    }
}
